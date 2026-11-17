using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Manager")]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
[Route("api/manager/qc-review")]
public class ManagerQcReviewController(SurveyDbContext db, IProjectPhotoStorage photos) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private IQueryable<Project> RelevantProjects() => User.IsInRole("Admin")
        ? db.Projects.AsNoTracking()
        : db.Projects.AsNoTracking().Where(p => p.CreatedByUserId == UserId ||
            db.Users.Any(q => q.Id == p.AssignedQcId && q.ManagerId == UserId));

    // Monitoring only. All mutations remain in the original surveyor/QC controllers.
    [HttpGet("dashboard")]
    public async Task<ActionResult<ManagerQcCounts>> Dashboard(CancellationToken ct) =>
        Ok(Count(await LoadRows(await RelevantProjects().Select(p => p.Id).ToArrayAsync(ct), ct)));

    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<ManagerQcProject>>> Projects(CancellationToken ct)
    {
        var projects = await RelevantProjects().OrderByDescending(p => p.UpdatedAt).ToListAsync(ct);
        var rows = await LoadRows(projects.Select(p => p.Id).ToArray(), ct);
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, ct);
        return Ok(projects.Select(p => Summary(p, rows.Where(r => r.ProjectId == p.Id).ToArray(), users)));
    }

    [HttpGet("projects/{projectId:guid}")]
    public async Task<ActionResult<ManagerQcProjectDetails>> Project(Guid projectId, CancellationToken ct)
    {
        var project = await RelevantProjects().SingleOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return NotFound();
        var rows = await LoadRows([projectId], ct);
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, ct);
        var areas = await db.ProjectAreas.AsNoTracking().Where(a => a.ProjectId == projectId)
            .OrderBy(a => a.CreatedAt).ToListAsync(ct);
        return Ok(new ManagerQcProjectDetails(Summary(project, rows, users), areas.Select(a =>
        {
            var equipment = rows.Where(r => r.AreaId == a.Id).ToArray();
            return new ManagerQcArea(a.Id, a.AreaName, equipment.Length, Count(equipment));
        }).ToArray()));
    }

    [HttpGet("projects/{projectId:guid}/areas/{areaId:guid}")]
    public async Task<ActionResult<ManagerQcAreaDetails>> Area(Guid projectId, Guid areaId, CancellationToken ct)
    {
        if (!await RelevantProjects().AnyAsync(p => p.Id == projectId, ct)) return NotFound();
        var area = await db.ProjectAreas.AsNoTracking().SingleOrDefaultAsync(a => a.Id == areaId && a.ProjectId == projectId, ct);
        if (area is null) return NotFound();
        var rows = (await LoadRows([projectId], ct)).Where(r => r.AreaId == areaId).OrderBy(r => r.Equipment.SequenceNo).ToArray();
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, ct);
        return Ok(new ManagerQcAreaDetails(area.Id, area.AreaName, Count(rows), rows.Select(r => Equipment(r, users)).ToArray()));
    }

    [HttpGet("equipment/{equipmentId:guid}")]
    public async Task<ActionResult<ManagerQcInspection>> Inspection(Guid equipmentId, CancellationToken ct, [FromQuery] Guid? inspectionId = null)
    {
        var equipment = await db.AreaEquipment.AsNoTracking().SingleOrDefaultAsync(e => e.Id == equipmentId, ct);
        if (equipment is null) return NotFound();
        var project = await RelevantProjects().SingleOrDefaultAsync(p => p.Id == equipment.ProjectId, ct);
        if (project is null) return NotFound();
        var row = (await LoadRows([project.Id], ct)).Where(r => r.Equipment.Id == equipmentId &&
            (!inspectionId.HasValue || r.Inspection?.Id == inspectionId))
            .OrderByDescending(r => r.Inspection?.CreatedAt).FirstOrDefault();
        if (row is null) return NotFound();
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, ct);
        var history = row.Inspection is null ? [] : await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == QcWorkflowHistory.EntityType && a.EntityId == row.Inspection.Id)
            .OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).ToListAsync(ct);
        var detail = row.Inspection is null ? null : await new QcInspectionReader(db)
            .BuildInspectionResponse(row.Inspection, equipment, project, ct);
        var grade = row.Inspection?.ChecklistTemplateId is Guid templateId
            ? await db.ChecklistTemplates.AsNoTracking().Where(t => t.Id == templateId)
                .Select(t => (InspectionType?)t.InspectionType).SingleOrDefaultAsync(ct) : null;
        var areaName = await db.ProjectAreas.AsNoTracking().Where(a => a.Id == equipment.AreaId)
            .Select(a => a.AreaName).SingleOrDefaultAsync(ct);
        return Ok(new ManagerQcInspection(Equipment(row, users), detail, history.Select(a =>
            new ManagerQcHistory(a.Id, a.Action, a.OccurredAt,
                a.ActorId.HasValue ? users.GetValueOrDefault(a.ActorId.Value)?.DisplayName : null,
                a.Comments, a.Severity, a.QcStatus, a.ReviewStartedAt, a.ReviewedAt)).ToArray(),
            grade?.ToString(), equipment.EquipmentName, equipment.Description, equipment.EquipmentCode,
            areaName, equipment.RfidTagUid));
    }

    [HttpGet("projects/{projectId:guid}/records")]
    public async Task<IActionResult> Records(Guid projectId, CancellationToken ct)
    {
        if (!await RelevantProjects().AnyAsync(p => p.Id == projectId, ct)) return NotFound();
        var rows = await LoadRows([projectId], ct);
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, ct);
        var areas = await db.ProjectAreas.AsNoTracking().Where(a => a.ProjectId == projectId)
            .ToDictionaryAsync(a => a.Id, a => a.AreaName, ct);
        var templates = await db.ChecklistTemplates.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        var inspectionIds = rows.Where(r => r.Inspection != null).Select(r => r.Inspection!.Id).ToArray();
        var reviewedIds = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == QcWorkflowHistory.EntityType && inspectionIds.Contains(a.EntityId) &&
            (a.Action == "QC Started Review" || a.Action == "QC Approved" || a.Action == "QC Requested Changes"))
            .Select(a => a.EntityId).Distinct().ToListAsync(ct);
        return Ok(rows.Where(r => r.Inspection != null &&
            (r.Inspection.SubmittedAt != null || r.Review != null || r.LastActivity != null))
            .DistinctBy(r => r.Inspection!.Id).Select(r => new
            {
                r.ProjectId, r.AreaId, AreaName = areas.GetValueOrDefault(r.AreaId),
                AreaTotalEquipment = rows.Where(a => a.AreaId == r.AreaId).Select(a => a.Equipment.Id).Distinct().Count(),
                Equipment = Equipment(r, users), ItemNumber = r.Equipment.EquipmentCode,
                r.Equipment.EquipmentName, r.Equipment.Description, r.Equipment.RfidTagUid,
                InspectionDate = r.Inspection!.InspectionTimestamp ?? r.Inspection.SubmittedAt,
                InspectionType = r.Inspection.ChecklistTemplateId is Guid tid
                    ? templates.GetValueOrDefault(tid)?.InspectionType.ToString() : null,
                InspectorName = r.Inspection.InspectorName ?? users.GetValueOrDefault(r.Inspection.SurveyorId)?.DisplayName,
                r.Inspection.SubmittedAt,
                HasReview = r.Review?.StartedAt != null || r.Review?.ReviewedAt != null ||
                    (r.Review != null && r.Review.Status != QcReviewStatus.PendingReview) || reviewedIds.Contains(r.Inspection.Id),
                LastQcActivity = r.LastActivity
            }));
    }

    [HttpGet("inspections/{inspectionId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> Photo(Guid inspectionId, Guid photoId, CancellationToken ct)
    {
        var allowed = await (from s in db.SurveySessions.AsNoTracking()
            join e in db.AreaEquipment.AsNoTracking() on s.AreaEquipmentId equals e.Id
            join p in RelevantProjects() on e.ProjectId equals p.Id
            where s.Id == inspectionId select s.Id).AnyAsync(ct);
        if (!allowed) return NotFound();
        var file = await db.Files.AsNoTracking().SingleOrDefaultAsync(f => f.Id == photoId &&
            f.SurveySessionId == inspectionId && (f.Category == FileCategory.EquipmentPhoto || f.Category == FileCategory.FindingPhoto), ct);
        if (file is null) return NotFound();
        var content = await photos.OpenReadAsync(file.StorageKey, ct);
        return content is null ? NotFound() : File(content.Stream, content.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("reporting-managers")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReportingManagers(CancellationToken ct) => Ok(await db.Users.AsNoTracking()
        .Where(u => u.Role == UserRole.Manager && u.IsActive && u.ApprovalStatus == ApprovalStatus.Approved)
        .OrderBy(u => u.DisplayName).Select(u => new { u.Id, u.DisplayName }).ToListAsync(ct));

    [HttpPut("users/{qcId:guid}/manager")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetManager(Guid qcId, SetReportingManagerRequest request, CancellationToken ct)
    {
        var qc = await db.Users.SingleOrDefaultAsync(u => u.Id == qcId && u.Role == UserRole.Qc && u.IsActive, ct);
        if (qc is null) return NotFound();
        if (request.ManagerId.HasValue && !await db.Users.AnyAsync(u => u.Id == request.ManagerId &&
                u.Role == UserRole.Manager && u.IsActive && u.ApprovalStatus == ApprovalStatus.Approved, ct))
            return BadRequest(new { error = "Select an active, approved Manager." });
        qc.ManagerId = request.ManagerId;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<List<Row>> LoadRows(Guid[] projectIds, CancellationToken ct) => ReviewRows(projectIds).ToListAsync(ct);

    private IQueryable<Row> ReviewRows(Guid[] projectIds) =>
        from e in db.AreaEquipment.AsNoTracking()
        where projectIds.Contains(e.ProjectId)
        join s in db.SurveySessions.AsNoTracking() on e.Id equals s.AreaEquipmentId into sessions
        from s in sessions.DefaultIfEmpty()
        join q in db.QcReviews.AsNoTracking() on s.Id equals q.SurveySessionId into reviews
        from q in reviews.DefaultIfEmpty()
        select new Row(e.ProjectId, e.AreaId, e, s, q,
            db.AuditLogs.Where(a => a.EntityType == QcWorkflowHistory.EntityType && a.EntityId == s.Id)
                .Select(a => (DateTimeOffset?)a.OccurredAt).Max());

    private static ManagerQcCounts Count(IEnumerable<Row> rows)
    {
        var submitted = rows.Where(r => r.Inspection?.SubmittedAt != null).ToArray();
        var now = DateTimeOffset.UtcNow;
        return new(submitted.Count(r => r.Status == QcReviewStatus.PendingReview),
            submitted.Count(r => r.Status == QcReviewStatus.InReview),
            submitted.Count(r => r.Status == QcReviewStatus.ChangesRequested),
            submitted.Count(r => r.Status == QcReviewStatus.Approved),
            submitted.Count(r => r.Inspection!.DueDate < now && r.Status != QcReviewStatus.Approved));
    }

    private static ManagerQcProject Summary(Project p, IReadOnlyCollection<Row> rows, Dictionary<Guid, User> users)
    {
        var qc = users.GetValueOrDefault(p.AssignedQcId);
        var statuses = rows.Where(r => r.Inspection?.SubmittedAt != null).Select(r => r.Status).ToArray();
        var status = QcWorkflowStatus.Aggregate(statuses);
        return new(p.Id, p.ProjectName, p.JobNo, p.Location,
            users.GetValueOrDefault(p.AssignedSurveyorId)?.DisplayName ?? "", qc?.DisplayName ?? "",
            qc?.ManagerId, qc?.ManagerId is Guid id ? users.GetValueOrDefault(id)?.DisplayName : null,
            p.Status, status, rows.Count, Count(rows),
            rows.Select(r => r.LastActivity ?? r.Review?.ReviewedAt ?? r.Review?.StartedAt).Max());
    }

    private static ManagerQcEquipment Equipment(Row row, Dictionary<Guid, User> users)
    {
        var s = row.Inspection; var q = row.Review;
        var status = QcWorkflowStatus.InspectionStatus(s?.Status, q?.Status);
        return new(row.Equipment.Id, s?.Id, row.Equipment.SequenceNo, row.Equipment.TagNumber, row.Equipment.Type,
            status, s?.SubmittedAt is null ? null : row.Status, q?.Comments, q?.Severity,
            q is null ? null : users.GetValueOrDefault(q.ReviewedByUserId)?.DisplayName, q?.StartedAt, q?.ReviewedAt);
    }
    private sealed record Row(Guid ProjectId, Guid AreaId, AreaEquipment Equipment, SurveySession? Inspection, QcReview? Review, DateTimeOffset? LastActivity)
    {
        public QcReviewStatus Status => Review?.Status ?? QcReviewStatus.PendingReview;
    }
}
