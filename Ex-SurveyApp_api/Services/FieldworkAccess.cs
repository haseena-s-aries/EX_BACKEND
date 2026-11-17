using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public static class FieldworkAccess
{
    public static IQueryable<Project> Projects(SurveyDbContext db, ClaimsPrincipal user)
    {
        var id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        // Fieldwork does not introduce additional rights for other roles.
        return db.Projects.Where(p => user.IsInRole("Surveyor") &&
            (p.AssignedSurveyorId == id || p.AdditionalSurveyorId == id));
    }

    public static string Version(object value) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    public static async Task<object?> Entity(SurveyDbContext db, string kind, Guid id, Guid projectId, CancellationToken ct)
    {
        if (kind == "equipment") return await db.AreaEquipment.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id && e.ProjectId == projectId, ct);
        if (kind == "defect") return await (from f in db.Findings.AsNoTracking()
            join e in db.AreaEquipment on f.RegisterEquipmentId equals e.Id
            where f.Id == id && e.ProjectId == projectId select f).SingleOrDefaultAsync(ct);
        if (kind == "activity") return await (from a in db.InspectionActivities.AsNoTracking()
            join t in db.InspectionTasks on a.InspectionTaskId equals t.Id
            join e in db.AreaEquipment on t.AreaEquipmentId equals (Guid?)e.Id
            where a.Id == id && e.ProjectId == projectId
            select new {a.Id, a.RowVersion, a.ReviewStatus, a.UpdatedAt}).SingleOrDefaultAsync(ct);
        return null;
    }
}
