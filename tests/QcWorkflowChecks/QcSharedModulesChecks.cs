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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static class QcSharedModulesChecks
{
    public static async Task Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "qc-shared-modules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["TenantFoundation:Enabled"] = "false";
        builder.Configuration["InspectionActivities:Enabled"] = "true";
        builder.Services.AddControllers().AddApplicationPart(typeof(ProjectController).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddDbContext<SurveyDbContext>(o => o.UseInMemoryDatabase(root));
        builder.Services.Configure<ProjectPhotoStorageSettings>(o => o.RootPath = root);
        builder.Services.AddSingleton<IProjectPhotoStorage>(new FakePhotos());
        builder.Services.AddScoped<ProjectScheduling>();
        builder.Services.AddTenantFoundation(builder.Configuration);
        builder.Services.AddInspectionActivityFoundation(builder.Configuration);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
            User U(string name, UserRole role) => new() {DisplayName = name, Email = name + "@test.invalid", PasswordHash = "fixture", Role = role, ApprovalStatus = ApprovalStatus.Approved, IsActive = true};
            var admin = U("admin", UserRole.Admin); var qc = U("qc", UserRole.Qc); var other = U("other-qc", UserRole.Qc); var surveyor = U("surveyor", UserRole.Surveyor);
            db.Users.AddRange(admin, qc, other, surveyor);
            Project P(string name, Guid primary, Guid? additional = null) => new() {ProjectName = name, JobNo = name, Sector = "", Location = "Test", VesselOrSiteName = "Vessel", AssignedQcId = primary, AdditionalQcId = additional, AssignedSurveyorId = surveyor.Id, CreatedByUserId = admin.Id, ClientName = "Test Company"};
            var primary = P("Primary", qc.Id); var additional = P("Additional", other.Id, qc.Id); var excluded = P("Excluded", other.Id);
            db.Projects.AddRange(primary, additional, excluded);
            var fixture = new List<(Project Project, ProjectArea Area, AreaEquipment Equipment, Finding Defect, SurveyFile Photo)>();
            foreach (var project in new[] {primary, additional, excluded})
            {
                var area = new ProjectArea {ProjectId = project.Id, AreaName = project.ProjectName + " Deck"};
                var equipment = new AreaEquipment {ProjectId = project.Id, AreaId = area.Id, TagNumber = project.JobNo + "-1", Type = "Motor", SequenceNo = 1, CreatedByUserId = surveyor.Id};
                var finding = new Finding {RegisterEquipmentId = equipment.Id, RegisterAuthorId = surveyor.Id, Observation = project.ProjectName + " observation", RegisterDate = new DateOnly(2026, 11, 13), RegisterGrade = InspectionType.Visual, FaultCategory = "B", AosdId = "AOSD-1", Remarks = "First remark", AdditionalRemarks = "Second remark"};
                var photo = new SurveyFile {ProjectId = project.Id, AreaEquipmentId = equipment.Id, FindingId = finding.Id, FileName = "photo.png", StorageKey = "not-read-outside-scope", ContentType = "image/png", Category = FileCategory.FindingPhoto, EquipmentPhotoRole = "DefectPhoto1", UploadedById = surveyor.Id};
                db.ProjectAreas.Add(area); db.AreaEquipment.Add(equipment); db.Findings.Add(finding); db.Files.Add(photo);
                fixture.Add((project, area, equipment, finding, photo));
            }
            await db.SaveChangesAsync();
            await app.StartAsync();
            using var client = new HttpClient {BaseAddress = new Uri(app.Urls.Single())};
            client.DefaultRequestHeaders.Add("X-Test-Actor", qc.Id.ToString());
            client.DefaultRequestHeaders.Add("X-Test-Role", "Qc");
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var projectList = await client.GetFromJsonAsync<JsonElement>("/api/projects");
            Check(projectList.GetArrayLength() == 2 && projectList.EnumerateArray().All(p => p.GetProperty("id").GetGuid() != excluded.Id), "Shared Surveys returns Primary and Additional QC projects only");
            foreach (var f in fixture.Take(2))
            {
                foreach (var path in new[] {
                    $"/api/projects/{f.Project.Id}", $"/api/projects/{f.Project.Id}/areas/{f.Area.Id}/photos",
                    $"/api/projects/{f.Project.Id}/common-layouts", $"/api/projects/{f.Project.Id}/work-schedule",
                    $"/api/v1/haer/projects/{f.Project.Id}/register",
                    $"/api/defects/register/projects/{f.Project.Id}/equipment",
                    $"/api/defects/{f.Defect.Id}/photos/{f.Photo.Id}",
                    $"/api/v1/inspection-activities/category-checklist/projects/{f.Project.Id}/selection?equipmentId={f.Equipment.Id}"
                }) Check((await client.GetAsync(path)).StatusCode == HttpStatusCode.OK, "Assigned QC read: " + path);
            }
            foreach (var path in new[] {
                $"/api/projects/{excluded.Id}", $"/api/projects/{excluded.Id}/areas/{fixture[2].Area.Id}/photos",
                $"/api/projects/{excluded.Id}/common-layouts", $"/api/projects/{excluded.Id}/work-schedule",
                $"/api/v1/haer/projects/{excluded.Id}/register", $"/api/defects/register/projects/{excluded.Id}/equipment",
                $"/api/v1/inspection-activities/category-checklist/projects/{excluded.Id}/selection?equipmentId={fixture[2].Equipment.Id}",
                $"/api/defects/{fixture[2].Defect.Id}/photos/{fixture[2].Photo.Id}"
            }) Check((await client.GetAsync(path)).StatusCode == HttpStatusCode.NotFound, "Forged unassigned ID is rejected: " + path);
            Check((await client.GetAsync($"/api/v1/inspection-activities/category-checklist/projects/{primary.Id}/selection?equipmentId={fixture[2].Equipment.Id}")).StatusCode == HttpStatusCode.NotFound, "Equipment from another project cannot be mixed into an assigned checklist");
            foreach (var path in new[] {"/api/defects/register/projects", "/api/v1/equipment-identification/registration-projects"})
            {
                var response = await client.GetAsync(path);
                Check(response.StatusCode == HttpStatusCode.OK, "Scoped picker read succeeds: " + path);
                var rows = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
                Check(rows.GetArrayLength() == 2, "Picker includes exactly two assigned projects");
            }
            var defects = await client.GetFromJsonAsync<JsonElement>("/api/defects?openOnly=false");
            Check(defects.GetProperty("items").GetArrayLength() == 2 && defects.GetProperty("items").EnumerateArray().All(d => !d.GetProperty("canEdit").GetBoolean()), "Shared register returns assigned defects without write capability");
            var excludedDefects = await client.GetFromJsonAsync<JsonElement>($"/api/defects?openOnly=false&projectId={excluded.Id}");
            Check(excludedDefects.GetProperty("items").GetArrayLength() == 0, "Forged defect project filter cannot leak records");
            Check((await client.GetAsync("/api/v1/inspection-activities/category-checklist/catalog")).StatusCode == HttpStatusCode.OK, "QC can read the shared dynamic category catalog");
            var selection = await client.GetFromJsonAsync<JsonElement>($"/api/v1/inspection-activities/category-checklist/projects/{primary.Id}/selection?equipmentId={fixture[0].Equipment.Id}");
            Check(selection.GetProperty("points").GetArrayLength() == 59, "QC sees all 59 original checkpoint definitions without writing a selection");
            foreach (var (method, path) in new[] {
                (HttpMethod.Post, "/api/projects"), (HttpMethod.Put, $"/api/projects/{primary.Id}"), (HttpMethod.Delete, $"/api/projects/{primary.Id}"),
                (HttpMethod.Post, $"/api/projects/{primary.Id}/areas"),
                (HttpMethod.Put, $"/api/defects/register/projects/{primary.Id}/{fixture[0].Defect.Id}"),
                (HttpMethod.Put, $"/api/defects/register/{fixture[0].Defect.Id}/photos/1"),
                (HttpMethod.Delete, $"/api/defects/register/{fixture[0].Defect.Id}/photos/1"),
                (HttpMethod.Put, $"/api/v1/haer/equipment/{fixture[0].Equipment.Id}/metadata"),
                (HttpMethod.Put, $"/api/v1/inspection-activities/category-checklist/projects/{primary.Id}/selection?equipmentId={fixture[0].Equipment.Id}"),
                (HttpMethod.Put, "/api/v1/inspection-activities/category-checklist/catalog/categories"),
                (HttpMethod.Post, $"/api/v1/inspection-activities/defects/reconcile/{primary.Id}")
            })
            {
                using var request = new HttpRequestMessage(method, path) {Content = method == HttpMethod.Put && path.Contains("/photos/") ? new MultipartFormDataContent() : JsonContent.Create(new { })};
                Check((await client.SendAsync(request)).StatusCode == HttpStatusCode.Forbidden, "QC restricted write is rejected: " + method + " " + path);
            }
            Check(db.Projects.Count() == 3 && db.AreaEquipment.Count() == 3 && db.Findings.Count() == 3 && !db.ChecklistVersionSnapshots.Any(), "Browsing and denied writes create no duplicate or altered records");
            client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
            Check((await client.GetFromJsonAsync<JsonElement>("/api/projects")).GetArrayLength() == 3, "Existing Admin project scope is preserved");
            Console.WriteLine("All QC shared-module HTTP checks passed. Isolated in-memory test data only.");
        }
        finally {await app.StopAsync(); Directory.Delete(root, true);}
    }

    private static void Check(bool value, string message) {if (!value) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message);}
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-Actor"], out var id)) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString())], "Test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
        }
    }
}
