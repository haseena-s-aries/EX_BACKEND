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

    public static string Version(object value) => value is ChecklistState checklist
        ? checklist.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    /// <summary>One checkpoint of an equipment-specific checklist, in the browser bundle's point shape.</summary>
    public sealed record ChecklistPoint(string Code, string Section, int ItemNumber, string Text, string ProtectionApplicability);

    /// <summary>
    /// The saved equipment checklist as used by SaveCategorySelection's integer version check.
    /// Source is "equipment" (equipment selection), "project" (inherited project selection) or "template" (catalog default).
    /// </summary>
    public sealed record ChecklistState(Guid EquipmentId, int Version, string Source, IReadOnlyList<ChecklistPoint> Points);

    public static async Task<ChecklistState?> Checklist(SurveyDbContext db, Guid equipmentId, Guid projectId, CancellationToken ct)
    {
        if (!await db.AreaEquipment.AsNoTracking().AnyAsync(e => e.Id == equipmentId && e.ProjectId == projectId, ct)) return null;
        // Same keys as InspectionActivitiesController.SelectionKey; snapshots are append-only so history is preserved.
        var equipmentKey = $"CategorySelection/{projectId:D}/{equipmentId:D}";
        var projectKey = $"CategorySelection/{projectId:D}";
        var own = await db.ChecklistVersionSnapshots.AsNoTracking().Where(x => x.ProtectionTypeSnapshot == equipmentKey)
            .OrderByDescending(x => x.VersionNumber).Select(x => new {x.Id, x.VersionNumber}).FirstOrDefaultAsync(ct);
        var inherited = own is null ? await db.ChecklistVersionSnapshots.AsNoTracking().Where(x => x.ProtectionTypeSnapshot == projectKey)
            .OrderByDescending(x => x.VersionNumber).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct) : null;
        var snapshotId = own?.Id ?? inherited;
        var points = new List<ChecklistPoint>();
        if (snapshotId is not null)
            foreach (var x in await db.CheckpointDefinitionSnapshots.AsNoTracking()
                         .Where(x => x.ChecklistVersionSnapshotId == snapshotId).OrderBy(x => x.SortOrder)
                         .Select(x => new {x.QuestionCode, x.SectionCode, x.QuestionText, x.ApplicabilityCode}).ToListAsync(ct))
                points.Add(new ChecklistPoint(x.QuestionCode, x.SectionCode ?? "",
                    int.TryParse(x.QuestionCode.Split('/').LastOrDefault(), out var item) ? item : 0, x.QuestionText, x.ApplicabilityCode ?? ""));
        return new ChecklistState(equipmentId, own?.VersionNumber ?? 0, own is not null ? "equipment" : inherited is not null ? "project" : "template", points);
    }

    public static async Task<object?> Entity(SurveyDbContext db, string kind, Guid id, Guid projectId, CancellationToken ct)
    {
        if (kind == "equipment") return await db.AreaEquipment.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id && e.ProjectId == projectId, ct);
        if (kind == "defect") return await (from f in db.Findings.AsNoTracking()
            join e in db.AreaEquipment on f.RegisterEquipmentId equals e.Id
            where f.Id == id && e.ProjectId == projectId select f).SingleOrDefaultAsync(ct);
        if (kind == "checklist") return await Checklist(db, id, projectId, ct);
        if (kind == "activity") return await (from a in db.InspectionActivities.AsNoTracking()
            join t in db.InspectionTasks on a.InspectionTaskId equals t.Id
            join e in db.AreaEquipment on t.AreaEquipmentId equals (Guid?)e.Id
            where a.Id == id && e.ProjectId == projectId
            select new {a.Id, a.RowVersion, a.ReviewStatus, a.UpdatedAt}).SingleOrDefaultAsync(ct);
        return null;
    }
}
