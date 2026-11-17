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

internal static class AccountSecurityChecks
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
            Check((await client.GetAsync("/api/account/profile")).StatusCode == HttpStatusCode.Unauthorized, "Profile requires authentication");
            foreach (var u in users)
            {
                Bearer(await Login(u, "Original@123"));
                var profile = await client.GetFromJsonAsync<JsonElement>("/api/account/profile");
                Check(profile.GetProperty("id").GetGuid() == u.Id && profile.GetProperty("role").GetString() == u.Role.ToString(), "Own profile loads for " + u.Role);
                var saved = await client.PutAsJsonAsync("/api/account/profile", new {displayName = "Updated " + u.Role, phoneNumber = "+971 50 123 4567", designation = "Inspector", id = disabled.Id, role = "Admin", isActive = false});
                Check(saved.StatusCode == HttpStatusCode.OK, "Permitted profile edits save for " + u.Role);
                var again = await client.GetFromJsonAsync<JsonElement>("/api/account/profile");
                Check(again.GetProperty("displayName").GetString() == "Updated " + u.Role && again.GetProperty("phoneNumber").GetString() == "+971 50 123 4567" && again.GetProperty("isActive").GetBoolean() && again.GetProperty("role").GetString() == u.Role.ToString(), "Payload cannot change identity, role or activation");
            }
            Check((await db.Users.AsNoTracking().SingleAsync(u => u.Id == disabled.Id)).DisplayName == "disabled", "Other users are unchanged by My Profile");
            var admin = users.Single(u => u.Role == UserRole.Admin); Bearer(await Login(admin, "Original@123"));
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jR1UAAAAASUVORK5CYII=");
            async Task<HttpResponseMessage> Photo(string name, string type, byte[] bytes) {using var form = new MultipartFormDataContent(); var content = new ByteArrayContent(bytes); content.Headers.ContentType = new(type); form.Add(content, "photo", name); return await client.PutAsync("/api/account/profile/photo", form);}
            Check((await Photo("profile.pdf", "application/pdf", "%PDF-"u8.ToArray())).StatusCode == HttpStatusCode.BadRequest, "Profile rejects PDF uploads");
            Check((await Photo("profile.png", "image/png", new byte[12])).StatusCode == HttpStatusCode.BadRequest, "Profile validates file signatures");
            Check((await Photo("profile.png", "image/png", png)).StatusCode == HttpStatusCode.OK, "Profile PNG upload succeeds");
            var firstPhoto = await client.GetFromJsonAsync<JsonElement>("/api/account/profile");
            Check((await client.GetAsync(firstPhoto.GetProperty("profilePhotoUrl").GetString())).StatusCode == HttpStatusCode.OK, "Stored photo is served through authenticated own-profile route");
            Check((await Photo("replacement.png", "image/png", png)).StatusCode == HttpStatusCode.OK && Directory.GetFiles(Path.Combine(root, "profile-photos"), "*", SearchOption.AllDirectories).Length == 1, "Replacing a photo removes the previous stored file");
            Check((await client.DeleteAsync("/api/account/profile/photo")).StatusCode == HttpStatusCode.OK && (await client.GetAsync("/api/account/profile/photo")).StatusCode == HttpStatusCode.NotFound, "Removing a photo persists");
            var oldSession = await Login(admin, "Original@123"); Bearer(null);
            foreach (var address in new[] {"unknown@example.test", disabled.Email, pending.Email})
            {
                var response = await client.PostAsJsonAsync("/api/account/forgot-password", new {email = address});
                Check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString() == AccountController.GenericResetMessage, "Unknown, disabled and pending accounts receive the generic reset response");
            }
            Check(mail.Messages.Count == 0, "Reset cannot activate disabled or pending accounts");
            await client.PostAsJsonAsync("/api/account/forgot-password", new {email = admin.Email});
            var token = Token(mail.Messages.Last().Body, "reset-password"); var stored = await db.AccountActionTokens.SingleAsync();
            Check(stored.TokenHash != token && stored.TokenHash.Length == 64 && stored.ExpiresAt - stored.CreatedAt == TimeSpan.FromMinutes(20), "Reset persists a hash only and expires in twenty minutes");
            Check(mail.Messages.Last().Body.Contains("https://app.example.test/#reset-password?token="), "Reset origin comes from trusted HTTPS configuration and secret stays in the URL fragment");
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); stored.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2); await db.SaveChangesAsync();
            Check((await client.PostAsJsonAsync("/api/account/reset-password", new {token, newPassword = "Reset@123", confirmPassword = "Reset@123"})).StatusCode == HttpStatusCode.BadRequest, "Expired reset token is rejected");
            await client.PostAsJsonAsync("/api/account/forgot-password", new {email = admin.Email}); token = Token(mail.Messages.Last().Body, "reset-password");
            var reset = new {token, newPassword = "Reset@123", confirmPassword = "Reset@123"};
            Check((await client.PostAsJsonAsync("/api/account/reset-password", reset)).StatusCode == HttpStatusCode.OK, "Valid reset changes the password");
            Check((await client.PostAsJsonAsync("/api/account/reset-password", reset)).StatusCode == HttpStatusCode.BadRequest, "Reset token cannot be reused");
            Check((await client.PostAsJsonAsync("/api/auth/login", new {email = admin.Email, password = "Original@123"})).StatusCode == HttpStatusCode.Unauthorized, "Previous password no longer works");
            Bearer(oldSession); Check((await client.GetAsync("/api/account/profile")).StatusCode == HttpStatusCode.Unauthorized, "Password reset revokes existing JWT sessions");
            Bearer(await Login(admin, "Reset@123"));
            Check(mail.Messages.Last().Subject.Contains("password was changed") && !mail.Messages.Last().Body.Contains("Reset@123"), "Password-change confirmation contains no password");
            Check((await client.PostAsJsonAsync("/api/account/change-password", new {currentPassword = "wrong", newPassword = "Changed@123", confirmPassword = "Changed@123"})).StatusCode == HttpStatusCode.BadRequest, "Change Password verifies the current password");
            var currentSession = client.DefaultRequestHeaders.Authorization!.Parameter;
            Check((await client.PostAsJsonAsync("/api/account/change-password", new {currentPassword = "Reset@123", newPassword = "Changed@123", confirmPassword = "Changed@123"})).StatusCode == HttpStatusCode.OK, "Authenticated Change Password succeeds");
            Check((await client.GetAsync("/api/account/profile")).StatusCode == HttpStatusCode.Unauthorized, "Password change revokes the old current session too");
            Bearer(await Login(admin, "Changed@123"));
            Check((await client.PostAsJsonAsync("/api/account/email-change", new {email = users[1].Email, currentPassword = "Changed@123"})).StatusCode == HttpStatusCode.Conflict, "Duplicate login email is rejected");
            var oldEmail = admin.Email;
            Check((await client.PostAsJsonAsync("/api/account/email-change", new {email = "verified@example.test", currentPassword = "Changed@123"})).StatusCode == HttpStatusCode.OK, "Email change requests verification");
            var pendingProfile = await client.GetFromJsonAsync<JsonElement>("/api/account/profile");
            Check(pendingProfile.GetProperty("email").GetString() == oldEmail && pendingProfile.GetProperty("pendingEmail").GetString() == "verified@example.test", "Original login stays active before verification");
            Check(mail.Messages.Last().Recipient == oldEmail && mail.Messages.Last().Subject.Contains("change requested"), "Previous email is notified about the pending change");
            var emailToken = Token(mail.Messages.Single(m => m.Recipient == "verified@example.test").Body, "verify-email");
            Check((await client.PostAsJsonAsync("/api/account/reset-password", new {token = emailToken, newPassword = "bad", confirmPassword = "bad"})).StatusCode == HttpStatusCode.BadRequest, "Email token cannot be used to reset a password");
            var pendingToken = await db.AccountActionTokens.SingleAsync(t => t.Purpose == "EmailChange");
            pendingToken.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); pendingToken.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2); await db.SaveChangesAsync();
            Check((await client.PostAsJsonAsync("/api/account/verify-email", new {token = emailToken})).StatusCode == HttpStatusCode.BadRequest, "Expired email verification is rejected without changing the login email");
            await client.PostAsJsonAsync("/api/account/email-change", new {email = "verified@example.test", currentPassword = "Changed@123"});
            emailToken = Token(mail.Messages.Last(m => m.Recipient == "verified@example.test").Body, "verify-email");
            Bearer(null);
            Check((await client.PostAsJsonAsync("/api/account/verify-email", new {token = emailToken})).StatusCode == HttpStatusCode.OK, "Email verification commits the new login identifier");
            Check((await client.PostAsJsonAsync("/api/account/verify-email", new {token = emailToken})).StatusCode == HttpStatusCode.BadRequest, "Email verification is single use");
            Check((await client.PostAsJsonAsync("/api/auth/login", new {email = oldEmail, password = "Changed@123"})).StatusCode == HttpStatusCode.Unauthorized, "Old email stops working after verification");
            admin.Email = "verified@example.test"; Bearer(await Login(admin, "Changed@123"));
            db.ChangeTracker.Clear(); var refreshed = await db.Users.SingleAsync(u => u.Id == admin.Id);
            Check(refreshed.Id == admin.Id && refreshed.Role == UserRole.Admin && refreshed.ApprovalStatus == ApprovalStatus.Approved, "Verification preserves user identity, role and approval");
            Check(!await db.AccountActionTokens.AnyAsync(t => t.UserId == admin.Id && t.ConsumedAt == null), "Security changes invalidate all outstanding account tokens");
            await BootstrapAccountProvisioner.ProvisionStartupAsync(db, hasher, "Admin", refreshed.Email, "seed-password", "Seed Name", UserRole.Admin);
            refreshed.Email = "renamed-bootstrap@example.test"; await db.SaveChangesAsync();
            await BootstrapAccountProvisioner.ProvisionStartupAsync(db, hasher, "Admin", "verified@example.test", "seed-password", "Seed Name", UserRole.Admin);
            Check(await db.Users.CountAsync() == 7 && refreshed.DisplayName == "Updated Admin" && hasher.VerifyHashedPassword(refreshed, refreshed.PasswordHash, "Changed@123") != PasswordVerificationResult.Failed, "Startup preserves user-managed email, name and password without recreating the old account");
            Bearer(null);
            var manager = users.Single(u => u.Role == UserRole.Manager);
            await client.PostAsJsonAsync("/api/account/forgot-password", new {email = manager.Email});
            var raceToken = Token(mail.Messages.Last().Body, "reset-password");
            var racePayload = new {token = raceToken, newPassword = "Concurrent@123", confirmPassword = "Concurrent@123"};
            var race = await Task.WhenAll(client.PostAsJsonAsync("/api/account/reset-password", racePayload), client.PostAsJsonAsync("/api/account/reset-password", racePayload));
            Check(race.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && race.Count(r => r.StatusCode == HttpStatusCode.BadRequest) == 1, "Concurrent consumption accepts a reset token exactly once");
            var external = new User {Email = "external@example.test", DisplayName = "External", PasswordHash = "", Role = UserRole.Client, ApprovalStatus = ApprovalStatus.Approved};
            db.Users.Add(external); await db.SaveChangesAsync();
            Bearer(new TokenService(settings).Create(external).Token);
            Check(!(await client.GetFromJsonAsync<JsonElement>("/api/account/profile")).GetProperty("canChangePassword").GetBoolean(), "An account without a local password does not receive a password-creation flow");
            Check((await client.PostAsJsonAsync("/api/account/change-password", new {currentPassword = "anything", newPassword = "New@123", confirmPassword = "New@123"})).StatusCode == HttpStatusCode.BadRequest, "Non-local credentials are directed to the identity provider");
            var surveyor = users.Single(u => u.Role == UserRole.Surveyor); Bearer(await Login(surveyor, "Original@123")); mail.FailDelivery = true;
            Check((await client.PostAsJsonAsync("/api/account/email-change", new {email = "delivery-failed@example.test", currentPassword = "Original@123"})).StatusCode == HttpStatusCode.ServiceUnavailable, "Authenticated email change reports SMTP failure without changing the active login");
            Check((await client.GetFromJsonAsync<JsonElement>("/api/account/profile")).GetProperty("email").GetString() == surveyor.Email, "Delivery failure preserves the current email");
            mail.FailDelivery = false; Bearer(null);
            var codeUser = Make("same-screen", UserRole.Qc); db.Users.Add(codeUser); await db.SaveChangesAsync();
            var codeSession = await Login(codeUser, "Original@123"); Bearer(null);
            var beforeCode = mail.Messages.Count;
            foreach (var address in new[] {"unknown@example.test", disabled.Email, pending.Email, external.Email})
                Check((await client.PostAsJsonAsync("/api/account/forgot-password-code", new {email = address})).StatusCode == HttpStatusCode.OK, "Code requests do not reveal unknown or ineligible accounts");
            Check(mail.Messages.Count == beforeCode, "Code recovery does not email ineligible accounts");
            Check((await client.PostAsJsonAsync("/api/account/forgot-password-code", new {email = codeUser.Email})).StatusCode == HttpStatusCode.OK, "Same-screen recovery sends a code");
            var code = Regex.Match(mail.Messages.Last().Body, @"[A-F0-9]{4}(?:-[A-F0-9]{4}){3}").Value;
            Check(code.Length == 19 && !mail.Messages.Last().Body.Contains("#reset-password"), "Email contains a copyable verification code rather than a reset link");
            var savedCode = await db.AccountActionTokens.SingleAsync(t => t.UserId == codeUser.Id && t.Purpose == "PasswordResetCode");
            Check(savedCode.TokenHash.Length == 64 && savedCode.TokenHash != code.Replace("-", "") && savedCode.ExpiresAt - savedCode.CreatedAt == TimeSpan.FromMinutes(20), "Code is stored hashed and expires after twenty minutes");
            object CodePayload(string email, string value, string confirm = "SameScreen@123") => new {email, code = value, newPassword = "SameScreen@123", confirmPassword = confirm};
            Check((await client.PostAsJsonAsync("/api/account/reset-password-code", CodePayload(codeUser.Email, "0000-0000-0000-0000"))).StatusCode == HttpStatusCode.BadRequest, "Incorrect code cannot reset a password");
            Check((await client.PostAsJsonAsync("/api/account/reset-password-code", CodePayload(manager.Email, code))).StatusCode == HttpStatusCode.BadRequest, "Code is bound to its account email");
            Check((await client.PostAsJsonAsync("/api/account/reset-password-code", CodePayload(codeUser.Email, code, "different"))).StatusCode == HttpStatusCode.BadRequest, "Code reset requires matching passwords");
            savedCode.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
            Check((await client.PostAsJsonAsync("/api/account/reset-password-code", CodePayload(codeUser.Email, code))).StatusCode == HttpStatusCode.BadRequest, "Expired verification code is rejected");
            savedCode.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20); await db.SaveChangesAsync();
            Check((await client.PostAsJsonAsync("/api/account/reset-password-code", CodePayload(codeUser.Email, code.ToLowerInvariant()))).StatusCode == HttpStatusCode.OK, "Same-screen code accepts pasted lowercase separators and changes the password");
            Check((await client.PostAsJsonAsync("/api/account/reset-password-code", CodePayload(codeUser.Email, code))).StatusCode == HttpStatusCode.BadRequest, "Verification code cannot be replayed");
            Bearer(codeSession); Check((await client.GetAsync("/api/account/profile")).StatusCode == HttpStatusCode.Unauthorized, "Same-screen reset revokes existing sessions");
            Bearer(null); Check((await client.PostAsJsonAsync("/api/auth/login", new {email = codeUser.Email, password = "Original@123"})).StatusCode == HttpStatusCode.Unauthorized, "Same-screen reset rejects the previous password");
            await Login(codeUser, "SameScreen@123");
            for (var i = 0; i < 105; i++) await client.PostAsJsonAsync("/api/account/forgot-password", new {email = "unknown@example.test"});
            Check((await client.PostAsJsonAsync("/api/account/forgot-password", new {email = "unknown@example.test"})).StatusCode == HttpStatusCode.TooManyRequests, "Anonymous security endpoints enforce rate limiting");
            Console.WriteLine("All account security HTTP checks passed. Isolated fixtures and captured mail only; no real messages sent.");
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
        public Task SendRegistrationOtpAsync(string recipientEmail, string recipientName, string otp, CancellationToken ct) => Task.CompletedTask;
        public Task SendProjectDelayAsync(string recipientEmail, string recipientName, string subject, string body, CancellationToken ct) {if (FailDelivery) throw new IOException("Synthetic delivery failure"); Messages.Add((recipientEmail, subject, body)); return Task.CompletedTask;}
    }
}
