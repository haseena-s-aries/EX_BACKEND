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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static class ProjectUploadHttpChecks
{
    public static async Task Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "plan-http-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(ProjectController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(ProjectUploadOpenApi.Configure);
        builder.Services.AddCors(options => options.AddPolicy("AllowFrontend", policy => policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));
        builder.Services.AddDbContext<SurveyDbContext>(o => o.UseInMemoryDatabase(root));
        builder.Services.Configure<ProjectPhotoStorageSettings>(o => o.RootPath = root);
        builder.Services.AddSingleton<IProjectPhotoStorage, ProjectPhotoStorage>();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => {});
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseSwagger(); app.UseCors("AllowFrontend"); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SurveyDbContext>();
            var surveyor = User(UserRole.Surveyor); var qc = User(UserRole.Qc); var clientUser = User(UserRole.Client);
            db.Users.AddRange(surveyor, qc, clientUser); await db.SaveChangesAsync();
            var data = new UpsertProjectRequest("HTTP plan", "Marine", "HTTP-1", "Dubai", "Site", surveyor.Id, qc.Id, clientUser.Id);
            await app.StartAsync();
            using var client = new HttpClient {BaseAddress = new Uri(app.Urls.Single())};
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            json.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
            var swagger = await client.GetAsync("/swagger/v1/swagger.json");
            Check(swagger.StatusCode == HttpStatusCode.OK, "Swagger JSON loads without duplicate-route conflict");
            using var document = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
            foreach (var (path, method) in new[] {("/api/projects", "post"), ("/api/projects/{id}", "put")})
            {
                var bodies = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("requestBody").GetProperty("content");
                Check(bodies.TryGetProperty("application/json", out _) && bodies.TryGetProperty("multipart/form-data", out var multipartSchema) && multipartSchema.GetProperty("schema").GetProperty("properties").GetProperty("hazardousAreaPlan").GetProperty("format").GetString() == "binary", "Swagger documents JSON and optional file upload for " + method);
            }
            client.DefaultRequestHeaders.Add("Origin", "http://localhost:3000");
            var unauthenticated = await client.PostAsJsonAsync("/api/projects", data, json);
            Check(unauthenticated.StatusCode == HttpStatusCode.Unauthorized, "HTTP create requires authentication");
            Check(unauthenticated.Headers.TryGetValues("Access-Control-Allow-Origin", out var cors) && cors.Single() == "http://localhost:3000", "Authentication errors include CORS so browser can show session errors");
            client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
            var jsonCreate = await client.PostAsJsonAsync("/api/projects", data, json);
            Check(jsonCreate.StatusCode == HttpStatusCode.Created, "Existing JSON creation route remains usable");
            var id = (await jsonCreate.Content.ReadFromJsonAsync<ProjectResponse>(json))!.Id;
            var noClient = data with {JobNo = "optional-client", AssignedClientId = null};
            var noClientResult = await client.PostAsJsonAsync("/api/projects", noClient, json);
            Check(noClientResult.StatusCode == HttpStatusCode.Created, "JSON project can omit Client");
            var noClientProject = (await noClientResult.Content.ReadFromJsonAsync<ProjectResponse>(json))!;
            Check(noClientProject.AssignedClientId is null && noClientProject.ProjectName == "HTTP PLAN" && noClientProject.JobNo == "OPTIONAL-CLIENT" && noClientProject.Location == "DUBAI" && noClientProject.VesselOrSiteName == "SITE", "Project text is stored uppercase and Client remains null");
            var editedNoClient = await client.PutAsJsonAsync($"/api/projects/{id}", noClient with {JobNo = data.JobNo}, json);
            Check(editedNoClient.StatusCode == HttpStatusCode.OK && (await editedNoClient.Content.ReadFromJsonAsync<ProjectResponse>(json))!.AssignedClientId is null, "Edit can explicitly clear Client assignment");
            var invalidQc = await client.PostAsJsonAsync("/api/projects", noClient with {JobNo = "invalid-qc", AssignedQcId = Guid.Empty}, json);
            Check(invalidQc.StatusCode == HttpStatusCode.BadRequest, "QC remains required without Client");
            using var noClientForm = Form(noClient with {JobNo = "optional-client-upload"}, "OriginalCase.pdf", "application/pdf", "%PDF-1.7\noriginal\n%%EOF"u8.ToArray());
            var noClientUpload = await client.PostAsync("/api/projects", noClientForm);
            Check(noClientUpload.StatusCode == HttpStatusCode.Created && (await noClientUpload.Content.ReadFromJsonAsync<ProjectResponse>(json))!.HazardousAreaPlanFileName == "OriginalCase.pdf", "Multipart project omits Client and preserves uploaded filename case");
            using var form = Form(data with {JobNo = "HTTP-2"}, "plan.pdf", "application/pdf", "%PDF-1.7\noriginal\n%%EOF"u8.ToArray());
            var multipart = await client.PostAsync("/api/projects", form);
            var body = await multipart.Content.ReadAsStringAsync();
            Check(multipart.StatusCode == HttpStatusCode.Created, "Multipart form binds existing project DTO and optional upload: " + body);
            var uploaded = (await multipart.Content.ReadFromJsonAsync<ProjectResponse>(json))!;
            Check(uploaded.HazardousAreaPlanFileName == "plan.pdf", "HTTP creation returns original filename");
            using var replace = Form(data, "replacement.jpeg", "image/jpeg", [0xff, 0xd8, 0xff, 1, 2, 3, 4]);
            Check((await client.PutAsync($"/api/projects/{id}", replace)).StatusCode == HttpStatusCode.OK, "Multipart edit route uploads replacement");
            using var invalid = Form(data with {JobNo = "HTTP-3"}, "bad.png", "image/png", [1, 2, 3]);
            Check((await client.PostAsync("/api/projects", invalid)).StatusCode == HttpStatusCode.BadRequest, "HTTP upload rejects unsupported file");
            var download = await client.GetAsync(uploaded.HazardousAreaPlanPath);
            Check(download.StatusCode == HttpStatusCode.OK && download.Content.Headers.ContentType?.MediaType == "application/pdf", "Authenticated HTTP download serves original PDF");
            client.DefaultRequestHeaders.Remove("X-Test-Role");
            Check((await client.GetAsync(uploaded.HazardousAreaPlanPath)).StatusCode == HttpStatusCode.Unauthorized, "File route cannot be opened anonymously");
            foreach (var role in new[] {"Qc", "Surveyor", "Client"})
            {
                client.DefaultRequestHeaders.Remove("X-Test-Role");
                client.DefaultRequestHeaders.Add("X-Test-Role", role);
                Check((await client.DeleteAsync($"/api/projects/{uploaded.Id}")).StatusCode == HttpStatusCode.Forbidden, role + " cannot delete a project");
            }
            client.DefaultRequestHeaders.Remove("X-Test-Role");
            client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
            Check((await client.DeleteAsync($"/api/projects/{uploaded.Id}")).StatusCode == HttpStatusCode.NoContent, "Manager can delete a project");
            Check((await client.GetAsync(uploaded.HazardousAreaPlanPath)).StatusCode == HttpStatusCode.NotFound, "Deleted project plan is no longer accessible");
            client.DefaultRequestHeaders.Remove("X-Test-Role");
            client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
            Check((await client.DeleteAsync($"/api/projects/{id}")).StatusCode == HttpStatusCode.NoContent, "Admin retains project delete access");
            Console.WriteLine("All project upload HTTP checks passed.");
        }
        finally {await app.StopAsync(); Directory.Delete(root, true);}
    }
    private static MultipartFormDataContent Form(UpsertProjectRequest data, string name, string mime, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        foreach (var property in typeof(UpsertProjectRequest).GetProperties())
            if (property.GetValue(data) is {} value) form.Add(new StringContent(value.ToString()!), property.Name);
        var content = new ByteArrayContent(bytes); content.Headers.ContentType = new(mime);
        form.Add(content, "hazardousAreaPlan", name); return form;
    }
    private static User User(UserRole role) => new() {Email = role + "@http.invalid", DisplayName = role.ToString(), PasswordHash = "test", Role = role, ApprovalStatus = ApprovalStatus.Approved};
    private static void Check(bool passed, string message) {if (!passed) throw new Exception(message); Console.WriteLine("PASS: " + message);}
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, Guid.Empty.ToString()), new(ClaimTypes.Role, role)], "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
}
