using System.Security.Claims;
using System.Text.Json.Serialization;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public sealed record ReportAsset(string Key, string ContentType, string Url)
{
    [JsonIgnore] public string StorageKey { get; init; } = "";
}
public sealed record ReportAnswer(int Number, string Question, string? Value, string? Comment, string? InspectionType = null);
public sealed record ReportRevisionEntry(int Revision, DateTimeOffset GeneratedAt);
public sealed record ReportFinding(string Observation, string? Recommendation, string? Severity, string Status, IReadOnlyList<ReportAsset> Photos) {public Guid Id {get; init;}};
public sealed record ReportInspection(DateTimeOffset? Date, string Inspector, string? InspectionType, string? Comments,
    string Status, string? QcComments, string? QcSeverity, string CheckedBy, DateTimeOffset? ReviewedAt,
    IReadOnlyList<ReportAnswer> Answers, IReadOnlyList<ReportAsset> Photos, IReadOnlyList<ReportFinding> Findings) {public Guid Id {get; init;} public DateTimeOffset? ReviewStartedAt {get; init;}};
public sealed record ReportEquipment(int SequenceNo, string TagNumber, string? EquipmentId, string Type,
    decimal XPercent, decimal YPercent, string? CircuitId, string? Location, string? Manufacturer,
    string? ProtectionDetails, string? CertificateNumber, string? RfidTagUid,
    IReadOnlyList<ReportInspection> Inspections) {public Guid Id {get; init;} public string InspectionStatus {get; init;} = "NotStarted";};
public sealed record ReportArea(string Name, ReportAsset? Layout, IReadOnlyList<ReportEquipment> Equipment) {public Guid Id {get; init;}};
public sealed record ProjectReport(Guid ProjectId, string ProjectName, string JobNumber, string SiteName, string Location,
    string Client, string PreparedBy, string CheckedBy, string ApprovedBy, DateTimeOffset GeneratedAt,
    ReportAsset? HazardousAreaPlan, IReadOnlyList<ReportArea> Areas)
{
    public IReadOnlyList<ReportRevisionEntry> Revisions { get; init; } = [];
    public string? UidNumber { get; init; }
    public string? DocumentNumber { get; init; }
    public string? Revision { get; init; }
    public string? Description { get; init; }
    public string? ClientName { get; init; }
    public ReportAsset? ClientLogo { get; init; }
    public CompanyResponse? OurCompany {get; init;}
    public ReportAsset? OurCompanyLogo {get; init;}
    [JsonIgnore] public IEnumerable<ReportAsset> Assets =>
        (HazardousAreaPlan is null ? Enumerable.Empty<ReportAsset>() : new[] {HazardousAreaPlan})
        .Concat(ClientLogo is null ? Enumerable.Empty<ReportAsset>() : new[] {ClientLogo})
        .Concat(OurCompanyLogo is null ? Enumerable.Empty<ReportAsset>() : new[] {OurCompanyLogo})
        .Concat(Areas.Where(a => a.Layout != null).Select(a => a.Layout!))
        .Concat(Areas.SelectMany(a => a.Equipment).SelectMany(e => e.Inspections)
            .SelectMany(i => i.Photos.Concat(i.Findings.SelectMany(f => f.Photos)))).DistinctBy(a => a.Key);
}

public sealed record HaerRow(Guid EquipmentRecordId, int SequenceNo, string EquipmentId, string TagNumber, string Area, string EquipmentType,
    string? Description, string? Location, string? Manufacturer, string? Model, string? SerialNumber, string? CircuitId, string? ExProtectionType,
    string? GasGroup, string? TemperatureClass, string? HazardousAreaZone, string? AmbientTemperatureRange, string? IpRating,
    string? ProtectionDetails, string? CertificateNumber, string? NotifiedBody, string? SpecialConditionsOfUse,
    string? RfidTagUid, string InspectionStatus, string? LastInspectionGrade, DateTimeOffset? LastInspectionAt,
    string? LastInspectionResult, int OutstandingDefects, string? Inspector, string? QcStatus, string? RfidIdentificationMethod,
    string ReconciliationStatus, IReadOnlyDictionary<string, string> MetadataAvailability)
{
    public Guid AreaId { get; init; }
    public string? ItemNumber { get; init; }
    public string? EquipmentName { get; init; }
    public string? Comments { get; init; }
    public IReadOnlyList<HaerInspectionHistory> InspectionHistory {get; init;} = [];
    public IReadOnlyList<string> OutstandingDefectStatuses {get; init;} = [];
}
public sealed record HaerInspectionHistory(string Grade, DateTimeOffset? InspectedAt, string Status, string Result, string? Inspector);

public sealed record HaerReport(Guid ProjectId, string ProjectName, string JobNumber, string SiteName,
    string? ClientName, string? Revision, DateTimeOffset GeneratedAt, IReadOnlyList<HaerRow> Equipment);

public sealed class ProjectReportData(SurveyDbContext db)
{
    public IQueryable<Project> AccessibleProjects(ClaimsPrincipal user)
    {
        var id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var query = db.Projects.AsNoTracking();
        if (user.IsInRole("Admin")) return query;
        if (user.IsInRole("Manager")) return query.Where(p => p.CreatedByUserId == id || db.Users.Any(q => q.Id == p.AssignedQcId && q.ManagerId == id));
        if (user.IsInRole("Qc")) return query.Where(p => p.AssignedQcId == id);
        if (user.IsInRole("Surveyor")) return query.Where(p => p.AssignedSurveyorId == id);
        if (user.IsInRole("Client")) return query.Where(p => p.AssignedClientId == id);
        return query.Where(_ => false);
    }

    public async Task<ProjectReport?> Load(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var project = await AccessibleProjects(user).Include(p => p.OurCompany).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return null;
        var areas = await db.ProjectAreas.AsNoTracking().Where(a => a.ProjectId == id).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync(ct);
        var equipment = await db.AreaEquipment.AsNoTracking().Where(e => e.ProjectId == id).OrderBy(e => e.SequenceNo).ToListAsync(ct);
        var equipmentIds = equipment.Select(e => e.Id).ToArray();
        var sessions = await db.SurveySessions.AsNoTracking().Where(s => s.AreaEquipmentId.HasValue && equipmentIds.Contains(s.AreaEquipmentId.Value)).OrderBy(s => s.CreatedAt).ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToArray();
        var jobIds = sessions.Where(s => s.JobId.HasValue).Select(s => s.JobId!.Value).Distinct().ToArray();
        var revisions = await db.ReportRevisions.AsNoTracking().Where(r => jobIds.Contains(r.JobId)).OrderBy(r => r.Revision)
            .Select(r => new ReportRevisionEntry(r.Revision, r.GeneratedAt)).ToListAsync(ct);
        var answers = await (from a in db.SurveyAnswers.AsNoTracking() join q in db.ChecklistQuestions.AsNoTracking() on a.ChecklistQuestionId equals q.Id
            where sessionIds.Contains(a.SurveySessionId) orderby q.SortOrder select new {a.SurveySessionId, q.ChecklistTemplateId, q.SortOrder, q.Text, a.Value, a.Comment}).ToListAsync(ct);
        var findings = await db.Findings.AsNoTracking().Where(f => f.SurveySessionId.HasValue && sessionIds.Contains(f.SurveySessionId.Value)).OrderBy(f => f.Id).ToListAsync(ct);
        var findingIds = findings.Select(f => f.Id).ToArray();
        var files = await db.Files.AsNoTracking().Where(f =>
            ((f.SurveySessionId.HasValue && sessionIds.Contains(f.SurveySessionId.Value)) ||
             (f.FindingId.HasValue && findingIds.Contains(f.FindingId.Value))) &&
            (f.Category == FileCategory.EquipmentPhoto || f.Category == FileCategory.FindingPhoto))
            .OrderBy(f => f.UploadedAt).ThenBy(f => f.Id).ToListAsync(ct);
        var reviews = await db.QcReviews.AsNoTracking().Where(q => sessionIds.Contains(q.SurveySessionId)).ToListAsync(ct);
        var templateIds = sessions.Where(s => s.ChecklistTemplateId.HasValue).Select(s => s.ChecklistTemplateId!.Value).Concat(answers.Select(a => a.ChecklistTemplateId)).Distinct().ToArray();
        var templates = await db.ChecklistTemplates.AsNoTracking().Where(t => templateIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var areaIds = areas.Select(a => a.Id).ToArray();
        var layouts = await db.ProjectAreaPhotos.AsNoTracking().Where(p => areaIds.Contains(p.ProjectAreaId)).OrderBy(p => p.UploadedAt).ToListAsync(ct);
        var userIds = sessions.Select(s => s.SurveyorId).Concat(reviews.Select(q => q.ReviewedByUserId))
            .Concat(new Guid?[] {project.AssignedSurveyorId, project.AssignedQcId, project.AssignedClientId}.Where(x => x.HasValue).Select(x => x!.Value)).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        string Name(Guid? userId) => userId.HasValue ? users.GetValueOrDefault(userId.Value) ?? "-" : "-";
        ReportAsset Asset(string key, string type, string path) => new(key, type, $"/api/reports/projects/{id}/assets/{key}") {StorageKey = path};
        ReportAsset Photo(SurveyFile f) => Asset("photo-" + f.Id, f.ContentType, f.StorageKey);
        ReportInspection Inspection(SurveySession s)
        {
            var qc = reviews.SingleOrDefault(q => q.SurveySessionId == s.Id);
            var inspector = string.IsNullOrWhiteSpace(s.InspectorName) ? Name(s.SurveyorId) : s.InspectorName;
            var imageFiles = files.Where(f => f.SurveySessionId == s.Id || (f.FindingId.HasValue && findings.Any(d => d.Id == f.FindingId && d.SurveySessionId == s.Id))).Where(f => f.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)).ToArray();
            var status = qc?.Status == QcReviewStatus.Approved || s.Status == SurveyStatus.Approved ? "QcApproved" : qc?.Status.ToString() ?? (s.SubmittedAt.HasValue ? "PendingReview" : "NotSubmitted");
            var inspectionTypes = (s.ChecklistTemplateId.HasValue ? new[] {s.ChecklistTemplateId.Value} : answers.Where(a => a.SurveySessionId == s.Id).Select(a => a.ChecklistTemplateId).Distinct())
                .Where(templates.ContainsKey).Select(t => templates[t].InspectionType).Distinct().ToArray();
            return new(s.InspectionTimestamp ?? s.SubmittedAt, inspector,
                inspectionTypes.Length > 0 ? string.Join(", ", inspectionTypes.Select(t => t.ToString())) : null,
                s.SurveyorRemarks, status, qc?.Comments, qc?.Severity.ToString(), qc is null ? "-" : Name(qc.ReviewedByUserId), qc?.ReviewedAt,
                answers.Where(a => a.SurveySessionId == s.Id).Select(a => new ReportAnswer(a.SortOrder, a.Text, a.Value, a.Comment,
                    templates.TryGetValue(a.ChecklistTemplateId, out var template) ? template.InspectionType.ToString() : null)).ToArray(),
                imageFiles.Select(Photo).ToArray(), findings.Where(f => f.SurveySessionId == s.Id).Select(f => new ReportFinding(f.Observation, f.Recommendation, f.Severity, f.Status.ToString(), imageFiles.Where(p => p.FindingId == f.Id).Select(Photo).ToArray()) {Id = f.Id}).ToArray()) {Id = s.Id, ReviewStartedAt = qc?.StartedAt};
        }
        var checkedNames = reviews.Select(q => Name(q.ReviewedByUserId)).Distinct().ToArray();
        var approvedNames = reviews.Where(q => q.Status == QcReviewStatus.Approved).Select(q => Name(q.ReviewedByUserId)).Distinct().ToArray();
        return new(id, project.ProjectName, project.JobNo, project.VesselOrSiteName, project.Location, project.ClientName ?? Name(project.AssignedClientId), Name(project.AssignedSurveyorId),
            checkedNames.Length == 0 ? "-" : string.Join(", ", checkedNames), approvedNames.Length == 0 ? "-" : string.Join(", ", approvedNames), DateTimeOffset.UtcNow,
            project.HazardousAreaPlanPath is null ? null : Asset("plan", Path.GetExtension(project.HazardousAreaPlanPath).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg" }, project.HazardousAreaPlanPath),
            areas.Select(area => {
                var layout = layouts.FirstOrDefault(p => p.ProjectAreaId == area.Id && p.FilePath == area.LayoutImagePath) ?? layouts.FirstOrDefault(p => p.ProjectAreaId == area.Id);
                return new ReportArea(area.AreaName, layout is null ? null : Asset("layout-" + layout.Id, "image/" + Path.GetExtension(layout.FilePath).TrimStart('.'), layout.FilePath),
                    equipment.Where(e => e.AreaId == area.Id).Select(e => new ReportEquipment(e.SequenceNo, e.TagNumber, e.EquipmentCode, e.Type, e.XPercent, e.YPercent, e.CircuitId,
                        e.Location, e.Manufacturer, e.ProtectionDetails, e.CertificateNumber, e.RfidTagUid,
                        sessions.Where(s => s.AreaEquipmentId == e.Id).Select(Inspection).ToArray()) {Id = e.Id, InspectionStatus = QcWorkflowStatus.InspectionStatus(sessions.FirstOrDefault(s => s.AreaEquipmentId == e.Id)?.Status, reviews.FirstOrDefault(q => q.SurveySessionId == sessions.FirstOrDefault(s => s.AreaEquipmentId == e.Id)?.Id)?.Status).ToString()}).ToArray()) {Id = area.Id};
            }).ToArray()) {Revisions = revisions, UidNumber = project.UidNumber, DocumentNumber = project.DocumentNumber,
                Revision = project.Revision, Description = project.Description, ClientName = project.ClientName,
                ClientLogo = project.ClientLogoPath is null ? null : Asset("client-logo", Path.GetExtension(project.ClientLogoPath).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg", project.ClientLogoPath),
                OurCompany = project.OurCompany is null ? null : CompanyAssets.Response(project.OurCompany),
                OurCompanyLogo = project.OurCompany?.LogoPath is null || CompanyAssets.Response(project.OurCompany).LogoUrl is null ? null : Asset("our-company-logo", "image/png", "company-logo/" + project.OurCompany.LogoPath)};
    }

    public async Task<HaerReport?> LoadHaer(Guid id, ClaimsPrincipal user, CancellationToken ct, Guid? areaId = null)
    {
        var project = await AccessibleProjects(user).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return null;
        var areas = await db.ProjectAreas.AsNoTracking().Where(a => a.ProjectId == id)
            .ToDictionaryAsync(a => a.Id, a => a.AreaName, ct);
        if (areaId.HasValue && !areas.ContainsKey(areaId.Value)) return null;
        var equipment = await db.AreaEquipment.AsNoTracking().Where(e => e.ProjectId == id && (!areaId.HasValue || e.AreaId == areaId.Value))
            .OrderBy(e => e.AreaId).ThenBy(e => e.SequenceNo).ToListAsync(ct);
        var equipmentIds = equipment.Select(e => e.Id).ToArray();
        var sessions = await db.SurveySessions.AsNoTracking().Where(s => s.AreaEquipmentId.HasValue && equipmentIds.Contains(s.AreaEquipmentId.Value))
            .OrderByDescending(s => s.InspectionTimestamp ?? s.SubmittedAt ?? s.CreatedAt).ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToArray();
        var templateIds = sessions.Where(s => s.ChecklistTemplateId.HasValue).Select(s => s.ChecklistTemplateId!.Value).Distinct().ToArray();
        var grades = await db.ChecklistTemplates.AsNoTracking().Where(t => templateIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.InspectionType.ToString(), ct);
        var reviews = await db.QcReviews.AsNoTracking().Where(r => sessionIds.Contains(r.SurveySessionId)).ToDictionaryAsync(r => r.SurveySessionId, ct);
        var inspectors = await db.Users.AsNoTracking().Where(u => sessions.Select(s => s.SurveyorId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var savedAnswers = await db.SurveyAnswers.AsNoTracking().Where(a => sessionIds.Contains(a.SurveySessionId)).Select(a => new {a.SurveySessionId, a.Value}).ToListAsync(ct);
        var legacyAnswersBySession = savedAnswers.GroupBy(x => x.SurveySessionId).ToDictionary(x => x.Key, x => x.Select(a => a.Value).ToArray());
        var legacyFindingStatuses = await (from finding in db.Findings.AsNoTracking() join session in db.SurveySessions.AsNoTracking() on finding.SurveySessionId equals session.Id
                                           where session.AreaEquipmentId.HasValue && equipmentIds.Contains(session.AreaEquipmentId.Value) && finding.Status != FindingStatus.Closed && finding.Status != FindingStatus.Verified
                                           select new {EquipmentId = session.AreaEquipmentId!.Value, finding.Status}).ToListAsync(ct);
        var registerFindingStatuses = await db.Findings.AsNoTracking()
            .Where(f => f.RegisterEquipmentId.HasValue && equipmentIds.Contains(f.RegisterEquipmentId.Value) && f.Status != FindingStatus.Closed && f.Status != FindingStatus.Verified)
            .Select(f => new {EquipmentId = f.RegisterEquipmentId!.Value, f.Status}).ToListAsync(ct);
        var findingStatusesByEquipment = legacyFindingStatuses.Concat(registerFindingStatuses).ToLookup(f => f.EquipmentId, f => f.Status.ToString());
        var activityRows = await (from activity in db.InspectionActivities.AsNoTracking()
                                  join task in db.InspectionTasks.AsNoTracking() on activity.InspectionTaskId equals task.Id
                                  where equipmentIds.Contains(task.AreaEquipmentId ?? Guid.Empty)
                                  orderby activity.StartedAt descending
                                  select new {EquipmentId = task.AreaEquipmentId!.Value, activity.Grade, activity.ReviewStatus, activity.StartedAt, activity.InspectorNameSnapshot, activity.Id}).ToListAsync(ct);
        var activityIds = activityRows.Select(x => x.Id).ToArray();
        var activityComments = (await db.ActivityReviewEvents.AsNoTracking()
            .Where(x => activityIds.Contains(x.InspectionActivityId) && x.Comments != null)
            .OrderByDescending(x => x.DecidedAt).Select(x => new {x.InspectionActivityId, x.Comments}).ToListAsync(ct))
            .GroupBy(x => x.InspectionActivityId).ToDictionary(x => x.Key, x => x.First().Comments);
        var activityResponses = await (from response in db.CheckpointResponseRevisions.AsNoTracking()
                                       join revision in db.InspectionActivityRevisions.AsNoTracking() on response.ActivityRevisionId equals revision.Id
                                       where activityIds.Contains(revision.InspectionActivityId)
                                       select new {revision.InspectionActivityId, response.CheckpointDefinitionSnapshotId, response.RevisionNumber, response.ResponseCode}).ToListAsync(ct);
        var activityResponsesByActivity = activityResponses.GroupBy(x => x.InspectionActivityId).ToDictionary(g => g.Key, g => g.GroupBy(x => x.CheckpointDefinitionSnapshotId).Select(cp => cp.OrderByDescending(x => x.RevisionNumber).First().ResponseCode).ToArray());
        var activityFindings = await db.ActivityFindings.AsNoTracking().Where(f => activityIds.Contains(f.InspectionActivityId) && f.StatusCode != "ClientClosed" && f.StatusCode != "Closed")
            .Select(f => new {f.InspectionActivityId, f.StatusCode}).ToListAsync(ct);
        var equipmentByActivity = activityRows.ToDictionary(x => x.Id, x => x.EquipmentId);
        var activityFindingCounts = activityFindings.Where(x => equipmentByActivity.ContainsKey(x.InspectionActivityId)).GroupBy(x => equipmentByActivity[x.InspectionActivityId]).ToDictionary(x => x.Key, x => x.Count());
        var rows = equipment.Select(e => {
            var latest = sessions.FirstOrDefault(s => s.AreaEquipmentId == e.Id);
            var latestActivity = activityRows.FirstOrDefault(x => x.EquipmentId == e.Id);
            var review = latest is not null && reviews.TryGetValue(latest.Id, out var found) ? found : null;
            var grade = latest?.ChecklistTemplateId is Guid templateId ? grades.GetValueOrDefault(templateId) : null;
            var status = QcWorkflowStatus.InspectionStatus(latest?.Status, review?.Status).ToString();
            var latestIsModern = latestActivity is not null && (latest is null || latestActivity.StartedAt > (latest.InspectionTimestamp ?? latest.SubmittedAt ?? latest.CreatedAt));
            var modernResponses = latestActivity is null ? Array.Empty<string?>() : activityResponsesByActivity.GetValueOrDefault(latestActivity.Id) ?? [];
            var legacyValues = latest is null ? Array.Empty<string?>() : legacyAnswersBySession.GetValueOrDefault(latest.Id) ?? [];
            var answers = latestIsModern ? modernResponses : legacyValues;
            var result = answers.Length == 0 ? null : answers.Any(x => x is "Fail" or "False" or "FAIL" or "false") ? "Fail" : answers.All(x => x is "Pass" or "True" or "PASS" or "true" or "NA" or "N/A") ? "Pass" : "Unknown";
            var notApplicable = System.Text.Json.JsonSerializer.Deserialize<string[]>(e.NotApplicableFieldsJson ?? "[]")?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            string Availability(string key, string? value) => notApplicable.Contains(key) ? "NotApplicable" : string.IsNullOrWhiteSpace(value) ? "Unknown" : "Available";
            var statusFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["description"] = Availability("description", e.Description), ["location"] = Availability("location", e.Location), ["manufacturer"] = Availability("manufacturer", e.Manufacturer), ["model"] = Availability("model", e.Model),
                ["serialNumber"] = Availability("serialNumber", e.SerialNumber), ["exProtectionType"] = Availability("exProtectionType", e.ExProtectionType),
                ["gasGroup"] = Availability("gasGroup", e.GasGroup), ["temperatureClass"] = Availability("temperatureClass", e.TemperatureClass),
                ["hazardousAreaZone"] = Availability("hazardousAreaZone", e.HazardousAreaZone), ["ambientTemperatureRange"] = Availability("ambientTemperatureRange", e.AmbientTemperatureRange),
                ["ipRating"] = Availability("ipRating", e.IpRating), ["certificateNumber"] = Availability("certificateNumber", e.CertificateNumber),
                ["notifiedBody"] = Availability("notifiedBody", e.NotifiedBody), ["specialConditionsOfUse"] = Availability("specialConditionsOfUse", e.SpecialConditionsOfUse),
                ["circuitId"] = Availability("circuitId", e.CircuitId), ["rfidTagUid"] = Availability("rfidTagUid", e.RfidTagUid), ["rfidIdentificationMethod"] = Availability("rfidIdentificationMethod", e.RfidIdentificationMethod)
            };
            var outstanding = findingStatusesByEquipment[e.Id].Count() + activityFindingCounts.GetValueOrDefault(e.Id);
            var history = sessions.Where(s => s.AreaEquipmentId == e.Id).Select(s => {
                var saved = legacyAnswersBySession.GetValueOrDefault(s.Id) ?? [];
                var legacyResult = saved.Length == 0 ? "Unknown" : saved.Any(x => x is "Fail" or "False" or "FAIL" or "false") ? "Fail" : saved.All(x => x is "Pass" or "True" or "PASS" or "true" or "NA" or "N/A") ? "Pass" : "Unknown";
                var sessionReview = reviews.GetValueOrDefault(s.Id);
                return new HaerInspectionHistory(s.ChecklistTemplateId is Guid checklistId ? grades.GetValueOrDefault(checklistId) ?? "Unknown" : "Unknown", s.InspectionTimestamp ?? s.SubmittedAt ?? s.CreatedAt,
                    QcWorkflowStatus.InspectionStatus(s.Status, sessionReview?.Status).ToString(), legacyResult, string.IsNullOrWhiteSpace(s.InspectorName) ? inspectors.GetValueOrDefault(s.SurveyorId) : s.InspectorName);
            }).Concat(activityRows.Where(a => a.EquipmentId == e.Id).Select(a => {
                var values = activityResponsesByActivity.GetValueOrDefault(a.Id) ?? [];
                var activityResult = values.Length == 0 ? "Unknown" : values.Any(x => x is "Fail" or "False" or "FAIL" or "false") ? "Fail" : values.All(x => x is "Pass" or "True" or "PASS" or "true" or "NA" or "N/A") ? "Pass" : "Unknown";
                return new HaerInspectionHistory(a.Grade?.ToString() ?? "Category checklist", a.StartedAt, a.ReviewStatus.ToString(), activityResult, a.InspectorNameSnapshot);
            })).OrderByDescending(x => x.InspectedAt).ToArray();
            var findingStatuses = findingStatusesByEquipment[e.Id]
                .Concat(activityFindings.Where(f => equipmentByActivity.GetValueOrDefault(f.InspectionActivityId) == e.Id).Select(f => f.StatusCode))
                .GroupBy(statusCode => statusCode).Select(group => $"{group.Key}: {group.Count()}").OrderBy(x => x).ToArray();
            return new HaerRow(e.Id, e.SequenceNo, e.EquipmentCode ?? e.TagNumber, e.TagNumber, areas.GetValueOrDefault(e.AreaId, "Unassigned area"),
                e.Type, e.Description, e.Location, e.Manufacturer, e.Model, e.SerialNumber, e.CircuitId, e.ExProtectionType, e.GasGroup, e.TemperatureClass, e.HazardousAreaZone, e.AmbientTemperatureRange,
                e.IpRating, e.ProtectionDetails, e.CertificateNumber, e.NotifiedBody, e.SpecialConditionsOfUse, e.RfidTagUid,
                latestIsModern ? latestActivity!.ReviewStatus.ToString() : status, latestIsModern ? latestActivity!.Grade?.ToString() : grade,
                latestIsModern ? latestActivity!.StartedAt : latest?.InspectionTimestamp ?? latest?.SubmittedAt,
                result, outstanding, latestIsModern ? latestActivity!.InspectorNameSnapshot : latest is null ? null : string.IsNullOrWhiteSpace(latest.InspectorName) ? inspectors.GetValueOrDefault(latest.SurveyorId) : latest.InspectorName,
                latestIsModern ? latestActivity!.ReviewStatus.ToString() : review?.Status.ToString(), e.RfidIdentificationMethod, e.ReconciliationStatus, statusFields)
                {AreaId = e.AreaId, ItemNumber = e.EquipmentCode, EquipmentName = e.EquipmentName,
                    Comments = latestIsModern ? activityComments.GetValueOrDefault(latestActivity!.Id) : latest?.SurveyorRemarks ?? review?.Comments,
                    InspectionHistory = history, OutstandingDefectStatuses = findingStatuses};
        }).ToArray();
        return new HaerReport(project.Id, project.ProjectName, project.JobNo, project.VesselOrSiteName, project.ClientName, project.Revision, DateTimeOffset.UtcNow, rows);
    }
}
