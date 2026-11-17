using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

/// <summary>
/// Server-side query filters for tenant-owned data. Every method requires a context installed by
/// TenantScopeAuthorizationHandler after database membership validation; no request DTO/header ID
/// can construct this context.
/// </summary>
public interface ITenantQueryScope
{
    IQueryable<Project> Projects();
    IQueryable<ProjectArea> Areas();
    IQueryable<AreaEquipment> Equipment();
    IQueryable<SurveySession> SurveySessions();
    IQueryable<SurveyAnswer> ChecklistAnswers();
    IQueryable<Finding> Findings();
    IQueryable<QcReview> QcReviews();
    IQueryable<ProjectAreaPhoto> ProjectAreaPhotos();
    IQueryable<SurveyFile> Files();
    IQueryable<ReportRevision> ReportRevisions();
    IQueryable<Project> ClientDashboardProjects(Guid clientId);
}

public sealed class TenantQueryScope(SurveyDbContext db, ITenantContextAccessor contextAccessor) : ITenantQueryScope
{
    private Guid TenantId => contextAccessor.Current?.TenantId
        ?? throw new UnauthorizedAccessException("An active server-validated tenant membership is required.");

    private IQueryable<Guid> ProjectIds => db.TenantProjectBindings.AsNoTracking()
        .Where(x => x.TenantId == TenantId && !db.TenantProjectBindings.AsNoTracking()
            .Any(other => other.ProjectId == x.ProjectId && other.TenantId != TenantId))
        .Select(x => x.ProjectId);

    private IQueryable<LegacyEntityMap> LegacyRecordMaps(string type) => db.LegacyEntityMaps.AsNoTracking()
        .Where(m => m.TenantId == TenantId && m.SourceEntityType == type && m.Status == LegacyMappingStatus.Verified &&
                    m.TargetEntityType == "Project" && m.TargetId != null && ProjectIds.Contains(m.TargetId.Value) &&
                    !db.LegacyEntityMaps.AsNoTracking().Any(other => other.Id != m.Id && other.SourceSystem == m.SourceSystem &&
                        other.SourceEntityType == m.SourceEntityType && other.SourceRecordKey == m.SourceRecordKey &&
                        other.Status == LegacyMappingStatus.Verified));

    public IQueryable<Project> Projects() => db.Projects.AsNoTracking().Where(x => ProjectIds.Contains(x.Id));
    public IQueryable<ProjectArea> Areas() => db.ProjectAreas.AsNoTracking().Where(x => ProjectIds.Contains(x.ProjectId));
    public IQueryable<AreaEquipment> Equipment() => db.AreaEquipment.AsNoTracking()
        .Where(x => ProjectIds.Contains(x.ProjectId) && db.ProjectAreas.Any(a => a.Id == x.AreaId && a.ProjectId == x.ProjectId));
    public IQueryable<SurveySession> SurveySessions() => db.SurveySessions.AsNoTracking().Where(x =>
        (x.AreaEquipmentId != null || x.EquipmentId != null || x.JobId != null) &&
        (x.AreaEquipmentId == null || Equipment().Any(e => e.Id == x.AreaEquipmentId)) &&
        (x.EquipmentId == null || LegacyRecordMaps("Equipment").Any(m => m.SourceRecordKey == x.EquipmentId.Value.ToString())) &&
        (x.JobId == null || LegacyRecordMaps("Job").Any(m => m.SourceRecordKey == x.JobId.Value.ToString())));
    public IQueryable<SurveyAnswer> ChecklistAnswers() => db.SurveyAnswers.AsNoTracking()
        .Where(x => SurveySessions().Any(s => s.Id == x.SurveySessionId));
    public IQueryable<Finding> Findings() => db.Findings.AsNoTracking()
        .Where(x => SurveySessions().Any(s => s.Id == x.SurveySessionId));
    public IQueryable<QcReview> QcReviews() => db.QcReviews.AsNoTracking()
        .Where(x => SurveySessions().Any(s => s.Id == x.SurveySessionId));
    public IQueryable<ProjectAreaPhoto> ProjectAreaPhotos() => db.ProjectAreaPhotos.AsNoTracking()
        .Where(x => Areas().Any(a => a.Id == x.ProjectAreaId));
    public IQueryable<SurveyFile> Files() => db.Files.AsNoTracking().Where(x =>
        (x.SurveySessionId != null || x.FindingId != null || x.JobId != null || x.AreaId != null || x.EquipmentId != null) &&
        (x.SurveySessionId == null || SurveySessions().Any(s => s.Id == x.SurveySessionId)) &&
        (x.FindingId == null || Findings().Any(f => f.Id == x.FindingId)) &&
        (x.JobId == null || LegacyRecordMaps("Job").Any(m => m.SourceRecordKey == x.JobId.Value.ToString())) &&
        (x.AreaId == null || LegacyRecordMaps("Area").Any(m => m.SourceRecordKey == x.AreaId.Value.ToString())) &&
        (x.EquipmentId == null || LegacyRecordMaps("Equipment").Any(m => m.SourceRecordKey == x.EquipmentId.Value.ToString())));
    public IQueryable<ReportRevision> ReportRevisions() => db.ReportRevisions.AsNoTracking()
        .Where(x => LegacyRecordMaps("Job").Any(m => m.SourceRecordKey == x.JobId.ToString()));
    public IQueryable<Project> ClientDashboardProjects(Guid clientId) => Projects().Where(x => (x.AssignedClientId == clientId || x.ClientAssignments.Any(a => a.IsActive && a.ClientUserId == clientId)));

}
