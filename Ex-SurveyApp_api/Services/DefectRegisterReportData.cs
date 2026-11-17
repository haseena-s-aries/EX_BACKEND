using System.Security.Claims;
using System.Text.Json.Serialization;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public sealed record DefectReportPhoto(Guid Id, string FileName, string ContentType, string Url, int Slot)
{
    [JsonIgnore] public string StorageKey { get; init; } = "";
}
public sealed record DefectReportRow(Guid Id, string ActionNo, DateOnly? Date, string? ItemNumber, string? AosdId,
    string? FaultCategory, string? FaultCode, string? Area, string? Grade, string? EquipmentDescription,
    string Observation, string? Recommendation, string Status, string? Remarks, string? AdditionalRemarks,
    IReadOnlyList<DefectReportPhoto> Photos);
public sealed record DefectRegisterReport(Guid ProjectId, string ProjectName, string JobNumber, string SiteName,
    string Location, string Inspectors, IReadOnlyList<DefectReportRow> Items)
{
    [JsonIgnore] public string? ClientLogoPath { get; init; }
    [JsonIgnore] public string? CompanyLogoName { get; init; }
}

/// <summary>Read-only report mapping; no reconciliation, defect edits or status transitions.</summary>
public sealed class DefectRegisterReportData(SurveyDbContext db)
{
    public async Task<DefectRegisterReport?> Load(Guid projectId, ClaimsPrincipal user, CancellationToken ct)
    {
        var project = await new ProjectReportData(db).AccessibleProjects(user).Include(p => p.OurCompany)
            .SingleOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return null;
        var equipment = await db.AreaEquipment.AsNoTracking().Where(e => e.ProjectId == projectId).ToDictionaryAsync(e => e.Id, ct);
        var areas = await db.ProjectAreas.AsNoTracking().Where(a => a.ProjectId == projectId).ToDictionaryAsync(a => a.Id, a => a.AreaName, ct);
        var sessions = await db.SurveySessions.AsNoTracking().Where(s => s.AreaEquipmentId.HasValue && equipment.Keys.Contains(s.AreaEquipmentId.Value)).ToDictionaryAsync(s => s.Id, ct);
        var sessionIds = sessions.Keys.ToArray();
        var equipmentIds = equipment.Keys.ToArray();
        var legacy = await db.Findings.AsNoTracking().Where(f => f.RegisterEquipmentId.HasValue && equipmentIds.Contains(f.RegisterEquipmentId.Value)
            || f.SurveySessionId.HasValue && sessionIds.Contains(f.SurveySessionId.Value)).ToListAsync(ct);
        var modern = await (from f in db.ActivityFindings.AsNoTracking()
            join a in db.InspectionActivities.AsNoTracking() on f.InspectionActivityId equals a.Id
            join t in db.InspectionTasks.AsNoTracking() on a.InspectionTaskId equals t.Id
            join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id
            where p.ProjectId == projectId
            select new { Finding = f, Activity = a, t.AreaEquipmentId }).ToListAsync(ct);
        var modernIds = modern.Select(x => x.Finding.Id).ToArray();
        var actions = await db.CorrectiveActions.AsNoTracking().Where(a => modernIds.Contains(a.ActivityFindingId)).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync(ct);
        var actionIds = actions.Select(a => a.Id).ToArray();
        var events = await db.CorrectiveActionEvents.AsNoTracking().Where(e => actionIds.Contains(e.CorrectiveActionId)).ToListAsync(ct);
        var eventIds = events.Select(e => e.Id).ToArray();
        var activityIds = modern.Select(x => x.Activity.Id).Distinct().ToArray();
        var evidence = await db.ActivityEvidence.AsNoTracking().Where(e => e.InspectionActivityId.HasValue && activityIds.Contains(e.InspectionActivityId.Value)
            || e.CorrectiveActionEventId.HasValue && eventIds.Contains(e.CorrectiveActionEventId.Value)).OrderBy(e => e.AddedAt).ThenBy(e => e.Id).ToListAsync(ct);
        var responseIds = modern.Select(x => x.Finding.CheckpointResponseRevisionId).Concat(evidence.Where(e => e.CheckpointResponseRevisionId.HasValue).Select(e => e.CheckpointResponseRevisionId!.Value)).ToArray();
        var definitions = await db.CheckpointResponseRevisions.AsNoTracking().Where(r => responseIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.CheckpointDefinitionSnapshotId, ct);
        var evidenceFileIds = evidence.Select(e => e.SurveyFileId).ToArray();
        var legacyIds = legacy.Select(f => f.Id).ToArray();
        var files = await db.Files.AsNoTracking().Where(f => (f.ProjectId == null || f.ProjectId == projectId) && (evidenceFileIds.Contains(f.Id)
            || f.FindingId.HasValue && legacyIds.Contains(f.FindingId.Value) && f.Category == FileCategory.FindingPhoto
            || f.SurveySessionId.HasValue && sessionIds.Contains(f.SurveySessionId.Value) && f.Category == FileCategory.EquipmentPhoto))
            .OrderBy(f => f.UploadedAt).ThenBy(f => f.Id).ToListAsync(ct);
        var templateIds = sessions.Values.Where(s => s.ChecklistTemplateId.HasValue).Select(s => s.ChecklistTemplateId!.Value).Distinct().ToArray();
        var templateGrades = await db.ChecklistTemplates.AsNoTracking().Where(t => templateIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.InspectionType, ct);
        var linkedLegacyIds = modern.Where(x => x.Finding.LegacyFindingId.HasValue).Select(x => x.Finding.LegacyFindingId!.Value).ToHashSet();
        var rows = new List<DefectReportRow>();
        var inspectorIds = new HashSet<Guid>();
        var recordedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? Area(AreaEquipment? e) => e is null ? null : areas.GetValueOrDefault(e.AreaId);
        IReadOnlyList<DefectReportPhoto> Photos(IEnumerable<SurveyFile> candidates)
        {
            var used = new HashSet<Guid>(); var output = new List<DefectReportPhoto>();
            foreach (var file in candidates)
            {
                if (!used.Add(file.Id)) continue;
                var slot = file.EquipmentPhotoRole switch { "DefectPhoto1" => 1, "DefectPhoto2" => 2, _ => output.Any(p => p.Slot == 1) ? 2 : 1 };
                output.Add(new(file.Id, file.FileName, file.ContentType, $"/api/reports/projects/{projectId}/defect-register/photos/{file.Id}", slot) { StorageKey = file.StorageKey });
            }
            return output;
        }
        foreach (var f in legacy.Where(f => !linkedLegacyIds.Contains(f.Id)))
        {
            var session = f.SurveySessionId.HasValue ? sessions.GetValueOrDefault(f.SurveySessionId.Value) : null;
            var e = equipment.GetValueOrDefault(f.RegisterEquipmentId ?? session?.AreaEquipmentId ?? Guid.Empty);
            if (e is null) continue;
            if (session is not null) {
                if (!string.IsNullOrWhiteSpace(session.InspectorName)) recordedNames.Add(session.InspectorName);
                else inspectorIds.Add(session.SurveyorId);
            }
            var grade = f.RegisterGrade ?? (session?.ChecklistTemplateId is {} templateId
                ? (templateGrades.TryGetValue(templateId, out var savedGrade) ? (InspectionType?)savedGrade : null) : null);
            var photos = files.Where(p => p.FindingId == f.Id)
                .Concat(files.Where(p => p.FindingId == null && session is not null && p.SurveySessionId == session.Id));
            rows.Add(new(f.Id, f.Id.ToString("N")[..8].ToUpperInvariant(), f.RegisterDate ?? (session is null ? null : DateOnly.FromDateTime((session.InspectionTimestamp ?? session.CreatedAt).Date)),
                e.EquipmentCode, f.AosdId, f.FaultCategory, f.FaultCode, Area(e), grade?.ToString(), e.Description ?? e.EquipmentName ?? e.Type,
                f.Observation, f.Recommendation, f.Status.ToString(), f.Remarks, f.AdditionalRemarks, Photos(photos)));
        }
        foreach (var item in modern)
        {
            var f = item.Finding; var e = equipment.GetValueOrDefault(item.AreaEquipmentId ?? Guid.Empty);
            if (!string.IsNullOrWhiteSpace(item.Activity.InspectorNameSnapshot)) recordedNames.Add(item.Activity.InspectorNameSnapshot);
            else inspectorIds.Add(item.Activity.InspectorUserId);
            var rowActions = actions.Where(a => a.ActivityFindingId == f.Id).ToArray();
            var rowEventIds = events.Where(ev => rowActions.Any(a => a.Id == ev.CorrectiveActionId)).Select(ev => ev.Id).ToHashSet();
            var definitionId = definitions.GetValueOrDefault(f.CheckpointResponseRevisionId);
            var photos = evidence.Where(ev => ev.CorrectiveActionEventId.HasValue ? rowEventIds.Contains(ev.CorrectiveActionEventId.Value)
                : ev.InspectionActivityId == item.Activity.Id && (!ev.CheckpointResponseRevisionId.HasValue || definitions.GetValueOrDefault(ev.CheckpointResponseRevisionId.Value) == definitionId))
                .OrderByDescending(ev => ev.CheckpointResponseRevisionId.HasValue)
                .Select(ev => (File: files.FirstOrDefault(p => p.Id == ev.SurveyFileId), Evidence: ev)).Where(x => x.File is not null)
                .Select(x => x.File!);
            if (f.LegacyFindingId.HasValue)
                photos = files.Where(p => p.FindingId == f.LegacyFindingId).Concat(photos);
            var actionReference = rowActions.Length == 0 ? f.Id.ToString("N")[..8].ToUpperInvariant() : string.Join("\n", rowActions.Select(a => a.ActionReference).Distinct());
            var status = rowActions.Length == 0 ? f.StatusCode : string.Join("\n", rowActions.Select(a => a.Status.ToString()).Distinct());
            var grade = string.IsNullOrWhiteSpace(f.FailedGrades) ? item.Activity.Grade?.ToString() : string.Join(" / ", f.FailedGrades.Split(',').Select(g => g switch { "D" => "Detailed", "C" => "Close", "V" => "Visual", _ => g }));
            rows.Add(new(f.Id, actionReference, DateOnly.FromDateTime(item.Activity.StartedAt.UtcDateTime),
                e?.EquipmentCode, f.AosdId, f.FaultCategory, f.FaultCode, Area(e), grade, e?.Description ?? e?.EquipmentName ?? e?.Type,
                f.Observation, f.ProposedAction, status, f.RegisterRemarks, f.FollowUpRemarks, Photos(photos)));
        }
        if (project.AssignedSurveyorId != Guid.Empty) inspectorIds.Add(project.AssignedSurveyorId);
        if (project.AdditionalSurveyorId.HasValue) inspectorIds.Add(project.AdditionalSurveyorId.Value);
        var names = await db.Users.AsNoTracking().Where(u => inspectorIds.Contains(u.Id)).OrderBy(u => u.DisplayName).Select(u => u.DisplayName).ToListAsync(ct);
        return new(project.Id, project.ProjectName, project.JobNo, project.VesselOrSiteName, project.Location, string.Join(", ", names.Concat(recordedNames).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name)),
            rows.OrderByDescending(r => r.Date).ThenBy(r => r.Id).ToArray()) { ClientLogoPath = project.ClientLogoPath, CompanyLogoName = project.OurCompany?.LogoPath };
    }
}
