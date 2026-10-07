using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ex_SurveyApp_api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

static class SqlAssignmentVerification
{
    public static async Task Run(bool repairSoleLink = false)
    {
        // Read-only check against the configured SQL database. Never print connection credentials.
        var root = Path.GetFullPath("Ex-SurveyApp_api");
        var config = new ConfigurationBuilder().SetBasePath(root)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables().Build();
        var connectionString = config.GetConnectionString("SurveyDatabase");
        if (string.IsNullOrWhiteSpace(connectionString)) {Console.WriteLine("SQL verification unavailable: no configured connection."); return;}
        var builder = new SqlConnectionStringBuilder(connectionString) {ConnectTimeout = 8};
        await using var connection = new SqlConnection(builder.ConnectionString);
        try {await connection.OpenAsync();}
        catch (SqlException ex) {Console.WriteLine($"SQL verification unavailable: connection failed (SQL error {ex.Number})."); return;}
        await using var schema = connection.CreateCommand();
        schema.CommandText = "SELECT CASE WHEN COL_LENGTH('dbo.Users', 'ManagerId') IS NOT NULL AND COL_LENGTH('dbo.Projects', 'AssignedQcId') IS NOT NULL THEN 1 ELSE 0 END";
        if (Convert.ToInt32(await schema.ExecuteScalarAsync()) != 1) {Console.WriteLine("SQL verification: required ManagerId/AssignedQcId columns are missing; apply the existing migration."); return;}
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer(builder.ConnectionString).Options);
        var managers = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Manager && u.IsActive && u.ApprovalStatus == ApprovalStatus.Approved)
            .Select(u => new {u.Id, u.DisplayName}).ToListAsync();
        foreach (var manager in managers) Console.WriteLine($"Available approved Manager: {manager.DisplayName}; id: {manager.Id}");
        var qcs = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Qc)
            .Select(u => new {u.Id, u.DisplayName, u.ManagerId, ManagerName = u.Manager == null ? null : u.Manager.DisplayName}).ToListAsync();
        if (repairSoleLink)
        {
            if (qcs.Count != 1 || managers.Count != 1 || qcs[0].ManagerId != null)
                throw new InvalidOperationException("Repair requires exactly one QC with no Manager and one approved Manager. No data changed.");
            var admin = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Role == UserRole.Admin && u.IsActive && u.ApprovalStatus == ApprovalStatus.Approved);
            if (admin is null) throw new InvalidOperationException("No active approved Admin available. No data changed.");
            var backup = Path.GetFullPath(Path.Combine("..", "backups", "qc-reporting-link-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            await File.WriteAllTextAsync(backup, JsonSerializer.Serialize(new {QcId = qcs[0].Id, PreviousManagerId = qcs[0].ManagerId, NewManagerId = managers[0].Id}, new JsonSerializerOptions {WriteIndented = true}));
            var controller = new ManagerQcReviewController(db, null!) {ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {User = new ClaimsPrincipal(new ClaimsIdentity([
                    new(ClaimTypes.NameIdentifier, admin.Id.ToString()), new(ClaimTypes.Role, "Admin")], "Local maintenance"))}}};
            var result = await controller.SetManager(qcs[0].Id, new SetReportingManagerRequest(managers[0].Id), CancellationToken.None);
            if (result is not NoContentResult) throw new InvalidOperationException("Existing reporting-manager action rejected repair.");
            Console.WriteLine($"Saved QC reporting link using existing Admin action. Previous value backup: {backup}");
            db.ChangeTracker.Clear();
            qcs = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Qc)
                .Select(u => new {u.Id, u.DisplayName, u.ManagerId, ManagerName = u.Manager == null ? null : u.Manager.DisplayName}).ToListAsync();
        }
        Console.WriteLine($"SQL columns verified. QC users: {qcs.Count}; saved reporting relationships: {qcs.Count(q => q.ManagerId != null)}.");
        foreach (var qc in qcs) Console.WriteLine($"QC: {qc.DisplayName}; saved Manager: {qc.ManagerName ?? "NOT ASSIGNED"}; QC id: {qc.Id}; Manager id: {qc.ManagerId}");
        var assignments = await db.Projects.AsNoTracking().Select(p => new {
            p.Id, p.ProjectName, p.AssignedQcId, QcName = p.AssignedQc.DisplayName,
            p.AssignedQc.ManagerId, ManagerName = p.AssignedQc.Manager == null ? null : p.AssignedQc.Manager.DisplayName,
        }).ToListAsync();
        Console.WriteLine($"SQL AssignedQc/Manager EF navigation verified for {assignments.Count} projects.");
        foreach (var p in assignments) Console.WriteLine($"Project: {p.ProjectName}; assigned QC: {p.QcName} ({p.AssignedQcId}); Manager: {p.ManagerName ?? "NOT ASSIGNED"} ({p.ManagerId})");
        var scopedSubmitted = await (from s in db.SurveySessions.AsNoTracking()
            join e in db.AreaEquipment.AsNoTracking() on s.AreaEquipmentId equals e.Id
            join p in db.Projects.AsNoTracking() on e.ProjectId equals p.Id
            where s.SubmittedAt != null select new {p.Id, p.AssignedQcId, p.AssignedQc.ManagerId}).ToListAsync();
        Console.WriteLine($"Saved submitted inspections: {scopedSubmitted.Count}; with a QC reporting Manager: {scopedSubmitted.Count(s => s.ManagerId != null)}.");
        foreach (var manager in managers)
        {
            var controller = new QcController(db, null!) {ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {User = new ClaimsPrincipal(new ClaimsIdentity([
                    new(ClaimTypes.NameIdentifier, manager.Id.ToString()), new(ClaimTypes.Role, "Manager")], "SQL verification"))}}};
            var result = await controller.GetProjects(CancellationToken.None, pendingOnly: true);
            var projects = (IEnumerable<QcProjectSummaryResponse>)((OkObjectResult)result.Result!).Value!;
            Console.WriteLine($"Actual SQL QC Pending projects for Manager {manager.DisplayName}: {string.Join(", ", projects.Select(p => p.ProjectName))}");
        }
        foreach (var qc in qcs)
        {
            var controller = new QcController(db, null!) {ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {User = new ClaimsPrincipal(new ClaimsIdentity([
                    new(ClaimTypes.NameIdentifier, qc.Id.ToString()), new(ClaimTypes.Role, "Qc")], "SQL verification"))}}};
            var result = await controller.GetProjects(CancellationToken.None, pendingOnly: true);
            var projects = (IEnumerable<QcProjectSummaryResponse>)((OkObjectResult)result.Result!).Value!;
            Console.WriteLine($"Actual SQL QC Pending projects for QC {qc.DisplayName}: {string.Join(", ", projects.Select(p => p.ProjectName))}");
        }
        Console.WriteLine(repairSoleLink ? "SQL reporting repair and verification complete." : "Read-only SQL verification complete. No database records changed.");
    }
}
