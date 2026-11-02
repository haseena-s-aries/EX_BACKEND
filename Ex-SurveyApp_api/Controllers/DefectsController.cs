using System.Security.Claims;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Manager")]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
[Route("api/defects")]
public class DefectsController(SurveyDbContext db, IProjectPhotoStorage storage) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private IQueryable<Project> RelevantProjects() => User.IsInRole("Admin")
        ? db.Projects.AsNoTracking()
        : db.Projects.AsNoTracking().Where(p => p.CreatedByUserId == UserId ||
            db.Users.Any(q => q.Id == p.AssignedQcId && q.ManagerId == UserId));

    private IQueryable<DefectRow> Rows() =>
        from finding in db.Findings.AsNoTracking()
        join inspection in db.SurveySessions.AsNoTracking() on finding.SurveySessionId equals inspection.Id
        join equipment in db.AreaEquipment.AsNoTracking() on inspection.AreaEquipmentId equals equipment.Id
        join project in RelevantProjects() on equipment.ProjectId equals project.Id
        join area in db.ProjectAreas.AsNoTracking() on equipment.AreaId equals area.Id
        join surveyor in db.Users.AsNoTracking() on inspection.SurveyorId equals surveyor.Id
        select new DefectRow {
            Id = finding.Id, InspectionId = inspection.Id, ProjectId = project.Id,
            ProjectName = project.ProjectName, AreaName = area.AreaName, TagNumber = equipment.TagNumber,
            Observation = finding.Observation, Recommendation = finding.Recommendation,
            Severity = finding.Severity, Status = finding.Status, ReportedBy = surveyor.DisplayName,
            InspectionDate = inspection.InspectionTimestamp ?? inspection.CreatedAt, InspectionDueDate = inspection.DueDate
        };

    [HttpGet]
    public async Task<ActionResult<DefectsResponse>> GetDefects(CancellationToken ct, [FromQuery] bool openOnly = true)
    {
        var all = await Rows().OrderByDescending(x => x.InspectionDate).ThenBy(x => x.Id).ToListAsync(ct);
        var open = all.Where(x => x.Status is FindingStatus.Open or FindingStatus.RectificationSubmitted).ToArray();
        var summary = new DefectSummary(open.Length,
            open.Count(x => Severity(x.Severity) == "High"), open.Count(x => Severity(x.Severity) == "Medium"),
            open.Count(x => Severity(x.Severity) == "Low"), open.Count(x => Severity(x.Severity) == "Unspecified"));
        var shown = openOnly ? open : all.ToArray();
        var ids = shown.Select(x => x.Id).ToArray();
        var photos = await db.Files.AsNoTracking().Where(f => f.FindingId.HasValue && ids.Contains(f.FindingId.Value) &&
            f.Category == FileCategory.FindingPhoto && f.ContentType.StartsWith("image/"))
            .Select(f => new {f.Id, f.FindingId, f.FileName}).ToListAsync(ct);
        return Ok(new DefectsResponse(summary, shown.Select(row => row with {
            Severity = Severity(row.Severity), Photos = photos.Where(f => f.FindingId == row.Id)
                .Select(f => new DefectPhoto(f.Id, f.FileName, $"/api/defects/{row.Id}/photos/{f.Id}")).ToArray()
        }).ToArray()));
    }

    [HttpGet("{findingId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> Photo(Guid findingId, Guid photoId, CancellationToken ct)
    {
        if (!await Rows().AnyAsync(x => x.Id == findingId, ct)) return NotFound();
        var file = await db.Files.AsNoTracking().SingleOrDefaultAsync(f => f.Id == photoId && f.FindingId == findingId &&
            f.Category == FileCategory.FindingPhoto && f.ContentType.StartsWith("image/"), ct);
        if (file is null) return NotFound();
        var content = await storage.OpenReadAsync(file.StorageKey, ct);
        if (content is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(content.Stream, content.ContentType, enableRangeProcessing: true);
    }

    private static string Severity(string? value) => value?.Trim().ToLowerInvariant() switch {
        "high" => "High", "medium" => "Medium", "low" => "Low", _ => "Unspecified"
    };
}

public sealed record DefectSummary(int OpenCount, int High, int Medium, int Low, int Unspecified);
public sealed record DefectPhoto(Guid Id, string FileName, string DownloadUrl);
public sealed record DefectsResponse(DefectSummary Summary, IReadOnlyList<DefectRow> Items);
public sealed record DefectRow
{
    public Guid Id { get; init; }
    public Guid InspectionId { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = "";
    public string AreaName { get; init; } = "";
    public string TagNumber { get; init; } = "";
    public string Observation { get; init; } = "";
    public string? Recommendation { get; init; }
    public string? Severity { get; init; }
    public FindingStatus Status { get; init; }
    public string ReportedBy { get; init; } = "";
    public DateTimeOffset InspectionDate { get; init; }
    public DateTimeOffset? InspectionDueDate { get; init; }
    public IReadOnlyList<DefectPhoto> Photos { get; init; } = [];
}
