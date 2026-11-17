using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

internal static class UserAccessChecks
{
    public static async Task Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "account-security-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var mail = new MailCapture();
        var settings = new JwtSettings("AccountTest", "AccountTest", "test-only-key-with-at-least-thirty-two-characters");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName = "Development"});
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["AccountSecurity:PublicAppUrl"] = "https://app.example.test/";
        builder.Services.AddDbContext<SurveyDbContext>(o => o.UseInMemoryDatabase(root));
        builder.Services.AddControllers().AddApplicationPart(typeof(AccountController).Assembly).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>(); builder.Services.AddSingleton(settings); builder.Services.AddSingleton<TokenService>(); builder.Services.AddSingleton<IEmailService>(mail); builder.Services.AddSingleton<IAccountMailQueue>(mail);
        builder.Services.Configure<ProjectPhotoStorageSettings>(o => o.RootPath = root); builder.Services.AddSingleton<IProjectPhotoStorage, ProjectPhotoStorage>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
            o.TokenValidationParameters = new() {ValidateIssuer = true, ValidIssuer = settings.Issuer, ValidateAudience = true, ValidAudience = settings.Audience, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), ValidateLifetime = true};
            o.Events = new JwtBearerEvents {OnTokenValidated = async c => {
                var db = c.HttpContext.RequestServices.GetRequiredService<SurveyDbContext>();
                var id = Guid.Parse(c.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
                if (user is null || !user.IsActive || user.ApprovalStatus != ApprovalStatus.Approved || !c.Principal!.IsInRole(user.Role.ToString()) || !TokenService.IsCurrentSession(c.Principal, user)) c.Fail("Session revoked.");
            }};
        });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(o => {o.RejectionStatusCode = 429; o.AddPolicy("AnonymousAuth", _ => RateLimitPartition.GetFixedWindowLimiter("test", _ => new() {PermitLimit = 100, Window = TimeSpan.FromMinutes(5), QueueLimit = 0}));});
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers();
        try
        {
            using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
            var hasher = new PasswordHasher<User>();
            User Make(string name, UserRole role) {var u = new User {Email = name + "@example.test", DisplayName = name, PasswordHash = "", Role = role, ApprovalStatus = ApprovalStatus.Approved}; u.PasswordHash = hasher.HashPassword(u, "Original@123"); return u;}
            var users = Enum.GetValues<UserRole>().Select(r => Make(r.ToString().ToLowerInvariant(), r)).ToArray();
            var disabled = Make("disabled", UserRole.Surveyor); disabled.IsActive = false;
            var pending = Make("pending", UserRole.Client); pending.ApprovalStatus = ApprovalStatus.Pending;
            db.Users.AddRange(users); db.Users.AddRange(disabled, pending); await db.SaveChangesAsync();
            await app.StartAsync(); using var client = new HttpClient {BaseAddress = new Uri(app.Urls.Single())};
            async Task<string> Login(User u, string password) {var r = await client.PostAsJsonAsync("/api/auth/login", new {email = u.Email, password}); Check(r.StatusCode == HttpStatusCode.OK, "Local account login works"); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;}
            void Bearer(string? token) => client.DefaultRequestHeaders.Authorization = token is null ? null : new("Bearer", token);
            var admin = users.Single(u=>u.Role==UserRole.Admin); var manager=users.Single(u=>u.Role==UserRole.Manager);
            var surveyor=users.Single(u=>u.Role==UserRole.Surveyor); var ordinaryClient=users.Single(u=>u.Role==UserRole.Client);
            var requested = Make("pending-admin",UserRole.Admin);requested.ApprovalStatus=ApprovalStatus.Pending;db.Users.Add(requested);await db.SaveChangesAsync();
            Bearer(null);
            var email="new-registration@example.test";
            Check((await client.PostAsJsonAsync("/api/auth/register/send-otp",new{name="New Registration",email,role="Surveyor"})).StatusCode==HttpStatusCode.OK,"Registration OTP flow remains functional");
            var otp=mail.Messages.Last().Body;
            Check((await client.PostAsJsonAsync("/api/auth/register/verify-otp",new{email,otp})).StatusCode==HttpStatusCode.OK,"Registration verifies the existing OTP");
            var completed=await client.PostAsJsonAsync("/api/auth/register/complete",new{email,password="Registered@123"});
            Check(completed.StatusCode==HttpStatusCode.Created,"Registration completion creates an account");
            Check((await completed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accountStatus").GetString()=="PendingApproval","Registration explicitly reports PendingApproval");
            var newcomer=await db.Users.SingleAsync(u=>u.Email==email);
            Check(newcomer.ApprovalStatus==ApprovalStatus.Pending && newcomer.RegisteredAt!=null,"New registration records its date and awaits approval");
            Check((await client.PostAsJsonAsync("/api/auth/register/complete",new{email,password="Registered@123"})).StatusCode!=HttpStatusCode.Created,"Duplicate registration cannot create a second account");
            var pendingLogin=await client.PostAsJsonAsync("/api/auth/login",new{email,password="Registered@123"});
            Check(pendingLogin.StatusCode==HttpStatusCode.Forbidden && (await pendingLogin.Content.ReadAsStringAsync()).Contains("Your account is awaiting administrator approval."),"Pending login shows the approval message");
            Bearer(new TokenService(settings).Create(newcomer).Token);
            Check((await client.GetAsync("/api/account/profile")).StatusCode==HttpStatusCode.Unauthorized,"Pending accounts cannot enter protected modules even with a token");
            var adminJwt=await Login(admin,"Original@123");var managerJwt=await Login(manager,"Original@123");
            Bearer(managerJwt);
            var pendingRows=await client.GetFromJsonAsync<JsonElement>("/api/auth/approvals/pending");
            Check(pendingRows.EnumerateArray().All(u=>u.GetProperty("role").GetString() is not ("Admin" or "Manager")),"Manager pending list excludes Admin and Manager registrations");
            Check((await client.PutAsync($"/api/auth/approvals/{requested.Id}/approve",null)).StatusCode==HttpStatusCode.Forbidden,"Manager cannot approve Admin registrations");
            var managerNotices=await client.GetFromJsonAsync<JsonElement>("/api/user-access/notifications");
            Check(managerNotices.EnumerateArray().Count(n=>n.GetProperty("targetUserId").GetGuid()==newcomer.Id)==1,"Manager receives exactly one persistent registration alert");
            Bearer(adminJwt);var adminNotices=await client.GetFromJsonAsync<JsonElement>("/api/user-access/notifications");
            Check(adminNotices.EnumerateArray().Count(n=>n.GetProperty("targetUserId").GetGuid()==newcomer.Id)==1,"Admin receives exactly one persistent registration alert");
            var noticeId=adminNotices.EnumerateArray().Single(n=>n.GetProperty("targetUserId").GetGuid()==newcomer.Id).GetProperty("id").GetGuid();
            Bearer(managerJwt);Check((await client.PutAsync($"/api/user-access/notifications/{noticeId}/read",null)).StatusCode==HttpStatusCode.NotFound,"Notification read operations enforce recipient ownership");
            Check((await client.PutAsJsonAsync($"/api/auth/approvals/{newcomer.Id}/reject",new{reason=""})).StatusCode==HttpStatusCode.BadRequest,"Registration rejection requires a reason");
            Check((await client.PutAsync($"/api/auth/approvals/{newcomer.Id}/approve",null)).StatusCode==HttpStatusCode.OK,"Authorized Manager can approve Surveyor registration");
            db.ChangeTracker.Clear();var approved=await db.Users.SingleAsync(u=>u.Id==newcomer.Id);
            Check(approved.ApprovalStatus==ApprovalStatus.Approved && approved.ApprovalReviewedByUserId==manager.Id && approved.ApprovalReviewedAt!=null,"Approval saves reviewer and timestamp");
            var counts=await client.GetFromJsonAsync<JsonElement>("/api/user-access/counts");
            Check(counts.GetProperty("pendingRegistrations").GetInt32()==1,"Manager pending count updates after approval and excludes privileged accounts");
            Bearer(await Login(approved,"Registered@123"));
            Check((await client.GetFromJsonAsync<JsonElement>("/api/user-access/notifications")).EnumerateArray().Any(n=>n.GetProperty("kind").GetString()=="Decision"),"Approved user receives a durable approval notification");
            Bearer(adminJwt);Check((await client.PutAsJsonAsync($"/api/auth/approvals/{requested.Id}/reject",new{reason="Not authorized"})).StatusCode==HttpStatusCode.OK,"Admin rejects a registration with a reason");
            db.ChangeTracker.Clear();var rejected=await db.Users.SingleAsync(u=>u.Id==requested.Id);
            Check(rejected.ApprovalStatus==ApprovalStatus.Rejected && rejected.ApprovalRejectionReason=="Not authorized","Registration rejection preserves the record and reason");
            Bearer(await Login(surveyor,"Original@123"));var surveyorJwt=client.DefaultRequestHeaders.Authorization!.Parameter;
            Check((await client.PostAsJsonAsync("/api/user-access/role-requests",new{requestedRole="Surveyor",reason="Same"})).StatusCode==HttpStatusCode.BadRequest,"Role request must differ from current role");
            Check((await client.PostAsJsonAsync("/api/user-access/role-requests",new{requestedRole="Qc",reason=""})).StatusCode==HttpStatusCode.BadRequest,"Role request requires a reason");
            Check((await client.PostAsJsonAsync("/api/user-access/role-requests",new{requestedRole="Qc",reason="Qualified for review"})).StatusCode==HttpStatusCode.OK,"Authenticated user can request a role change");
            Check((await client.PostAsJsonAsync("/api/user-access/role-requests",new{requestedRole="Client",reason="Duplicate"})).StatusCode==HttpStatusCode.Conflict,"Only one pending role request is allowed");
            var request=(await client.GetFromJsonAsync<JsonElement>("/api/user-access/role-requests")).EnumerateArray().Single();var requestId=request.GetProperty("id").GetGuid();
            db.ChangeTracker.Clear();Check((await db.Users.SingleAsync(u=>u.Id==surveyor.Id)).Role==UserRole.Surveyor,"Submitting a request does not change the user's role");
            Bearer(managerJwt);Check((await client.GetAsync("/api/user-access/role-requests/all")).StatusCode==HttpStatusCode.Forbidden,"Managers cannot list role change requests");
            Check((await client.PutAsJsonAsync($"/api/user-access/role-requests/{requestId}/review",new{decision="Approved"})).StatusCode==HttpStatusCode.Forbidden,"Managers cannot approve role changes");
            var managerCounts=await client.GetFromJsonAsync<JsonElement>("/api/user-access/counts");Check(managerCounts.GetProperty("pendingRoleChanges").GetInt32()==0,"Manager counts do not expose role requests");
            Bearer(adminJwt);var requests=await client.GetFromJsonAsync<JsonElement>("/api/user-access/role-requests/all");Check(requests.GetArrayLength()==1,"Admin can list role change requests");
            Check((await client.GetFromJsonAsync<JsonElement>("/api/user-access/notifications")).EnumerateArray().Any(n=>n.GetProperty("kind").GetString()=="RoleRequest"),"Role requests notify Admin");
            db.JobAssignments.Add(new(){UserId=surveyor.Id,JobId=Guid.NewGuid(),Role=UserRole.Surveyor});await db.SaveChangesAsync();
            Check((await client.PutAsJsonAsync($"/api/user-access/role-requests/{requestId}/review",new{decision="Approved"})).StatusCode==HttpStatusCode.OK,"Admin approves a role change");
            Check((await client.PutAsJsonAsync($"/api/user-access/role-requests/{requestId}/review",new{decision="Approved"})).StatusCode==HttpStatusCode.Conflict,"A role request cannot be approved twice");
            db.ChangeTracker.Clear();var changed=await db.Users.SingleAsync(u=>u.Id==surveyor.Id);
            Check(changed.Role==UserRole.Qc && await db.JobAssignments.AnyAsync(a=>a.UserId==surveyor.Id),"Role approval preserves identity and existing assignment records");
            var decided=await db.RoleChangeRequests.SingleAsync(r=>r.Id==requestId);Check(decided.ReviewedBy==admin.Id&&decided.ReviewedAt!=null&&decided.Status==RoleRequestStatus.Approved,"Role decision saves Admin and timestamp");
            Bearer(surveyorJwt);Check((await client.GetAsync("/api/account/profile")).StatusCode==HttpStatusCode.Unauthorized,"Role approval revokes stale role JWTs");
            Bearer(await Login(changed,"Original@123"));
            Check((await client.PostAsJsonAsync("/api/user-access/role-requests",new{requestedRole="Client",reason="Change duties"})).StatusCode==HttpStatusCode.OK,"A reviewed request permits a new request");
            var second=(await client.GetFromJsonAsync<JsonElement>("/api/user-access/role-requests")).EnumerateArray().Single(r=>r.GetProperty("status").GetString()=="Pending").GetProperty("id").GetGuid();
            Bearer(adminJwt);Check((await client.PutAsJsonAsync($"/api/user-access/role-requests/{second}/review",new{decision="Rejected",reason=""})).StatusCode==HttpStatusCode.BadRequest,"Role rejection requires a reason");
            Check((await client.PutAsJsonAsync($"/api/user-access/role-requests/{second}/review",new{decision="Rejected",reason="Keep current duties"})).StatusCode==HttpStatusCode.OK,"Admin rejects a role request");
            db.ChangeTracker.Clear();Check((await db.Users.SingleAsync(u=>u.Id==surveyor.Id)).Role==UserRole.Qc,"Rejected role request does not change role");
            Check((await client.PutAsync($"/api/user-access/notifications/{noticeId}/read",null)).StatusCode==HttpStatusCode.NoContent,"Notification read state saves on the server");
            db.ChangeTracker.Clear();Check((await db.UserAccessNotifications.SingleAsync(n=>n.Id==noticeId)).ReadAt!=null,"Read state survives a new data context");
            Check((await client.PutAsync($"/api/user-access/notifications/{noticeId}/dismiss",null)).StatusCode==HttpStatusCode.NoContent,"Notifications can be dismissed persistently");
            Check(!(await client.GetFromJsonAsync<JsonElement>("/api/user-access/notifications")).EnumerateArray().Any(n=>n.GetProperty("id").GetGuid()==noticeId),"Dismissed notifications do not return after refresh");
            Check(await db.AuditLogs.AnyAsync(a=>a.Action=="Registration Approved")&&await db.AuditLogs.AnyAsync(a=>a.Action=="Role Request Approved"),"Registration and role decisions preserve audit history");
            Console.WriteLine("All user access workflow checks passed. Isolated database and captured mail only.");
        }
        finally
        {
            await app.StopAsync();
            if (!Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid test cleanup path.");
            Directory.Delete(root, true);
        }
    }
    private static string Token(string body, string action) {var match = Regex.Match(body, "#" + action + @"\?token=([A-Za-z0-9_-]{43})"); if (!match.Success) throw new InvalidOperationException("Expected account link is missing."); return match.Groups[1].Value;}
    private static void Check(bool ok, string message) {if (!ok) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message);}
    private sealed class MailCapture : IEmailService, IAccountMailQueue
    {
        public List<(string Recipient, string Subject, string Body)> Messages {get;} = [];
        public bool FailDelivery {get; set;}
        public bool TryEnqueue(AccountMail message) {Messages.Add((message.Recipient, message.Subject, message.Body)); return true;}
        public Task SendRegistrationOtpAsync(string recipientEmail, string recipientName, string otp, CancellationToken ct)  {Messages.Add((recipientEmail, "Registration OTP", otp)); return Task.CompletedTask;}
        public Task SendProjectDelayAsync(string recipientEmail, string recipientName, string subject, string body, CancellationToken ct) {if (FailDelivery) throw new IOException("Synthetic delivery failure"); Messages.Add((recipientEmail, subject, body)); return Task.CompletedTask;}
    }
}
