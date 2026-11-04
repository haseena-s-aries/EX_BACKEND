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
            db.CompanyMasters.AddRange(CompanyAssets.Seed());
            var surveyor = User(UserRole.Surveyor); var qc = User(UserRole.Qc); var clientUser = User(UserRole.Client);
            db.Users.AddRange(surveyor, qc, clientUser); await db.SaveChangesAsync();
            var data = new UpsertProjectRequest("HTTP plan", "Marine", "HTTP-1", "Dubai", "Site", surveyor.Id, qc.Id, clientUser.Id);
            await app.StartAsync();
            using var client = new HttpClient {BaseAddress = new Uri(app.Urls.Single())};
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            json.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
            var swagger = await client.GetAsync("/swagger/v1/swagger.json");
            var swaggerContent = await swagger.Content.ReadAsStringAsync();
            Check(swagger.StatusCode == HttpStatusCode.OK, "Swagger JSON loads without duplicate-route conflict (HTTP " + (int)swagger.StatusCode + ")");
            using var document = JsonDocument.Parse(swaggerContent);
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
            var missingProject = await client.GetAsync($"/api/projects/{Guid.NewGuid()}");
            Check(missingProject.StatusCode == HttpStatusCode.NotFound, "An old or missing project ID returns HTTP 404");
            var companyResponse = await client.GetAsync("/api/companies");
            var companies = (await companyResponse.Content.ReadFromJsonAsync<CompanyResponse[]>(json))!;
            Check(companyResponse.StatusCode == HttpStatusCode.OK && companies.Length == 11 && companies.Select(c => c.Id).Distinct().Count() == 11, "Company master preserves all 11 workbook rows as separate options");
            var df = companies.Single(c => c.DisplayName.EndsWith("- DF")); var dn = companies.Single(c => c.DisplayName.EndsWith("- DN"));
            Check(df.CompanyName == dn.CompanyName && df.Id != dn.Id && df.Email == "design@ariesgroup.ae, offshoredesign@ariesgroup.ae" && dn.Email == "design@ariesgroup.ae", "Repeated company names retain exact department-specific contacts");
            var filteredCompanies = (await client.GetFromJsonAsync<CompanyResponse[]>("/api/companies?search=aries%20marine%20wll", json))!;
            Check(filteredCompanies.Length == 1 && filteredCompanies[0].DisplayName.EndsWith("- DB") && filteredCompanies[0].Telephone is null, "Case-insensitive company search handles missing contacts");
            var companyLogo = await client.GetAsync(df.LogoUrl);
            Check(companyLogo.StatusCode == HttpStatusCode.OK && companyLogo.Content.Headers.ContentType?.MediaType == "image/png", "Company logo resolves the exact predefined workbook filename");
            var companyCreate = await client.PostAsJsonAsync("/api/projects", data with {JobNo = "company-project", OurCompanyId = df.Id}, json);
            Check(companyCreate.StatusCode == HttpStatusCode.Created, "Existing project create accepts optional company relationship");
            var companyProject = (await companyCreate.Content.ReadFromJsonAsync<ProjectResponse>(json))!;
            Check(companyProject.OurCompanyId == df.Id && companyProject.OurCompany?.Email == df.Email, "Project creation auto-populates company details from selected row");
            var companyEdit = await client.PutAsJsonAsync($"/api/projects/{companyProject.Id}", data with {JobNo = "company-project", OurCompanyId = dn.Id}, json);
            var editedCompanyProject = (await companyEdit.Content.ReadFromJsonAsync<ProjectResponse>(json))!;
            Check(companyEdit.StatusCode == HttpStatusCode.OK && editedCompanyProject.OurCompany?.Email == dn.Email && editedCompanyProject.OurCompanyId == dn.Id, "Changing selection updates the complete company relationship");
            var companyDetails = (await client.GetFromJsonAsync<ProjectResponse>($"/api/projects/{companyProject.Id}", json))!;
            Check(companyDetails.OurCompany?.DisplayName == dn.DisplayName && await db.CompanyMasters.CountAsync() == 11 && (await db.CompanyMasters.FindAsync(df.Id))!.Email == df.Email, "Edit/details reload selected company without changing or duplicating master rows");
            Check((await client.PostAsJsonAsync("/api/projects", data with {JobNo = "invalid-company", OurCompanyId = Guid.NewGuid()}, json)).StatusCode == HttpStatusCode.BadRequest, "Unknown company selection is rejected");
            var jsonCreate = await client.PostAsJsonAsync("/api/projects", data, json);
            Check(jsonCreate.StatusCode == HttpStatusCode.Created, "Existing JSON creation route remains usable");
            var id = (await jsonCreate.Content.ReadFromJsonAsync<ProjectResponse>(json))!.Id;
            var metadata = data with {JobNo = "metadata-project", AssignedClientId = null, UidNumber = " uid-226869 ", DocumentNumber = " kd2259/5001 ", Revision = " rev a ", Description = " initial issue ", ClientName = " client company "};
            using var logoForm = Form(metadata, "plan.pdf", "application/pdf", "%PDF-1.7\noriginal\n%%EOF"u8.ToArray());
            var logoBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jR1UAAAAASUVORK5CYII=");
            var logoContent = new ByteArrayContent(logoBytes); logoContent.Headers.ContentType = new("image/png");
            logoForm.Add(logoContent, "clientLogo", "OriginalCase.png");
            var metadataCreate = await client.PostAsync("/api/projects", logoForm);
            Check(metadataCreate.StatusCode == HttpStatusCode.Created, "Optional metadata and client logo bind through existing multipart creation endpoint");
            var metadataProject = (await metadataCreate.Content.ReadFromJsonAsync<ProjectResponse>(json))!;
            Check(metadataProject.UidNumber == "UID-226869" && metadataProject.DocumentNumber == "KD2259/5001" && metadataProject.Revision == "REV A" && metadataProject.Description == "INITIAL ISSUE" && metadataProject.ClientName == "CLIENT COMPANY" && metadataProject.AssignedClientId is null, "Metadata is trimmed uppercase and client details stay independent of assignment");
            Check(metadataProject.ClientLogoPath == $"/api/projects/{metadataProject.Id}/client-logo", "Client logo response exposes only authenticated URL");
            var logoDownload = await client.GetAsync(metadataProject.ClientLogoPath);
            Check(logoDownload.StatusCode == HttpStatusCode.OK && (await logoDownload.Content.ReadAsByteArrayAsync()).SequenceEqual(logoBytes), "Client logo download preserves original image bytes");
            var metadataEdit = await client.PutAsJsonAsync($"/api/projects/{metadataProject.Id}", metadata with {Description = " updated issue "}, json);
            Check(metadataEdit.StatusCode == HttpStatusCode.OK && (await metadataEdit.Content.ReadFromJsonAsync<ProjectResponse>(json))!.ClientLogoPath == metadataProject.ClientLogoPath, "Editing without a new logo preserves existing image");
            using var replacementLogoForm = Form(metadata, "plan.pdf", "application/pdf", "%PDF-1.7\noriginal\n%%EOF"u8.ToArray());
            var replacementBytes = logoBytes.Concat(new byte[] {0}).ToArray(); var replacementContent = new ByteArrayContent(replacementBytes); replacementContent.Headers.ContentType = new("image/png"); replacementLogoForm.Add(replacementContent, "clientLogo", "ReplacementCase.png");
            Check((await client.PutAsync($"/api/projects/{metadataProject.Id}", replacementLogoForm)).StatusCode == HttpStatusCode.OK && (await (await client.GetAsync(metadataProject.ClientLogoPath)).Content.ReadAsByteArrayAsync()).SequenceEqual(replacementBytes), "Explicit upload replaces client logo while preserving submitted bytes");
            foreach (var invalidLogoCase in new[] {("logo.png", "image/jpeg", logoBytes), ("logo.jpg", "image/jpeg", logoBytes), ("logo.png", "image/png", new byte[] {1, 2, 3})}) {
                using var invalidForm = Form(metadata with {JobNo = "bad-logo-" + Guid.NewGuid()}, "plan.pdf", "application/pdf", "%PDF-1.7\noriginal\n%%EOF"u8.ToArray());
                var content = new ByteArrayContent(invalidLogoCase.Item3); content.Headers.ContentType = new(invalidLogoCase.Item2); invalidForm.Add(content, "clientLogo", invalidLogoCase.Item1);
                Check((await client.PostAsync("/api/projects", invalidForm)).StatusCode == HttpStatusCode.BadRequest, "Logo rejects MIME/signature mismatch: " + invalidLogoCase.Item1);
            }
            using var invalidLogoForm = Form(metadata with {JobNo = "invalid-logo"}, "plan.pdf", "application/pdf", "%PDF-1.7\noriginal\n%%EOF"u8.ToArray());
            var invalidLogoContent = new ByteArrayContent(logoBytes); invalidLogoContent.Headers.ContentType = new("image/webp"); invalidLogoForm.Add(invalidLogoContent, "clientLogo", "logo.webp");
            Check((await client.PostAsync("/api/projects", invalidLogoForm)).StatusCode == HttpStatusCode.BadRequest, "Client logo upload rejects unsupported formats on backend");
            client.DefaultRequestHeaders.Remove("X-Test-Role");
            Check((await client.GetAsync(metadataProject.ClientLogoPath)).StatusCode == HttpStatusCode.Unauthorized, "Client logo requires authentication");
            client.DefaultRequestHeaders.Add("X-Test-Role", "Qc");
            Check((await client.GetAsync(metadataProject.ClientLogoPath)).StatusCode == HttpStatusCode.NotFound, "Unassigned QC cannot view client logo");
            client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
            var removeLogo = await client.PutAsJsonAsync($"/api/projects/{metadataProject.Id}", metadata with {RemoveClientLogo = true}, json);
            Check(removeLogo.StatusCode == HttpStatusCode.OK && (await removeLogo.Content.ReadFromJsonAsync<ProjectResponse>(json))!.ClientLogoPath is null && (await client.GetAsync(metadataProject.ClientLogoPath)).StatusCode == HttpStatusCode.NotFound, "Explicit removal clears client logo and revokes download");
            Check(document.RootElement.GetProperty("paths").TryGetProperty("/api/reports/projects/{projectId}/pdf", out _), "Swagger documents report PDF endpoint");
            Check(document.RootElement.GetProperty("paths").TryGetProperty("/api/reports/projects/{projectId}/haer", out _)
                && document.RootElement.GetProperty("paths").TryGetProperty("/api/reports/projects/{projectId}/haer.csv", out _)
                && document.RootElement.GetProperty("paths").TryGetProperty("/api/reports/projects/{projectId}/haer.pdf", out _)
                && document.RootElement.GetProperty("paths").TryGetProperty("/api/reports/projects/{projectId}/haer/excel", out _), "Swagger documents scoped HAER JSON, CSV, PDF and Excel endpoints");
            Check((await client.GetAsync("/api/reports/projects")).StatusCode == HttpStatusCode.OK, "Manager can load scoped report project list");
            var reportResponse = await client.GetAsync($"/api/reports/projects/{id}");
            Check(reportResponse.StatusCode == HttpStatusCode.OK && !(await reportResponse.Content.ReadAsStringAsync()).Contains("storageKey", StringComparison.OrdinalIgnoreCase), "Report aggregate HTTP response loads without physical storage keys");
            var haerResponse = await client.GetAsync($"/api/reports/projects/{id}/haer");
            var haerJson = await haerResponse.Content.ReadAsStringAsync();
            Check(haerResponse.StatusCode == HttpStatusCode.OK && haerJson.Contains("equipment", StringComparison.OrdinalIgnoreCase) && !haerJson.Contains("StorageKey", StringComparison.OrdinalIgnoreCase), "HAER JSON is scoped and exposes register data without physical storage keys");
            var haerCsv = await client.GetAsync($"/api/reports/projects/{id}/haer.csv");
            var haerCsvBody = await haerCsv.Content.ReadAsStringAsync();
            Check(haerCsv.StatusCode == HttpStatusCode.OK && haerCsv.Content.Headers.ContentType?.MediaType == "text/csv" && haerCsvBody.Contains("Equipment ID", StringComparison.Ordinal), "HAER CSV export includes register columns and CSV MIME type");
            var haerPdfResponse = await client.GetAsync($"/api/reports/projects/{id}/haer.pdf");
            var haerPdfBytes = await haerPdfResponse.Content.ReadAsByteArrayAsync();
            Check(haerPdfResponse.StatusCode == HttpStatusCode.OK && haerPdfResponse.Content.Headers.ContentType?.MediaType == "application/pdf" && haerPdfBytes.AsSpan(0, Math.Min(5, haerPdfBytes.Length)).SequenceEqual("%PDF-"u8), $"HAER PDF HTTP export generates a valid PDF (status {haerPdfResponse.StatusCode}, type {haerPdfResponse.Content.Headers.ContentType?.MediaType}, size {haerPdfBytes.Length})");
            var haerExcelResponse = await client.GetAsync($"/api/reports/projects/{id}/haer/excel");
            var haerExcelBody = await haerExcelResponse.Content.ReadAsStringAsync();
            Check(haerExcelResponse.StatusCode == HttpStatusCode.OK && haerExcelResponse.Content.Headers.ContentType?.MediaType == "application/vnd.ms-excel" && haerExcelBody.Contains("urn:schemas-microsoft-com:office:spreadsheet", StringComparison.Ordinal), "HAER Excel-compatible HTTP export returns a readable workbook");
            var reportPdf = await client.GetAsync($"/api/reports/projects/{id}/pdf");
            var pdfBytes = await reportPdf.Content.ReadAsByteArrayAsync();
            Check(reportPdf.StatusCode == HttpStatusCode.OK && reportPdf.Content.Headers.ContentType?.MediaType == "application/pdf" && pdfBytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8), "Report PDF HTTP endpoint generates an empty project safely");
            client.DefaultRequestHeaders.Remove("X-Test-Role");
            Check((await client.GetAsync($"/api/reports/projects/{id}/pdf")).StatusCode == HttpStatusCode.Unauthorized, "Report PDF requires authentication");
            client.DefaultRequestHeaders.Add("X-Test-Role", "Qc");
            Check((await client.GetAsync($"/api/reports/projects/{id}")).StatusCode == HttpStatusCode.NotFound && (await client.GetAsync($"/api/reports/projects/{id}/pdf")).StatusCode == HttpStatusCode.NotFound && (await client.GetAsync($"/api/reports/projects/{id}/haer")).StatusCode == HttpStatusCode.NotFound && (await client.GetAsync($"/api/reports/projects/{id}/haer.csv")).StatusCode == HttpStatusCode.NotFound && (await client.GetAsync($"/api/reports/projects/{id}/haer.pdf")).StatusCode == HttpStatusCode.NotFound && (await client.GetAsync($"/api/reports/projects/{id}/haer/excel")).StatusCode == HttpStatusCode.NotFound, "Unassigned QC cannot access report data, PDF or HAER exports");
            client.DefaultRequestHeaders.Remove("X-Test-Role");
            client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
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
            using var pngPlanForm = Form(data, "plan.PNG", "image/png", logoBytes);
            var pngPlanResponse = await client.PutAsync($"/api/projects/{id}", pngPlanForm);
            var pngPlan = (await pngPlanResponse.Content.ReadFromJsonAsync<ProjectResponse>(json))!;
            Check(pngPlanResponse.StatusCode == HttpStatusCode.OK && pngPlan.HazardousAreaPlanContentType == "image/png", "PNG plan uploads and response preserves PNG MIME");
            var pngDownload = await client.GetAsync(pngPlan.HazardousAreaPlanPath);
            Check(pngDownload.Content.Headers.ContentType?.MediaType == "image/png" && (await pngDownload.Content.ReadAsByteArrayAsync()).SequenceEqual(logoBytes), "PNG plan download preserves original bytes and MIME");
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
