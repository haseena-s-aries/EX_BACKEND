using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class HazardousAreaPlanChecks
{
    public static async Task Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "plan-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new ProjectPhotoStorage(Options.Create(new ProjectPhotoStorageSettings {RootPath = root}), new TestEnvironment(root));
            byte[] jpeg = [0xff, 0xd8, 0xff, 0xe0, 1, 2, 3, 4, 5, 6, 7, 8, 0xff, 0xd9];
            byte[] pdf = "%PDF-1.7\nOriginal quality PDF bytes\n%%EOF"u8.ToArray();
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aWQAAAABJRU5ErkJggg==");
            foreach (var item in new[] {("plan.JPG", "image/jpeg", jpeg), ("plan.jpeg", "image/jpeg", jpeg), ("plan.PNG", "image/png", png), ("plan.pdf", "application/pdf", pdf)})
            {
                var stored = await storage.SaveHazardousAreaPlanAsync(File(item.Item1, item.Item2, item.Item3), Guid.NewGuid(), default);
                var content = (await storage.OpenReadAsync(stored.RelativePath))!;
                using (content.Stream) { using var copy = new MemoryStream(); await content.Stream.CopyToAsync(copy); Check(copy.ToArray().SequenceEqual(item.Item3), "Original " + item.Item1 + " bytes are preserved"); }
                Check(content.ContentType == item.Item2, "Download MIME is derived from validated extension");
            }
            foreach (var item in new[] {
                (Name: "plan.png", Mime: "image/png", Bytes: jpeg, Message: "The file content does not match an allowed PDF or image type."),
                (Name: "plan.pdf", Mime: "image/jpeg", Bytes: pdf, Message: "Only JPG, JPEG, PNG and PDF files are allowed."),
                (Name: "plan.jpg", Mime: "image/jpeg", Bytes: pdf, Message: "The file content does not match an allowed PDF or image type."),
                (Name: "plan.pdf", Mime: "application/pdf", Bytes: jpeg, Message: "The file content does not match an allowed PDF or image type."),
                (Name: "plan.exe", Mime: "application/pdf", Bytes: pdf, Message: "Only JPG, JPEG, PNG and PDF files are allowed.")
            })
            {
                try { await storage.SaveHazardousAreaPlanAsync(File(item.Name, item.Mime, item.Bytes), Guid.NewGuid(), default); throw new Exception("Accepted unsupported plan"); }
                catch (ProjectPhotoValidationException ex) { Check(ex.Message == item.Message, "Reject extension/MIME/content mismatch: " + item.Name); }
            }
            var safe = await storage.SaveHazardousAreaPlanAsync(File("../../unsafe.pdf", "application/pdf", pdf), Guid.NewGuid(), default);
            Check(safe.OriginalFileName == "unsafe.pdf" && !safe.RelativePath.Contains(".."), "Generated storage path prevents filename traversal");
            try { await storage.OpenReadAsync("../outside.pdf"); throw new Exception("Path escaped storage root"); }
            catch (InvalidOperationException) { Check(true, "Download rejects paths outside storage root"); }
            await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var manager = User("Manager", UserRole.Manager); var qc = User("QC", UserRole.Qc); var surveyor = User("Surveyor", UserRole.Surveyor); var client = User("Client", UserRole.Client); var outsider = User("Outsider", UserRole.Client);
            db.Users.AddRange(manager, qc, surveyor, client, outsider); await db.SaveChangesAsync();
            var controller = As(new ProjectController(db, storage, NullLogger<ProjectController>.Instance), manager);
            var request = new UpsertProjectRequest("Plan project", "Marine", "PLAN-1", "Dubai", "Site", surveyor.Id, qc.Id, client.Id, ClientName: "Test Client Company", TargetDate: ProjectScheduling.Today.AddDays(30));
            var create = await controller.CreateProjectWithPlan(request, File("hazardous-area-plan.pdf", "application/pdf", pdf), default);
            var response = (ProjectResponse)((CreatedAtActionResult)create.Result!).Value!;
            var project = db.Projects.Single();
            Check(response.HazardousAreaPlanPath == $"/api/projects/{project.Id}/hazardous-area-plan" && response.HazardousAreaPlanFileName == "hazardous-area-plan.pdf" && response.HazardousAreaPlanContentType == "application/pdf", "Response exposes authenticated URL and metadata only");
            var originalPath = project.HazardousAreaPlanPath;
            await controller.UpdateProject(project.Id, request with {ProjectName = "Edited"}, default);
            Check(project.HazardousAreaPlanPath == originalPath, "JSON edit preserves existing plan");
            var invalid = await controller.UpdateProjectWithPlan(project.Id, request, File("bad.png", "image/png", jpeg), default);
            Check(invalid.Result is BadRequestObjectResult && project.HazardousAreaPlanPath == originalPath && System.IO.File.Exists(Path.Combine(root, originalPath!)), "Invalid replacement keeps existing document");
            await controller.UpdateProjectWithPlan(project.Id, request, File("replacement.jpg", "image/jpeg", jpeg), default);
            Check(project.HazardousAreaPlanPath != originalPath && !System.IO.File.Exists(Path.Combine(root, originalPath!)), "Successful explicit replacement switches file and cleans old storage");
            foreach (var user in new[] {manager, qc, surveyor, client})
            {
                var download = await As(new ProjectController(db, storage, NullLogger<ProjectController>.Instance), user).DownloadHazardousAreaPlan(project.Id, default);
                Check(download is FileStreamResult {ContentType: "image/jpeg", EnableRangeProcessing: true}, "Assigned " + user.Role + " may view original file");
                ((FileStreamResult)download).FileStream.Dispose();
            }
            Check(await As(new ProjectController(db, storage, NullLogger<ProjectController>.Instance), outsider).DownloadHazardousAreaPlan(project.Id, default) is NotFoundResult, "Unassigned client cannot download plan");
            await controller.CreateProject(request with {JobNo = "PLAN-2"}, default);
            Check(db.Projects.Single(x => x.JobNo == "PLAN-2").HazardousAreaPlanPath is null, "Project creation without plan remains optional");
            var invalidCreate = await controller.CreateProjectWithPlan(request with {JobNo = "PLAN-3"}, File("bad.pdf", "application/pdf", jpeg), default);
            Check(invalidCreate.Result is BadRequestObjectResult && !db.Projects.Any(x => x.JobNo == "PLAN-3"), "Invalid upload does not create partial project");
            Console.WriteLine("All Hazardous Area Plan checks passed.");
        }
        finally {Directory.Delete(root, true);}
    }

    private static FormFile File(string name, string mime, byte[] bytes) => new(new MemoryStream(bytes), 0, bytes.Length, "hazardousAreaPlan", name) {Headers = new HeaderDictionary(), ContentType = mime};
    private static User User(string name, UserRole role) => new() {Email = name + "@plan.invalid", DisplayName = name, PasswordHash = "test", Role = role, ApprovalStatus = ApprovalStatus.Approved};
    private static T As<T>(T controller, User user) where T : ControllerBase {controller.ControllerContext = new() {HttpContext = new DefaultHttpContext {User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Role, user.Role.ToString())], "Test"))}}; return controller;}
    private static void Check(bool passed, string message) {if (!passed) throw new Exception(message); Console.WriteLine("PASS: " + message);}
    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string EnvironmentName {get; set;} = "Test";
        public string ApplicationName {get; set;} = "Test";
        public string WebRootPath {get; set;} = root;
        public IFileProvider WebRootFileProvider {get; set;} = new NullFileProvider();
        public string ContentRootPath {get; set;} = root;
        public IFileProvider ContentRootFileProvider {get; set;} = new NullFileProvider();
    }
}
