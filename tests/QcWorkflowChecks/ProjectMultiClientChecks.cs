using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ProjectMultiClientChecks
{
    public static async Task Run()
    {
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        User U(string name, UserRole role) => new() {DisplayName = name, Email = name + "@test.invalid", PasswordHash = "test", Role = role, IsActive = true, ApprovalStatus = ApprovalStatus.Approved};
        var admin = U("admin", UserRole.Admin); var surveyor = U("surveyor", UserRole.Surveyor); var qc = U("qc", UserRole.Qc);
        var first = U("first", UserRole.Client); var second = U("second", UserRole.Client); var other = U("other", UserRole.Client);
        var inactive = U("inactive", UserRole.Client); inactive.IsActive = false;
        db.Users.AddRange(admin, surveyor, qc, first, second, other, inactive); await db.SaveChangesAsync();
        ClaimsPrincipal Actor(User u) => new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, u.Id.ToString()), new(ClaimTypes.Role, u.Role.ToString())], "Test"));
        ProjectController Controller(User u) {var c = new ProjectController(db, new FakePhotos(), NullLogger<ProjectController>.Instance); c.ControllerContext = new ControllerContext {HttpContext = new DefaultHttpContext {User = Actor(u)}}; return c;}
        var controller = Controller(admin); var ct = CancellationToken.None;
        var request = new UpsertProjectRequest("Multi-client", null, "MC-1", "Dubai", "Ship", surveyor.Id, qc.Id, ClientName: "  Acme Marine  ", TargetDate: ProjectScheduling.Today.AddDays(30), AssignedClientIds: [first.Id, second.Id, first.Id]);
        Check((await controller.CreateProject(request with {ClientName = "   "}, ct)).Result is BadRequestObjectResult, "Blank company details are rejected before any project is saved");
        Check(!db.Projects.Any(), "Rejected project leaves no records");
        foreach (var invalid in new[] {surveyor.Id, inactive.Id, Guid.NewGuid(), Guid.Empty})
            Check((await controller.CreateProject(request with {AssignedClientIds = [invalid]}, ct)).Result is BadRequestObjectResult, "Every Client ID must be an active approved Client");
        var created = (CreatedAtActionResult)(await controller.CreateProject(request, ct)).Result!;
        var response = (ProjectResponse)created.Value!; var id = response.Id;
        Check(response.ClientName == "ACME MARINE" && response.AssignedClientIds!.Count == 2, "Company details and deduplicated Client IDs are separate and saved");
        Check(response.AssignedClients!.Select(c => c.Id).ToHashSet().SetEquals([first.Id, second.Id]), "Create returns all assigned Client identities");
        Check(db.ProjectClientAssignments.Count() == 2, "Duplicate selection does not create duplicate assignment rows");
        foreach (var client in new[] {first, second})
        {
            Check((await Controller(client).GetProject(id, ct)).Result is OkObjectResult, "Each assigned Client can open the project");
            Check(await new ClientDashboardData(db, Actor(client)).Projects.AnyAsync(p => p.Id == id), "Each assigned Client sees the project in their dashboard");
            Check(await new ProjectReportData(db).AccessibleProjects(Actor(client)).AnyAsync(p => p.Id == id), "Each assigned Client can access scoped reports");
        }
        Check((await Controller(other).GetProject(id, ct)).Result is NotFoundObjectResult, "Unassigned Client cannot open project details");
        Check(!await new ClientDashboardData(db, Actor(other)).Projects.AnyAsync(), "Unassigned Client cannot see project on dashboard");
        var roundtrip = (ProjectResponse)((OkObjectResult)(await controller.GetProject(id, ct)).Result!).Value!;
        Check(roundtrip.AssignedClientIds!.Count == 2 && roundtrip.AssignedClients!.Count == 2, "Edit reload restores both IDs and names");
        await controller.UpdateProject(id, request with {AssignedClientIds = null, AssignedClientId = first.Id}, ct);
        Check(db.ProjectClientAssignments.Count(a => a.IsActive) == 2, "Legacy single-client payload does not erase additional Clients");
        await controller.UpdateProject(id, request with {AssignedClientIds = [second.Id]}, ct);
        Check((await Controller(first).GetProject(id, ct)).Result is NotFoundObjectResult, "Removing a Client revokes project access");
        Check(db.ProjectClientAssignments.Count() == 2 && !db.ProjectClientAssignments.Single(a => a.ClientUserId == first.Id).IsActive, "Removed assignment remains historical");
        await controller.UpdateProject(id, request, ct);
        Check(db.ProjectClientAssignments.Count() == 2 && db.ProjectClientAssignments.All(a => a.IsActive), "Reassigning reactivates existing link without duplicates");
        await controller.UpdateProject(id, request with {AssignedClientIds = null, AssignedClientId = null, ClearClientAssignments = true}, ct);
        Check(db.ProjectClientAssignments.All(a => !a.IsActive) && db.Projects.Single().AssignedClientId == null, "Explicit empty multipart assignment clears access without deleting history");
        Check(db.Projects.Single().AssignedSurveyorId == surveyor.Id && db.Projects.Single().AssignedQcId == qc.Id, "Surveyor and QC assignments are preserved");
        Check(db.AuditLogs.Any(a => a.Action == "Client Assignment"), "Client assignment changes retain audit history");
        var legacy = new Project {ProjectName = "Legacy", JobNo = "OLD", Sector = "", Location = "Dubai", VesselOrSiteName = "Site", AssignedSurveyorId = surveyor.Id, AssignedQcId = qc.Id, AssignedClientId = first.Id, CreatedByUserId = admin.Id};
        db.Projects.Add(legacy); await db.SaveChangesAsync();
        Check(await new ClientDashboardData(db, Actor(first)).Projects.AnyAsync(p => p.Id == legacy.Id), "Existing single-client relationship continues to authorize access");
        await using var sql = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ExSurveyMigrationDesignOnly;Integrated Security=True;TrustServerCertificate=True").Options);
        Check(new ProjectReportData(sql).AccessibleProjects(Actor(second)).ToQueryString().Contains("ProjectClientAssignments"), "Multi-client authorization translates to SQL Server");
        Console.WriteLine("All project multi-client checks passed.");
    }
    private static void Check(bool condition, string message) {if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message);}
}
