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
public sealed record ReportFinding(string Observation, string? Recommendation, string? Severity, string Status, IReadOnlyList<ReportAsset> Photos);
public sealed record ReportInspection(DateTimeOffset? Date, string Inspector, string? InspectionType, string? Comments,
    string Status, string? QcComments, string? QcSeverity, string CheckedBy, DateTimeOffset? ReviewedAt,
    IReadOnlyList<ReportAnswer> Answers, IReadOnlyList<ReportAsset> Photos, IReadOnlyList<ReportFinding> Findings);
public sealed record ReportEquipment(int SequenceNo, string TagNumber, string? EquipmentId, string Type,
    decimal XPercent, decimal YPercent, string? CircuitId, string? Location, string? Manufacturer,
    string? ProtectionDetails, string? CertificateNumber, IReadOnlyList<ReportInspection> Inspections);
public sealed record ReportArea(string Name, ReportAsset? Layout, IReadOnlyList<ReportEquipment> Equipment);
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
        var findings = await db.Findings.AsNoTracking().Where(f => sessionIds.Contains(f.SurveySessionId)).OrderBy(f => f.Id).ToListAsync(ct);
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
                imageFiles.Select(Photo).ToArray(), findings.Where(f => f.SurveySessionId == s.Id).Select(f => new ReportFinding(f.Observation, f.Recommendation, f.Severity, f.Status.ToString(), imageFiles.Where(p => p.FindingId == f.Id).Select(Photo).ToArray())).ToArray());
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
                        e.Location, e.Manufacturer, e.ProtectionDetails, e.CertificateNumber, sessions.Where(s => s.AreaEquipmentId == e.Id).Select(Inspection).ToArray())).ToArray());
            }).ToArray()) {Revisions = revisions, UidNumber = project.UidNumber, DocumentNumber = project.DocumentNumber,
                Revision = project.Revision, Description = project.Description, ClientName = project.ClientName,
                ClientLogo = project.ClientLogoPath is null ? null : Asset("client-logo", Path.GetExtension(project.ClientLogoPath).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg", project.ClientLogoPath),
                OurCompany = project.OurCompany is null ? null : CompanyAssets.Response(project.OurCompany),
                OurCompanyLogo = project.OurCompany?.LogoPath is null || CompanyAssets.Response(project.OurCompany).LogoUrl is null ? null : Asset("our-company-logo", "image/png", "company-logo/" + project.OurCompany.LogoPath)};
    }
}
