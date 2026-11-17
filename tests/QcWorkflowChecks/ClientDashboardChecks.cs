using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

static class ClientDashboardChecks
{
    static void Check(bool ok, string message) {if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message);}
    static User User(UserRole role) => new() {Email = Guid.NewGuid() + "@example.test", DisplayName = role.ToString(), PasswordHash = "test", Role = role, ApprovalStatus = ApprovalStatus.Approved};
    static ClaimsPrincipal Principal(User u) => new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, u.Id.ToString()), new(ClaimTypes.Role, u.Role.ToString())], "Test"));
    public static async Task Run()
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(ClientDashboardController).Assembly).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen(ProjectUploadOpenApi.Configure);
        var databaseName = "client-" + Guid.NewGuid();
        builder.Services.AddDbContext<SurveyDbContext>(o => o.UseInMemoryDatabase(databaseName));
        builder.Services.AddSingleton<IProjectPhotoStorage, FakeStorage>();
        builder.Services.AddSingleton<IGeneratedReportArtifactStorage, FakeArchive>();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, Auth>("Test", _ => {}); builder.Services.AddAuthorization();
        await using var app = builder.Build(); app.UseSwagger(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
        var c = User(UserRole.Client); var other = User(UserRole.Client); var manager = User(UserRole.Manager); var qc = User(UserRole.Qc); var surveyor = User(UserRole.Surveyor);
        db.Users.AddRange(c, other, manager, qc, surveyor);
        Project P(string name, Guid client) => new() {ProjectName = name, Sector = "Shipping", JobNo = name, Location = "Dubai", VesselOrSiteName = "Vessel", AssignedClientId = client, AssignedSurveyorId = surveyor.Id, AssignedQcId = qc.Id, CreatedByUserId = manager.Id, Status = ProjectStatus.Active};
        var own = P("Own", c.Id); var foreign = P("Foreign", other.Id); var empty = P("No inspections", c.Id);
        db.Projects.AddRange(own, foreign, empty);
        var ownArea = new ProjectArea {ProjectId = own.Id, AreaName = "Deck"}; var foreignArea = new ProjectArea {ProjectId = foreign.Id, AreaName = "Other deck"}; db.ProjectAreas.AddRange(ownArea, foreignArea);
        AreaEquipment E(Project p, ProjectArea a, int n) => new() {ProjectId = p.Id, AreaId = a.Id, SequenceNo = n, TagNumber = p.ProjectName + n, Type = "Motor", CreatedByUserId = manager.Id};
        var pin = E(own, ownArea, 1); var untouched = E(own, ownArea, 2); var foreignPin = E(foreign, foreignArea, 1); db.AreaEquipment.AddRange(pin, untouched, foreignPin);
        var session = new SurveySession {AreaEquipmentId = pin.Id, SurveyorId = surveyor.Id, SubmittedAt = DateTimeOffset.UtcNow, Status = SurveyStatus.Submitted, SurveyorRemarks = "Original survey remark"};
        var foreignSession = new SurveySession {AreaEquipmentId = foreignPin.Id, SurveyorId = surveyor.Id, SubmittedAt = DateTimeOffset.UtcNow, Status = SurveyStatus.Submitted}; db.SurveySessions.AddRange(session, foreignSession);
        var review = new QcReview {SurveySessionId = session.Id, ReviewedByUserId = qc.Id, Status = QcReviewStatus.PendingReview, Comments = "Original QC remark"}; db.QcReviews.Add(review);
        var finding = new Finding {SurveySessionId = session.Id, Observation = "Damaged seal", Recommendation = "Replace seal", Severity = "High"}; var otherFinding = new Finding {SurveySessionId = foreignSession.Id, Observation = "Private defect"}; db.Findings.AddRange(finding, otherFinding);
        var photo = new SurveyFile {FindingId = finding.Id, FileName = "before.png", StorageKey = "private/before.png", ContentType = "image/png", UploadedById = surveyor.Id, Category = FileCategory.FindingPhoto};
        var foreignPhoto = new SurveyFile {FindingId = otherFinding.Id, FileName = "private.png", StorageKey = "private/other.png", ContentType = "image/png", UploadedById = surveyor.Id, Category = FileCategory.FindingPhoto}; db.Files.AddRange(photo, foreignPhoto);
        QcWorkflowHistory.Record(db, session.Id, surveyor.Id, "Surveyor Submitted"); QcWorkflowHistory.Record(db, foreignSession.Id, surveyor.Id, "Surveyor Submitted"); await db.SaveChangesAsync();
        await app.StartAsync(); using var http = new HttpClient {BaseAddress = new Uri(app.Urls.Single())};
        Check((await http.GetAsync("/api/client/dashboard")).StatusCode == HttpStatusCode.Unauthorized, "Anonymous access rejected");
        http.DefaultRequestHeaders.Add("X-Test-User", c.Id.ToString()); http.DefaultRequestHeaders.Add("X-Test-Role", "Client");
        var dash = await http.GetFromJsonAsync<JsonElement>("/api/client/dashboard"); var counts = dash.GetProperty("counts");
        Check(dash.GetProperty("projects").GetArrayLength() == 2 && counts.GetProperty("awaitingQc").GetInt32() == 1 && counts.GetProperty("openDefects").GetInt32() == 1, "Dashboard counts only assigned projects, submitted QC work and actual defects");
        var progress = dash.GetProperty("projects").EnumerateArray().Single(p => p.GetProperty("projectId").GetGuid() == own.Id).GetProperty("progress");
        Check(progress.GetProperty("surveyProgress").GetDecimal() == 50 && progress.GetProperty("areasCompleted").GetInt32() == 0, "Backend progress excludes uninspected equipment and incomplete areas");
        foreach (var route in new[] {$"projects/{foreign.Id}", $"projects/{foreign.Id}/areas", $"projects/{foreign.Id}/defects", $"projects/{foreign.Id}/reports", $"projects/{foreign.Id}/remarks", $"areas/{foreignArea.Id}/equipment", $"equipment/{foreignPin.Id}", $"files/{foreignPhoto.Id}"})
            Check((await http.GetAsync("/api/client/" + route)).StatusCode == HttpStatusCode.Forbidden, "Foreign project access returns 403: " + route.Split('/')[0]);
        Check((await http.GetAsync($"/api/reports/projects/{foreign.Id}/pdf")).StatusCode == HttpStatusCode.Forbidden, "Existing report download enforces client access");
        foreach (var route in new[] {$"projects/{own.Id}", $"projects/{own.Id}/areas", $"areas/{ownArea.Id}/equipment", $"equipment/{pin.Id}", "defects", "reports", "notifications", $"files/{photo.Id}"})
            Check((await http.GetAsync("/api/client/" + route)).StatusCode == HttpStatusCode.OK, "Assigned project read succeeds: " + route.Split('/')[0]);
        var defectText = await http.GetStringAsync("/api/client/defects"); Check(!defectText.Contains("private/") && !defectText.Contains("Private defect"), "Client defects expose authenticated URLs without physical storage paths or foreign data");
        var manual = new Finding {RegisterEquipmentId=pin.Id,RegisterAuthorId=surveyor.Id,RegisterDate=new DateOnly(2026,11,12),RegisterGrade=InspectionType.Close,
            Observation="Registered defect without a survey session",FaultCategory="B",FaultCode="REG-1",AosdId="AOSD-1",Remarks="First saved remark",AdditionalRemarks="Second saved remark"};
        var foreignManual = new Finding {RegisterEquipmentId=foreignPin.Id,RegisterAuthorId=surveyor.Id,Observation="Foreign registered defect"};
        var manualPhotos = new[] {1,2}.Select(n=>new SurveyFile {FindingId=manual.Id,FileName=$"registered-{n}.png",StorageKey=$"private/register-{n}.png",ContentType="image/png",UploadedById=surveyor.Id,Category=FileCategory.FindingPhoto}).ToArray();
        db.Findings.AddRange(manual,foreignManual);db.Files.AddRange(manualPhotos);await db.SaveChangesAsync();
        var register = await http.GetFromJsonAsync<JsonElement>("/api/client/defects");
        var registeredRow = register.EnumerateArray().Single(r=>r.GetProperty("id").GetGuid()==manual.Id);
        Check(registeredRow.GetProperty("faultCategory").GetString()=="B" && registeredRow.GetProperty("additionalRemarks").GetString()=="Second saved remark" &&
            registeredRow.GetProperty("beforePhotos").GetArrayLength()==2 && !register.ToString().Contains("Foreign registered defect"),"Client register includes manual defects, metadata and both photos within assigned projects only");
        Check((await http.GetAsync($"/api/client/files/{manualPhotos[0].Id}")).StatusCode==HttpStatusCode.OK,"Registered defect evidence uses authenticated project-scoped file access");
        own.AssignedClientId=other.Id;
        var additionalClient=new ProjectClientAssignment {ProjectId=own.Id,ClientUserId=c.Id,IsActive=true};db.ProjectClientAssignments.Add(additionalClient);await db.SaveChangesAsync();
        var sharedRegister=await http.GetStringAsync("/api/client/defects");
        Check(sharedRegister.Contains(manual.Id.ToString()) && !sharedRegister.Contains("Foreign registered defect"),"Multiple Client assignment links authorize the same scoped register without foreign records");
        own.AssignedClientId=c.Id;db.ProjectClientAssignments.Remove(additionalClient);await db.SaveChangesAsync();
        var manualRemark = await http.PostAsJsonAsync("/api/client/remarks",new AddClientRemarkRequest(own.Id,"Registered defect question",pin.Id,manual.Id));
        Check(manualRemark.StatusCode==HttpStatusCode.Created,"Existing Client remark permission supports an authorized registered defect");
        db.ReportClientRemarks.RemoveRange(db.ReportClientRemarks.Where(r=>r.FindingId==manual.Id));db.Files.RemoveRange(manualPhotos);db.Findings.RemoveRange(manual,foreignManual);await db.SaveChangesAsync();
        var created = await http.PostAsJsonAsync("/api/client/remarks", new AddClientRemarkRequest(own.Id, "Please clarify", pin.Id, finding.Id));
        Check(created.StatusCode == HttpStatusCode.Created, "Client can add a contextual remark");
        var saved = await created.Content.ReadFromJsonAsync<JsonElement>(); Check(saved.GetProperty("clientUserId").GetGuid() == c.Id && !saved.GetProperty("isRead").GetBoolean(), "Remark author comes from authentication and starts unread");
        Check((await http.PostAsJsonAsync("/api/client/remarks", new AddClientRemarkRequest(own.Id, "Wrong context", foreignPin.Id))).StatusCode == HttpStatusCode.Forbidden, "Cannot attach remark to another client's equipment");
        Check((await http.PostAsJsonAsync("/api/client/remarks", new AddClientRemarkRequest(foreign.Id, "Wrong project"))).StatusCode == HttpStatusCode.Forbidden, "Cannot remark on another client's project");
        Check((await http.PostAsJsonAsync("/api/client/remarks", new AddClientRemarkRequest(own.Id, " "))).StatusCode == HttpStatusCode.BadRequest, "Blank remarks rejected");
        Check(session.SurveyorRemarks == "Original survey remark" && review.Comments == "Original QC remark", "Client remarks do not alter Surveyor or QC data");
        foreach (var route in new[] {$"/api/qc/inspections/{session.Id}/start-review", $"/api/qc/inspections/{session.Id}/approve", $"/api/qc/inspections/{session.Id}/request-changes", "/api/projects"})
            Check((await http.PostAsJsonAsync(route, new {comments = "Forbidden"})).StatusCode == HttpStatusCode.Forbidden, "Client technical write blocked: " + route.Split('/')[2]);
        Check((await http.PutAsJsonAsync($"/api/qc/inspections/{session.Id}/comments", new {comments = "Forbidden"})).StatusCode == HttpStatusCode.Forbidden, "Client cannot edit QC comments");
        Check((await http.PutAsJsonAsync($"/api/projects/{own.Id}", new {})).StatusCode == HttpStatusCode.Forbidden, "Client cannot edit assignments or project details");
        Check((await http.DeleteAsync($"/api/projects/{own.Id}")).StatusCode == HttpStatusCode.Forbidden, "Client cannot delete projects");
        finding.Status = FindingStatus.Closed; review.Status = QcReviewStatus.Approved; session.Status = SurveyStatus.Approved; review.ReviewedAt = DateTimeOffset.UtcNow;
        QcWorkflowHistory.Record(db, session.Id, qc.Id, "QC Approved", review); await db.SaveChangesAsync();
        dash = await http.GetFromJsonAsync<JsonElement>("/api/client/dashboard"); counts = dash.GetProperty("counts");
        Check(counts.GetProperty("awaitingQc").GetInt32() == 0 && counts.GetProperty("qcApproved").GetInt32() == 1 && counts.GetProperty("closedDefects").GetInt32() == 1, "Counts reflect QC approval and actual defect closure");
        var notes = await http.GetStringAsync("/api/client/notifications"); Check(notes.Contains("QC Approved") && notes.Contains("Defect Closed") && !notes.Contains("Foreign"), "Notifications use real scoped audit events");
        Check((await http.GetAsync("/swagger/v1/swagger.json")).StatusCode == HttpStatusCode.OK, "Swagger includes client endpoints without route/schema conflicts");
        http.DefaultRequestHeaders.Remove("X-Test-Role"); http.DefaultRequestHeaders.Add("X-Test-Role", "Manager"); Check((await http.GetAsync("/api/client/dashboard")).StatusCode == HttpStatusCode.Forbidden, "Client-specific APIs require Client role");
        await app.StopAsync(); Console.WriteLine("Client HTTP access/workflow checks passed.");
    }
    public static async Task Sql()
    {
        var root = Path.GetFullPath("Ex-SurveyApp_api"); var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json", true).AddJsonFile("appsettings.Development.json", true).AddEnvironmentVariables().Build();
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer(config.GetConnectionString("SurveyDatabase")).Options);
        Check(await db.Database.CanConnectAsync(), "SQL database reachable");
        Check(!await db.Database.GetPendingMigrationsAsync().ContinueWith(t => t.Result.Any()), "Client remarks migration applied");
        var users = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Client).ToListAsync();
        foreach (var user in users) {
            var data = new ClientDashboardData(db, Principal(user)); var summaries = await data.Summaries(default); var ids = summaries.Select(p => p.ProjectId).ToArray();
            Check(await db.Projects.Where(p => ids.Contains(p.Id)).AllAsync(p => p.AssignedClientId == user.Id), "SQL client summary query respects AssignedClientId");
            await data.Activity(default);
            var controller = new ClientDashboardController(db, new FakeStorage()) {ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext {HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext {User = Principal(user)}}};
            Check(await controller.Dashboard(default) is Microsoft.AspNetCore.Mvc.OkObjectResult, "SQL dashboard aggregates translate and execute");
            Check(await controller.Defects(default) is Microsoft.AspNetCore.Mvc.OkObjectResult, "SQL defects query executes with client scope");
            foreach (var project in summaries.Take(1)) Check(await new ProjectReportData(db).Load(project.ProjectId, Principal(user), default) != null, "SQL client report/details reuse existing dynamic loader");
        }
        await db.ReportClientRemarks.AsNoTracking().Take(1).ToListAsync(); Console.WriteLine("Client SQL query and schema checks passed.");
    }
    sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(Guid.TryParse(Request.Headers["X-Test-User"], out var id)
            ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString())], Scheme.Name)), Scheme.Name)) : AuthenticateResult.NoResult());
    }
    sealed class FakeArchive : IGeneratedReportArtifactStorage {
        public Task<ArchivedReportFile> SaveAsync(Guid projectId, Guid id, string name, byte[] bytes, CancellationToken ct) => throw new NotSupportedException("Client reads do not create report archives.");
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<StoredProjectPhotoContent?>(null);
    }
    sealed class FakeStorage : IProjectPhotoStorage {
        public int MaxFilesPerRequest => 20;
        public Task<StoredProjectPhoto> SaveAsync(Microsoft.AspNetCore.Http.IFormFile f, Guid p, Guid a, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveInspectionAsync(Microsoft.AspNetCore.Http.IFormFile f, Guid p, Guid i, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveHazardousAreaPlanAsync(Microsoft.AspNetCore.Http.IFormFile f, Guid p, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string path, CancellationToken ct = default) => Task.FromResult<StoredProjectPhotoContent?>(new(new MemoryStream([1,2,3]), "image/png"));
        public Task DeleteAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }
}
