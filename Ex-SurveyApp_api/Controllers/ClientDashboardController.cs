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
[Authorize(Roles = "Client")]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
[Route("api/client")]
public class ClientDashboardController(SurveyDbContext db, IProjectPhotoStorage storage) : ControllerBase
{
    private ClientDashboardData Data => new(db, User);

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var projects = await Data.Summaries(ct);
        var surveysInProgress = await (from session in db.SurveySessions.AsNoTracking()
            join equipment in db.AreaEquipment.AsNoTracking() on session.AreaEquipmentId equals equipment.Id
            join project in Data.Projects on equipment.ProjectId equals project.Id
            where project.Status != ProjectStatus.Completed && session.Status != SurveyStatus.Approved
            select project.Id).Distinct().CountAsync(ct);
        return Ok(new {Counts = new ClientDashboardCounts(
            projects.Count(p => p.ProjectStatus is ProjectStatus.Active or ProjectStatus.InProgress),
            surveysInProgress,
            projects.Sum(p => p.Progress.AwaitingQc), projects.Sum(p => p.Progress.QcApproved),
            projects.Count(p => p.ProjectStatus == ProjectStatus.Completed), projects.Sum(p => p.Progress.OpenDefects),
            projects.Sum(p => p.Progress.ClosedDefects), projects.Count), Projects = projects, RecentActivity = await Data.Activity(ct)});
    }

    [HttpGet("projects")]
    public async Task<IActionResult> Projects(CancellationToken ct) => Ok(await Data.Summaries(ct));

    [HttpGet("projects/{projectId:guid}")]
    public async Task<IActionResult> Project(Guid projectId, CancellationToken ct)
    {
        if (!await Data.HasProject(projectId, ct)) return Forbid();
        var report = await new ProjectReportData(db).Load(projectId, User, ct);
        return Ok(new {Project = (await Data.Summaries(ct)).Single(p => p.ProjectId == projectId), Report = report});
    }

    [HttpGet("projects/{projectId:guid}/areas")]
    public async Task<IActionResult> Areas(Guid projectId, CancellationToken ct)
    {
        if (!await Data.HasProject(projectId, ct)) return Forbid();
        return Ok((await new ProjectReportData(db).Load(projectId, User, ct))!.Areas);
    }

    [HttpGet("areas/{areaId:guid}/equipment")]
    public async Task<IActionResult> EquipmentInArea(Guid areaId, CancellationToken ct)
    {
        var area = await db.ProjectAreas.AsNoTracking().SingleOrDefaultAsync(a => a.Id == areaId, ct);
        if (area is null) return NotFound();
        if (!await Data.HasProject(area.ProjectId, ct)) return Forbid();
        return Ok((await new ProjectReportData(db).Load(area.ProjectId, User, ct))!.Areas.Single(a => a.Id == areaId));
    }

    [HttpGet("equipment/{equipmentId:guid}")]
    public async Task<IActionResult> Equipment(Guid equipmentId, CancellationToken ct)
    {
        var pin = await db.AreaEquipment.AsNoTracking().SingleOrDefaultAsync(e => e.Id == equipmentId, ct);
        if (pin is null) return NotFound();
        if (!await Data.HasProject(pin.ProjectId, ct)) return Forbid();
        var report = (await new ProjectReportData(db).Load(pin.ProjectId, User, ct))!;
        var equipment = report.Areas.Single(a => a.Id == pin.AreaId).Equipment.Single(e => e.Id == equipmentId);
        var sessionIds = equipment.Inspections.Select(s => s.Id).ToArray();
        var history = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == QcWorkflowHistory.EntityType && sessionIds.Contains(a.EntityId))
            .OrderBy(a => a.OccurredAt).Select(a => new {a.Id, a.Action, a.Comments, a.Severity, a.QcStatus, a.OccurredAt, a.ReviewStartedAt, a.ReviewedAt}).ToListAsync(ct);
        var certificates = await db.Files.AsNoTracking().Where(f => f.Category == FileCategory.Certificate &&
            f.SurveySessionId.HasValue && sessionIds.Contains(f.SurveySessionId.Value))
            .Select(f => new {f.Id, f.FileName, f.ContentType, Url = $"/api/client/files/{f.Id}"}).ToListAsync(ct);
        return Ok(new {ProjectId = pin.ProjectId, AreaId = pin.AreaId, Equipment = equipment, History = history, Certificates = certificates});
    }

    [HttpGet("defects")]
    [HttpGet("projects/{projectId:guid}/defects")]
    public async Task<IActionResult> Defects(CancellationToken ct, Guid? projectId = null)
    {
        if (projectId.HasValue && !await Data.HasProject(projectId.Value, ct)) return Forbid();
        var projects = Data.Projects.Where(p => !projectId.HasValue || p.Id == projectId);
        var rows = await (from f in db.Findings.AsNoTracking()
            join session in db.SurveySessions.AsNoTracking() on f.SurveySessionId equals session.Id into sessionGroup
            from s in sessionGroup.DefaultIfEmpty()
            join e in db.AreaEquipment.AsNoTracking() on (f.RegisterEquipmentId ?? (s == null ? null : s.AreaEquipmentId)) equals (Guid?)e.Id
            join a in db.ProjectAreas.AsNoTracking() on e.AreaId equals a.Id
            join p in projects on e.ProjectId equals p.Id
            select new {Finding = f, ProjectId = p.Id, p.ProjectName, EquipmentId = e.Id, e.EquipmentCode, e.TagNumber,
                e.Type, EquipmentDescription = e.Description ?? e.EquipmentName ?? e.Type, AreaId = a.Id, a.AreaName,
                SurveyorRemarks = s == null ? null : s.SurveyorRemarks,
                InspectionDate = s == null ? (DateTimeOffset?)null : s.InspectionTimestamp ?? s.CreatedAt,
                DueAt = s == null ? (DateTimeOffset?)null : s.DueDate,
                Grade = f.RegisterGrade ?? (s == null ? null : db.ChecklistTemplates.Where(t => t.Id == s.ChecklistTemplateId).Select(t => (InspectionType?)t.InspectionType).FirstOrDefault()),
                QcRemarks = s == null ? null : db.QcReviews.Where(q => q.SurveySessionId == s.Id).Select(q => q.Comments).FirstOrDefault()}).ToListAsync(ct);
        var ids = rows.Select(r => r.Finding.Id).ToArray();
        var photos = await db.Files.AsNoTracking().Where(f => f.FindingId.HasValue && ids.Contains(f.FindingId.Value) &&
            (f.Category == FileCategory.FindingPhoto || f.Category == FileCategory.RepairProof) && (f.ContentType.StartsWith("image/") || f.ContentType == "application/pdf")).ToListAsync(ct);
        object Photo(SurveyFile f) => new {f.Id, f.FileName, Url = $"/api/client/files/{f.Id}"};
        return Ok(rows.Select(r => new {Id = r.Finding.Id, r.ProjectId, r.ProjectName, r.EquipmentId, r.EquipmentCode, r.TagNumber,
            r.Type, r.EquipmentDescription, r.AreaId, r.AreaName, r.Finding.Observation, r.Finding.Recommendation, r.Finding.Severity, r.Finding.Status,
            r.Finding.RegisterDate, r.Finding.AosdId, r.Finding.FaultCategory, r.Finding.FaultCode, r.Finding.Remarks, r.Finding.AdditionalRemarks,
            r.InspectionDate, r.DueAt, r.Grade,
            r.SurveyorRemarks, r.QcRemarks, BeforePhotos = photos.Where(f => f.FindingId == r.Finding.Id && f.Category == FileCategory.FindingPhoto).Select(Photo),
            AfterPhotos = photos.Where(f => f.FindingId == r.Finding.Id && f.Category == FileCategory.RepairProof).Select(Photo)}));
    }

    [HttpGet("files/{fileId:guid}")]
    public async Task<IActionResult> FileContent(Guid fileId, CancellationToken ct)
    {
        var file = await db.Files.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fileId, ct);
        if (file is null) return NotFound();
        var sessionId = file.SurveySessionId;
        if (!sessionId.HasValue && file.FindingId.HasValue) sessionId = await db.Findings.Where(f => f.Id == file.FindingId).Select(f => (Guid?)f.SurveySessionId).SingleOrDefaultAsync(ct);
        var projectId = await (from s in db.SurveySessions.AsNoTracking() join e in db.AreaEquipment.AsNoTracking() on s.AreaEquipmentId equals e.Id
            where s.Id == sessionId select (Guid?)e.ProjectId).SingleOrDefaultAsync(ct);
        if (!projectId.HasValue && file.FindingId.HasValue)
            projectId = await (from f in db.Findings.AsNoTracking() join e in db.AreaEquipment.AsNoTracking() on f.RegisterEquipmentId equals e.Id
                where f.Id == file.FindingId select (Guid?)e.ProjectId).SingleOrDefaultAsync(ct);
        if (!projectId.HasValue || !await Data.HasProject(projectId.Value, ct)) return Forbid();
        if (file.Category is not (FileCategory.EquipmentPhoto or FileCategory.FindingPhoto or FileCategory.RepairProof or FileCategory.Certificate)) return Forbid();
        var content = await storage.OpenReadAsync(file.StorageKey, ct);
        if (content is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content.Stream, content.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("reports")]
    [HttpGet("projects/{projectId:guid}/reports")]
    public async Task<IActionResult> Reports(CancellationToken ct, Guid? projectId = null)
    {
        if (projectId.HasValue && !await Data.HasProject(projectId.Value, ct)) return Forbid();
        var summaries = (await Data.Summaries(ct)).Where(p => !projectId.HasValue || p.ProjectId == projectId).ToArray();
        var projectIds = summaries.Select(p => p.ProjectId).ToArray();
        var statuses = await db.ProjectReportWorkflows.AsNoTracking().Where(w => projectIds.Contains(w.ProjectId)).ToDictionaryAsync(w => w.ProjectId, w => w.Status, ct);
        return Ok(summaries.Select(p => new {
            p.ProjectId, p.ProjectName, p.DocumentNumber, p.Revision, p.GeneratedDate,
            ReportStatus = statuses.GetValueOrDefault(p.ProjectId, "Ongoing"),
            Availability = p.GeneratedDate.HasValue ? "Generated / available on demand" : "Available on demand",
            PdfUrl = $"/api/reports/projects/{p.ProjectId}/pdf"}));
    }

    [HttpGet("activity")]
    public async Task<IActionResult> Activity(CancellationToken ct) => Ok(await Data.Activity(ct));

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken ct) {
        var updates = (await Data.Activity(ct))
            .Where(a => a.Action is "Surveyor Submitted" or "Surveyor Resubmitted" or "Survey Completed" or "QC Approved" or "Defect Updated" or "Defect Closed" or "Report Generated" or "Report Revision Available")
            .Select(a => new {Id = a.Id.ToString(), a.ProjectId, Title = a.Action, Message = $"{a.ProjectName}{(a.Detail is null ? "" : ": " + a.Detail)}", Time = a.OccurredAt});
        var assignments = (await ProjectAssignmentNotices.Read(db, Data.Projects, Data.UserId, ct))
            .Select(a => new {Id = a.Id.ToString(), a.ProjectId, a.Title, a.Message, a.Time});
        return Ok(updates.Concat(assignments).OrderByDescending(n => n.Time));
    }

    [HttpGet("projects/{projectId:guid}/remarks")]
    public async Task<IActionResult> Remarks(Guid projectId, CancellationToken ct)
    {
        if (!await Data.HasProject(projectId, ct)) return Forbid();
        return Ok(await db.ReportClientRemarks.AsNoTracking().Where(r => r.ProjectId == projectId && r.ClientUserId == Data.UserId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct));
    }

    [HttpPost("remarks")]
    public async Task<IActionResult> AddRemark(AddClientRemarkRequest request, CancellationToken ct)
    {
        if (!await Data.HasProject(request.ProjectId, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Remark) || request.Remark.Length > 4000) return BadRequest(new {error = "Enter a remark of 1–4000 characters."});
        if (request.EquipmentId.HasValue && !await db.AreaEquipment.AnyAsync(e => e.Id == request.EquipmentId && e.ProjectId == request.ProjectId, ct)) return Forbid();
        if (request.FindingId.HasValue && !await (from f in db.Findings join session in db.SurveySessions on f.SurveySessionId equals session.Id into sessionGroup
            from s in sessionGroup.DefaultIfEmpty()
            join e in db.AreaEquipment on (f.RegisterEquipmentId ?? (s == null ? null : s.AreaEquipmentId)) equals (Guid?)e.Id where f.Id == request.FindingId && e.ProjectId == request.ProjectId &&
            (!request.EquipmentId.HasValue || e.Id == request.EquipmentId) select f.Id).AnyAsync(ct)) return Forbid();
        if (request.ReportId.HasValue && !await (from r in db.ReportRevisions join s in db.SurveySessions on r.JobId equals s.JobId
            join e in db.AreaEquipment on s.AreaEquipmentId equals e.Id where r.Id == request.ReportId && e.ProjectId == request.ProjectId select r.Id).AnyAsync(ct)) return Forbid();
        var remark = new ReportClientRemark {ClientUserId = Data.UserId, ProjectId = request.ProjectId, EquipmentId = request.EquipmentId,
            FindingId = request.FindingId, ReportId = request.ReportId, AgainstReport = request.AgainstReport || request.ReportId.HasValue, Remark = request.Remark.Trim()};
        db.ReportClientRemarks.Add(remark); await db.SaveChangesAsync(ct);
        return StatusCode(201, remark);
    }
}
