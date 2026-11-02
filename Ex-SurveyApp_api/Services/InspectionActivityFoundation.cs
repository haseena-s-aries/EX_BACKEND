using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public sealed record NewInspectionActivity(
    Guid TaskId,
    InspectionType Grade,
    Guid InspectorUserId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    Guid ChecklistVersionId,
    string InspectorNameSnapshot);

public interface IInspectionActivityWriter
{
    Task<InspectionActivity> CreateAsync(NewInspectionActivity request, CancellationToken cancellationToken = default);
    Task AddRevisionAsync(Guid activityId, InspectionActivityRevision revision, CancellationToken cancellationToken = default);
}

public interface ICorrectiveActionPolicy
{
    void EnsureClientMayClose(UserRole actorRole, bool isAssignedProjectClient, CorrectiveActionStatus currentStatus);
}

public sealed record InspectionTenantOwnership(bool IsResolved, Guid? ProjectId, Guid? TenantId, string ResultCode);

public interface IInspectionTenantOwnershipResolver
{
    Task<InspectionTenantOwnership> ResolveTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}

/// <summary>Read-only ownership preparation. It never accepts a tenant ID from the activity request.</summary>
public sealed class InspectionTenantOwnershipResolver(SurveyDbContext db) : IInspectionTenantOwnershipResolver
{
    public async Task<InspectionTenantOwnership> ResolveTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await db.InspectionTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == taskId, cancellationToken);
        if (task is null) return new(false, null, null, "TaskNotFound");
        var planProjectId = await db.InspectionPlans.AsNoTracking().Where(x => x.Id == task.InspectionPlanId)
            .Select(x => x.ProjectId).SingleOrDefaultAsync(cancellationToken);
        Guid? projectId;
        if (task.AreaEquipmentId is Guid pinId)
            projectId = await db.AreaEquipment.AsNoTracking().Where(x => x.Id == pinId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(cancellationToken);
        else if (task.LegacyEquipmentId is Guid legacyId)
        {
            var mappings = await db.LegacyEntityMaps.AsNoTracking().Where(x => x.SourceEntityType == "Equipment" && x.SourceRecordKey == legacyId.ToString("D") && x.TargetEntityType == "Project" && x.Status == LegacyMappingStatus.Verified).ToListAsync(cancellationToken);
            var mappedProjects = mappings.Where(x => x.TargetId.HasValue).Select(x => x.TargetId!.Value).Distinct().ToArray();
            if (mappedProjects.Length != 1) return new(false, null, null, mappings.Count == 0 ? "LegacyEquipmentUnmapped" : "LegacyEquipmentConflictingMap");
            projectId = mappedProjects[0];
        }
        else return new(false, null, null, "TaskEquipmentMissing");

        if (planProjectId.HasValue && planProjectId != projectId) return new(false, projectId, null, "PlanEquipmentProjectConflict");
        if (projectId is null) return new(false, null, null, "EquipmentProjectUnresolved");
        var tenantIds = await db.TenantProjectBindings.AsNoTracking().Where(x => x.ProjectId == projectId).Select(x => x.TenantId).Distinct().ToArrayAsync(cancellationToken);
        if (tenantIds.Length != 1) return new(false, projectId, null, tenantIds.Length == 0 ? "ProjectTenantUnmapped" : "ProjectTenantConflict");

        if (task.LegacyEquipmentId is Guid mappedLegacyId)
        {
            var tenantIdsForLegacy = await db.LegacyEntityMaps.AsNoTracking().Where(x => x.SourceEntityType == "Equipment" && x.SourceRecordKey == mappedLegacyId.ToString("D") && x.TargetEntityType == "Project" && x.TargetId == projectId && x.Status == LegacyMappingStatus.Verified && x.TenantId.HasValue).Select(x => x.TenantId!.Value).Distinct().ToArrayAsync(cancellationToken);
            if (tenantIdsForLegacy.Length != 1 || tenantIdsForLegacy[0] != tenantIds[0]) return new(false, projectId, null, "LegacyEquipmentTenantConflict");
        }
        return new(true, projectId, tenantIds[0], "Resolved");
    }
}

public sealed class CorrectiveActionPolicy : ICorrectiveActionPolicy
{
    public void EnsureClientMayClose(UserRole actorRole, bool isAssignedProjectClient, CorrectiveActionStatus currentStatus)
    {
        if (actorRole != UserRole.Client || !isAssignedProjectClient)
            throw new UnauthorizedAccessException("Only the client assigned to the owning project may formally close a corrective action.");
        if (currentStatus is CorrectiveActionStatus.ClientClosed)
            throw new InvalidOperationException("The corrective action is already closed.");
        if (currentStatus is not CorrectiveActionStatus.Verified)
            throw new InvalidOperationException("Aries verification is required before the client decision.");
    }
}

/// <summary>Application-level guard for new writes. Legacy controller workflows are not routed through it.</summary>
public static class InspectionActivityRules
{
    public static void EnsureFindingUsesFailedCheckpoint(CheckpointResponseRevision response)
    {
        if (response is null || !string.Equals(response.ResponseCode, "Fail", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A defect can only be raised against a failed checkpoint response.");
    }

    public static void ValidateGradeChecklist(InspectionType requestedGrade, InspectionType checklistGrade)
    {
        if (!Enum.IsDefined(requestedGrade) || requestedGrade != checklistGrade)
            throw new ArgumentException("Selected grade does not match the published checklist version.");
    }

    public static void EnsureExpectedRowVersion(byte[] actual, string presented)
    {
        if (!Convert.TryFromBase64String(presented, new byte[64], out var count))
            throw new ArgumentException("Activity row version is invalid.");
        var expected = Convert.FromBase64String(presented);
        if (count != actual.Length || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new InspectionRequestConflictException("Activity row version is stale.");
    }

    public static InspectionActivity Create(NewInspectionActivity request)
    {
        ValidateNewActivity(request);
        return new InspectionActivity
        {
            InspectionTaskId = request.TaskId,
            Grade = request.Grade,
            InspectorUserId = request.InspectorUserId,
            StartedAt = request.StartedAt,
            CompletedAt = request.CompletedAt,
            ChecklistVersionId = request.ChecklistVersionId,
            InspectorNameSnapshot = request.InspectorNameSnapshot.Trim()
        };
    }

    public static void EnsureRevisionAllowed(InspectionActivityReviewStatus status)
    {
        if (status == InspectionActivityReviewStatus.Accepted)
            throw new InvalidOperationException("Accepted activity history is immutable; create a new activity for a repeat inspection.");
    }

    public static ActivityReviewEvent RecordDecision(InspectionActivity activity, InspectionActivityReviewStatus decision, Guid reviewerId, string? comments, DateTimeOffset decidedAt)
    {
        if (reviewerId == Guid.Empty || decidedAt == default) throw new ArgumentException("Reviewer and decision time are required.");
        activity.ReviewStatus = decision;
        return new ActivityReviewEvent { InspectionActivityId = activity.Id, Decision = decision, ActorUserId = reviewerId, Comments = comments, DecidedAt = decidedAt };
    }

    public static void ValidateNewActivity(NewInspectionActivity request)
    {
        if (request.TaskId == Guid.Empty || request.InspectorUserId == Guid.Empty || request.ChecklistVersionId == Guid.Empty)
            throw new ArgumentException("Task, inspector and published checklist version are required.");
        if (request.StartedAt == default || request.CompletedAt is not null && request.CompletedAt < request.StartedAt)
            throw new ArgumentException("Activity timestamps are invalid.");
        if (string.IsNullOrWhiteSpace(request.InspectorNameSnapshot))
            throw new ArgumentException("Inspector display name snapshot is required.");
    }

    public static void ValidateCheckpointRevision(CheckpointResponseRevision response)
    {
        if (response.ActivityRevisionId == Guid.Empty || response.CheckpointDefinitionSnapshotId == Guid.Empty || response.RecordedByUserId == Guid.Empty)
            throw new ArgumentException("Activity revision, checkpoint and recording user are required.");
        if (response.RevisionNumber < 1 || response.RecordedAt == default)
            throw new ArgumentException("Checkpoint response revision and timestamp must be valid.");
    }

    public static void ValidateActivityAcceptanceDoesNotCloseFindings(IEnumerable<ActivityFinding> findings)
    {
        // Intentionally no status mutation: accepted inspection and defect closure are separate decisions.
        _ = findings ?? throw new ArgumentNullException(nameof(findings));
    }
}

public static class ChecklistApplicability
{
    public static bool TemplateMatches(ChecklistTemplate template, InspectionType grade, string? equipmentProtectionType)
    {
        if (!template.IsActive || template.LifecycleStatus != ChecklistLifecycleStatus.Published || template.InspectionType != grade) return false;
        if (string.IsNullOrWhiteSpace(template.ApplicableProtectionTypes)) return template.FamilyId is null; // legacy templates only
        if (string.IsNullOrWhiteSpace(equipmentProtectionType)) return false;
        return Matches(template.ApplicableProtectionTypes, equipmentProtectionType);
    }

    public static bool QuestionMatches(ChecklistQuestion question, string? equipmentProtectionType)
    {
        if (string.IsNullOrWhiteSpace(question.ApplicableProtectionTypes)) return true;
        return !string.IsNullOrWhiteSpace(equipmentProtectionType) && Matches(question.ApplicableProtectionTypes, equipmentProtectionType);
    }

    private static bool Matches(string configured, string actual) => string.Equals(configured.Trim(), "*", StringComparison.Ordinal) ||
        configured.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(actual.Trim(), StringComparer.OrdinalIgnoreCase);
}
