using System.Security.Claims;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;
namespace Ex_SurveyApp_api.Services;

public sealed class ClientDashboardData(SurveyDbContext db, ClaimsPrincipal user)
{
    public Guid UserId => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    // Reuse the existing report access policy, then require the Client assignment.
    public IQueryable<Project> Projects => new ProjectReportData(db).AccessibleProjects(user).Where(p => p.AssignedClientId == UserId);
    public Task<bool> HasProject(Guid id, CancellationToken ct) => Projects.AnyAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<ClientProjectSummary>> Summaries(CancellationToken ct)
    {
        var projects = await Projects.OrderByDescending(p => p.UpdatedAt).ToListAsync(ct);
        var ids = projects.Select(p => p.Id).ToArray();
        var areas = await db.ProjectAreas.AsNoTracking().Where(a => ids.Contains(a.ProjectId)).ToListAsync(ct);
        var equipment = await db.AreaEquipment.AsNoTracking().Where(e => ids.Contains(e.ProjectId)).ToListAsync(ct);
        var equipmentIds = equipment.Select(e => e.Id).ToArray();
        var sessions = await db.SurveySessions.AsNoTracking().Where(s => s.AreaEquipmentId.HasValue && equipmentIds.Contains(s.AreaEquipmentId.Value)).ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToArray();
        var jobIds = sessions.Where(s => s.JobId.HasValue).Select(s => s.JobId!.Value).Distinct().ToArray();
        var revisions = await db.ReportRevisions.AsNoTracking().Where(r => jobIds.Contains(r.JobId)).ToListAsync(ct);
        var reviews = await db.QcReviews.AsNoTracking().Where(r => sessionIds.Contains(r.SurveySessionId)).ToDictionaryAsync(r => r.SurveySessionId, ct);
        var findings = await db.Findings.AsNoTracking().Where(f => f.SurveySessionId.HasValue && sessionIds.Contains(f.SurveySessionId.Value)).ToListAsync(ct);
        var userIds = projects.SelectMany(p => new[] {p.AssignedSurveyorId, p.AssignedQcId}).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var generated = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "Project" && ids.Contains(a.EntityId) &&
            (a.Action == "Report Generated" || a.Action == "Report Revision Available")).ToListAsync(ct);
        return projects.Select(p => {
            var pins = equipment.Where(e => e.ProjectId == p.Id).ToArray();
            var pinIds = pins.Select(e => e.Id).ToHashSet();
            var inspections = sessions.Where(s => s.AreaEquipmentId.HasValue && pinIds.Contains(s.AreaEquipmentId.Value)).ToArray();
            var submitted = inspections.Where(s => s.SubmittedAt.HasValue || s.Status is SurveyStatus.Submitted or SurveyStatus.Returned or SurveyStatus.Approved).ToArray();
            var inspectedIds = submitted.Select(s => s.AreaEquipmentId!.Value).ToHashSet();
            var inspectionIds = inspections.Select(s => s.Id).ToHashSet();
            var defects = findings.Where(f => f.SurveySessionId.HasValue && inspectionIds.Contains(f.SurveySessionId.Value)).ToArray();
            QcReviewStatus Status(SurveySession s) => s.Status == SurveyStatus.Approved ? QcReviewStatus.Approved :
                reviews.TryGetValue(s.Id, out var q) ? q.Status : s.Status == SurveyStatus.Returned ? QcReviewStatus.ChangesRequested : QcReviewStatus.PendingReview;
            var statuses = submitted.Select(Status).ToArray();
            var projectAreas = areas.Where(a => a.ProjectId == p.Id).ToArray();
            int completed = projectAreas.Count(a => pins.Any(e => e.AreaId == a.Id) && pins.Where(e => e.AreaId == a.Id).All(e => inspectedIds.Contains(e.Id)));
            var progress = new ClientProgress(projectAreas.Length, completed, pins.Length, inspectedIds.Count,
                submitted.Length, statuses.Count(s => s == QcReviewStatus.PendingReview), statuses.Count(s => s == QcReviewStatus.InReview),
                statuses.Count(s => s == QcReviewStatus.ChangesRequested), statuses.Count(s => s == QcReviewStatus.Approved),
                defects.Count(f => f.Status is FindingStatus.Open or FindingStatus.RectificationSubmitted),
                defects.Count(f => f.Status is FindingStatus.Verified or FindingStatus.Closed),
                pins.Length == 0 ? 0 : Math.Round(100m * inspectedIds.Count / pins.Length, 1));
            return new ClientProjectSummary(p.Id, p.ProjectName, p.Sector, p.JobNo, p.Location, p.VesselOrSiteName, p.Status,
                names.GetValueOrDefault(p.AssignedSurveyorId, "-"), names.GetValueOrDefault(p.AssignedQcId, "-"),
                statuses.Length == 0 ? "NotSubmitted" : QcWorkflowStatus.Aggregate(statuses) == QcReviewStatus.Approved ? "QcApproved" : QcWorkflowStatus.Aggregate(statuses).ToString(),
                p.DocumentNumber, p.Revision, generated.Where(a => a.EntityId == p.Id).Select(a => (DateTimeOffset?)a.OccurredAt)
                    .Concat(revisions.Where(r => inspections.Any(i => i.JobId == r.JobId)).Select(r => (DateTimeOffset?)r.GeneratedAt)).Max(), progress);
        }).ToArray();
    }

    public async Task<IReadOnlyList<ClientActivity>> Activity(CancellationToken ct)
    {
        var projects = await Projects.Select(p => new {p.Id, p.ProjectName}).ToListAsync(ct);
        var ids = projects.Select(p => p.Id).ToArray();
        var equipment = await db.AreaEquipment.AsNoTracking().Where(e => ids.Contains(e.ProjectId)).Select(e => new {e.Id, e.ProjectId, e.TagNumber}).ToListAsync(ct);
        var equipmentIds = equipment.Select(e => e.Id).ToArray();
        var sessions = await db.SurveySessions.AsNoTracking().Where(s => s.AreaEquipmentId.HasValue && equipmentIds.Contains(s.AreaEquipmentId.Value)).Select(s => new {s.Id, s.AreaEquipmentId, s.JobId}).ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToArray();
        var findings = await db.Findings.AsNoTracking().Where(f => f.SurveySessionId.HasValue && sessionIds.Contains(f.SurveySessionId.Value)).Select(f => new {f.Id, f.SurveySessionId}).ToListAsync(ct);
        var findingIds = findings.Select(f => f.Id).ToArray();
        var logs = await db.AuditLogs.AsNoTracking().Where(a => (a.EntityType == "Project" && ids.Contains(a.EntityId)) ||
            (a.EntityType == QcWorkflowHistory.EntityType && sessionIds.Contains(a.EntityId)) || (a.EntityType == "Finding" && findingIds.Contains(a.EntityId)))
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(100).ToListAsync(ct);
        var auditActivity = logs.Select(a => {
            var sessionId = a.EntityType == "Finding" ? findings.Single(f => f.Id == a.EntityId).SurveySessionId : a.EntityId;
            var pin = a.EntityType == "Project" ? null : equipment.Single(e => e.Id == sessions.Single(s => s.Id == sessionId).AreaEquipmentId);
            var projectId = pin?.ProjectId ?? a.EntityId;
            return new ClientActivity(a.Id, projectId, projects.Single(p => p.Id == projectId).ProjectName,
                a.Action, pin is null ? a.Comments : $"{pin.TagNumber}{(string.IsNullOrWhiteSpace(a.Comments) ? "" : ": " + a.Comments)}", a.OccurredAt);
        }).ToArray();
        var jobIds = sessions.Where(s => s.JobId.HasValue).Select(s => s.JobId!.Value).Distinct().ToArray();
        var revisions = await db.ReportRevisions.AsNoTracking().Where(r => jobIds.Contains(r.JobId)).OrderByDescending(r => r.GeneratedAt).Take(100).ToListAsync(ct);
        var reportActivity = revisions.SelectMany(r => sessions.Where(s => s.JobId == r.JobId)
            .Select(s => equipment.Single(e => e.Id == s.AreaEquipmentId).ProjectId).Distinct()
            .Select(projectId => new ClientActivity(r.Id, projectId, projects.Single(p => p.Id == projectId).ProjectName,
                "Report Revision Available", $"Revision {r.Revision}", r.GeneratedAt)));
        return auditActivity.Concat(reportActivity).OrderByDescending(a => a.OccurredAt).Take(100).ToArray();
    }
}
