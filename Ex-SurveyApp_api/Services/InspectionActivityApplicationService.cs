using System.Security.Claims;
using Ex_SurveyApp_api.Contracts.V1;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ex_SurveyApp_api.Services;

public sealed class InspectionActivityOptions { public bool Enabled { get; set; } public bool DocumentUsageEnabled { get; set; } }

public sealed class InspectionFeatureInactiveException() : InvalidOperationException("Inspection activity APIs are not active.");
public sealed class InspectionRequestConflictException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

public interface IInspectionActivityApplicationService
{
    Task<InspectionPlanResponse> CreatePlanAsync(CreateInspectionPlanRequest request, CancellationToken ct = default);
    Task<InspectionTaskResponse> CreateTaskAsync(CreateInspectionTaskRequest request, CancellationToken ct = default);
    Task<InspectionActivityResponse> CreateActivityAsync(Guid taskId, CreateInspectionActivityRequest request, CancellationToken ct = default);
    Task<CheckpointResponseResponse> SaveCheckpointResponseAsync(Guid activityId, SaveCheckpointResponseRequest request, CancellationToken ct = default);
    Task<ActivityFindingResponse> AddFindingAsync(Guid activityId, AddActivityFindingRequest request, CancellationToken ct = default);
    Task<ActivityEvidenceResponse> AddEvidenceAsync(Guid activityId, AddActivityEvidenceRequest request, CancellationToken ct = default);
    Task<CorrectiveActionResponse> CreateCorrectiveActionAsync(CreateCorrectiveActionRequest request, CancellationToken ct = default);
    Task<CorrectiveActionResponse> SubmitCorrectiveActionForVerificationAsync(Guid actionId, CancellationToken ct = default);
    Task<CorrectiveActionResponse> ReportRectificationAsync(Guid actionId, CorrectiveActionReportRequest request, CancellationToken ct = default);
    Task<InspectionActivityResponse> SubmitForReviewAsync(Guid activityId, CancellationToken ct = default);
    Task<InspectionActivityResponse> RecordReviewAsync(Guid activityId, ActivityReviewDecisionRequest request, CancellationToken ct = default);
    Task<CheckpointReviewEvent> RecordCheckpointReviewAsync(Guid activityId, CheckpointReviewRequest request, CancellationToken ct = default);
    Task<CorrectiveActionResponse> VerifyCorrectiveActionAsync(Guid actionId, CorrectiveActionVerificationRequest request, CancellationToken ct = default);
    Task<CorrectiveActionResponse> DecideClientClosureAsync(Guid actionId, ClientClosureDecisionRequest request, CancellationToken ct = default);
}

public interface IInspectionActivityActorContext { Guid? UserId { get; } }
public sealed class HttpInspectionActivityActorContext(IHttpContextAccessor accessor) : IInspectionActivityActorContext
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

/// <summary>Server-side actor and tenant checks; tenant identifiers are never accepted from DTOs.</summary>
public sealed class InspectionActivityAuthorizer(
    SurveyDbContext db,
    IInspectionTenantOwnershipResolver activityOwnership,
    ITenantScopeResolver tenantResolver,
    ITenantContextAccessor tenantContext,
    IInspectionActivityActorContext actorContext,
    IOptions<TenantFoundationOptions> tenantOptions)
{
    public async Task<User> RequireActorAsync(Guid? actorId, CancellationToken ct, params UserRole[] roles)
    {
        if (actorId is not Guid id || id == Guid.Empty) throw new UnauthorizedAccessException("Authenticated actor is required.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null || !user.IsActive || user.ApprovalStatus != ApprovalStatus.Approved || !roles.Contains(user.Role))
            throw new UnauthorizedAccessException("Actor is inactive, unapproved, or lacks the required role.");
        if (tenantOptions.Value.Enabled && tenantContext.Current is null) throw new UnauthorizedAccessException("Tenant-scoped authorization context is required.");
        return user;
    }

    public async Task EnsureProjectScopeAsync(Guid projectId, CancellationToken ct)
    {
        if (tenantOptions.Value.Enabled)
        {
            var (ownership, failure) = await tenantResolver.ResolveResourceAsync(TenantResourceKind.Project, projectId, ct);
            if (ownership is null || ownership.TenantId != tenantContext.Current?.TenantId)
                throw new UnauthorizedAccessException($"Project is outside the authorized tenant scope ({failure}).");
            return;
        }
        var id = actorContext.UserId ?? throw new UnauthorizedAccessException("Authenticated actor is required.");
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new UnauthorizedAccessException("Actor is unavailable.");
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId, ct) ?? throw new UnauthorizedAccessException("Project was not found.");
        var permitted = actor.Role switch
        {
            UserRole.Admin => true,
            UserRole.Manager => project.CreatedByUserId == actor.Id || await db.Users.AnyAsync(q => q.Id == project.AssignedQcId && q.ManagerId == actor.Id, ct),
            UserRole.Surveyor => project.AssignedSurveyorId == actor.Id,
            UserRole.Qc => project.AssignedQcId == actor.Id,
            UserRole.Client => project.AssignedClientId == actor.Id,
            _ => false
        };
        if (!permitted) throw new UnauthorizedAccessException("Project is outside the actor's assigned project scope.");
    }

    public async Task<InspectionTask> EnsureTaskScopeAsync(Guid taskId, CancellationToken ct)
    {
        var task = await db.InspectionTasks.SingleOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw new KeyNotFoundException("Inspection task was not found.");
        if (!tenantOptions.Value.Enabled)
        {
            var projectId = await db.InspectionPlans.Where(x => x.Id == task.InspectionPlanId).Select(x => x.ProjectId).SingleOrDefaultAsync(ct);
            if (task.AreaEquipmentId is null || projectId is null) throw new UnauthorizedAccessException("Task must reference equipment in an accessible project.");
            var equipmentProject = await db.AreaEquipment.AsNoTracking().Where(x => x.Id == task.AreaEquipmentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct);
            if (equipmentProject != projectId) throw new UnauthorizedAccessException("Task equipment does not belong to its plan project.");
            var actorId = actorContext.UserId ?? Guid.Empty;
            var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct) ?? throw new UnauthorizedAccessException("Actor is unavailable.");
            var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId, ct) ?? throw new UnauthorizedAccessException("Owning project is unavailable.");
            var permitted = actor.Role switch
            {
                UserRole.Admin => true,
                UserRole.Manager => project.CreatedByUserId == actor.Id || await db.Users.AnyAsync(q => q.Id == project.AssignedQcId && q.ManagerId == actor.Id, ct),
                UserRole.Surveyor => project.AssignedSurveyorId == actor.Id && task.AssignedUserId == actor.Id,
                UserRole.Qc => project.AssignedQcId == actor.Id,
                UserRole.Client => project.AssignedClientId == actor.Id,
                _ => false
            };
            if (!permitted) throw new UnauthorizedAccessException("Task is outside the actor's assigned project scope.");
            return task;
        }
        var ownership = await activityOwnership.ResolveTaskAsync(taskId, ct);
        if (!ownership.IsResolved || ownership.TenantId != tenantContext.Current?.TenantId)
            throw new UnauthorizedAccessException("Inspection task ownership is missing, conflicting, or outside the authorized tenant.");
        return task;
    }

    public async Task<(InspectionActivity Activity, InspectionTask Task)> EnsureActivityScopeAsync(Guid activityId, CancellationToken ct)
    {
        var activity = await db.InspectionActivities.SingleOrDefaultAsync(x => x.Id == activityId, ct)
            ?? throw new KeyNotFoundException("Inspection activity was not found.");
        var task = await EnsureTaskScopeAsync(activity.InspectionTaskId, ct);
        return (activity, task);
    }

    public async Task<(CorrectiveAction Action, ActivityFinding Finding, InspectionActivity Activity, InspectionTask Task)> EnsureActionScopeAsync(Guid actionId, CancellationToken ct)
    {
        var row = await (from action in db.CorrectiveActions
                         join finding in db.ActivityFindings on action.ActivityFindingId equals finding.Id
                         join activity in db.InspectionActivities on finding.InspectionActivityId equals activity.Id
                         where action.Id == actionId
                         select new { Action = action, Finding = finding, Activity = activity }).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Corrective action was not found.");
        var task = await EnsureTaskScopeAsync(row.Activity.InspectionTaskId, ct);
        return (row.Action, row.Finding, row.Activity, task);
    }

    public Task<(TenantResourceOwnership? Ownership, string FailureCode)> ResolveFileScopeAsync(Guid fileId, CancellationToken ct) =>
        tenantResolver.ResolveResourceAsync(TenantResourceKind.SurveyFile, fileId, ct);

    public async Task EnsureActivityFileAccessAsync(Guid fileId, Guid actorId, InspectionTenantOwnership taskScope, CancellationToken ct)
    {
        if (tenantOptions.Value.Enabled)
        {
            if (!taskScope.IsResolved) throw new UnauthorizedAccessException("Activity ownership must be resolved before attaching evidence.");
            var (ownership, failure) = await ResolveFileScopeAsync(fileId, ct);
            if (ownership is null || ownership.TenantId != taskScope.TenantId) throw new UnauthorizedAccessException($"Evidence file is outside the activity tenant scope ({failure}).");
            return;
        }
        if (!await db.Files.AsNoTracking().AnyAsync(x => x.Id == fileId && x.UploadedById == actorId, ct))
            throw new UnauthorizedAccessException("Evidence file was not uploaded by this authenticated actor.");
    }
}

public sealed class InspectionActivityApplicationService(
    SurveyDbContext db,
    InspectionActivityAuthorizer authorizer,
    IInspectionActivityActorContext actorContext,
    IInspectionTenantOwnershipResolver ownershipResolver,
    ICorrectiveActionPolicy closurePolicy,
    IOptions<InspectionActivityOptions> options,
    TimeProvider clock) : IInspectionActivityApplicationService
{
    private Guid ActorId => actorContext.UserId ?? throw new UnauthorizedAccessException("Authenticated actor is required.");
    private void EnsureEnabled() { if (!options.Value.Enabled) throw new InspectionFeatureInactiveException(); }

    public async Task<InspectionPlanResponse> CreatePlanAsync(CreateInspectionPlanRequest request, CancellationToken ct = default)
    {
        EnsureEnabled(); InspectionActivityContractValidation.Validate(request);
        await authorizer.RequireActorAsync(ActorId, ct, UserRole.Admin, UserRole.Manager);
        await authorizer.EnsureProjectScopeAsync(request.ProjectId, ct);
        var plan = new InspectionPlan { ProjectId = request.ProjectId, Name = request.Name.Trim(), SourceReference = request.SourceReference?.Trim(), StartsAt = request.StartsAt, EndsAt = request.EndsAt, CreatedAt = clock.GetUtcNow() };
        db.InspectionPlans.Add(plan); await db.SaveChangesAsync(ct); return new(plan.Id, request.ProjectId, plan.Name, plan.SourceReference, plan.StartsAt, plan.EndsAt);
    }

    public async Task<InspectionTaskResponse> CreateTaskAsync(CreateInspectionTaskRequest request, CancellationToken ct = default)
    {
        EnsureEnabled(); InspectionActivityContractValidation.Validate(request);
        await authorizer.RequireActorAsync(ActorId, ct, UserRole.Admin, UserRole.Manager);
        var plan = await db.InspectionPlans.SingleOrDefaultAsync(x => x.Id == request.PlanId, ct) ?? throw new KeyNotFoundException("Inspection plan was not found.");
        if (plan.ProjectId is not Guid projectId) throw new UnauthorizedAccessException("A project-bound plan is required.");
        await authorizer.EnsureProjectScopeAsync(projectId, ct);
        var equipmentProjectId = await ResolveEquipmentProjectAsync(request.AreaEquipmentId, request.LegacyEquipmentId, ct);
        if (equipmentProjectId != projectId) throw new UnauthorizedAccessException("Task equipment does not belong to its plan project.");
        if (request.AssignedUserId is Guid assigned && !await db.Users.AnyAsync(x => x.Id == assigned && x.IsActive && x.ApprovalStatus == ApprovalStatus.Approved && x.Role == UserRole.Surveyor, ct))
            throw new ArgumentException("Assigned user must be an active, approved Surveyor.");
        var task = new InspectionTask { InspectionPlanId = request.PlanId, AreaEquipmentId = request.AreaEquipmentId, LegacyEquipmentId = request.LegacyEquipmentId, Kind = request.Kind, ScheduleReference = request.ScheduleReference?.Trim(), WorkPackageReference = request.WorkPackageReference?.Trim(), AssignedUserId = request.AssignedUserId, ScheduledAt = request.ScheduledAt };
        db.InspectionTasks.Add(task); await db.SaveChangesAsync(ct); return new(task.Id, task.InspectionPlanId, task.AreaEquipmentId, task.LegacyEquipmentId, task.Kind, task.ScheduleReference, task.WorkPackageReference, task.AssignedUserId, task.ScheduledAt, task.StatusCode);
    }

    public async Task<InspectionActivityResponse> CreateActivityAsync(Guid taskId, CreateInspectionActivityRequest request, CancellationToken ct = default)
    {
        EnsureEnabled(); InspectionActivityContractValidation.Validate(request);
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Admin, UserRole.Manager);
        var task = await authorizer.EnsureTaskScopeAsync(taskId, ct);
        if (actor.Role == UserRole.Surveyor && task.AssignedUserId != actor.Id) throw new UnauthorizedAccessException("Surveyor is not assigned to this task.");
        var checklist = await db.ChecklistVersionSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ChecklistVersionId, ct)
            ?? throw new ArgumentException("Checklist version was not found.");
        InspectionActivityRules.ValidateGradeChecklist(request.Grade, checklist.InspectionType);
        var activity = InspectionActivityRules.Create(new(task.Id, request.Grade, actor.Id, request.StartedAt, request.CompletedAt, checklist.Id, actor.DisplayName));
        var revision = new InspectionActivityRevision { InspectionActivityId = activity.Id, RevisionNumber = 1, ContentHash = HashActivity(activity), CreatedByUserId = actor.Id, CreatedAt = clock.GetUtcNow() };
        activity.UpdatedAt = clock.GetUtcNow();
        db.InspectionActivities.Add(activity); db.InspectionActivityRevisions.Add(revision);
        await db.SaveChangesAsync(ct);
        return Map(activity);
    }

    public async Task<CheckpointResponseResponse> SaveCheckpointResponseAsync(Guid activityId, SaveCheckpointResponseRequest request, CancellationToken ct = default)
    {
        EnsureEnabled(); InspectionActivityContractValidation.Validate(request);
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Admin, UserRole.Manager);
        var (activity, task) = await authorizer.EnsureActivityScopeAsync(activityId, ct);
        EnsureTaskWriter(task, actor);
        EnsureEditable(activity);
        RequireExpectedVersion(activity, request.ActivityRowVersion);
        var definition = await db.CheckpointDefinitionSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.CheckpointDefinitionSnapshotId, ct)
            ?? throw new ArgumentException("Checkpoint version was not found.");
        if (definition.ChecklistVersionSnapshotId != activity.ChecklistVersionId) throw new ArgumentException("Checkpoint does not belong to this activity's checklist version.");
        if (definition.RequireRemarkOnFail && string.Equals(request.ResponseCode, "Fail", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(request.Observation)) throw new ArgumentException("A remark is required for this failed checkpoint.");
        if (definition.NumericReadingEnabled && definition.NumericReadingRequired && request.NumericValue is null) throw new ArgumentException("A numeric reading is required for this checkpoint.");
        if (!definition.NumericReadingEnabled && request.NumericValue is not null) throw new ArgumentException("This checkpoint is not configured to accept a numeric reading.");
        var activityRevision = await db.InspectionActivityRevisions.Where(x => x.InspectionActivityId == activityId).OrderByDescending(x => x.RevisionNumber).FirstAsync(ct);
        var next = await db.CheckpointResponseRevisions.Where(x => x.ActivityRevisionId == activityRevision.Id && x.CheckpointDefinitionSnapshotId == definition.Id).Select(x => (int?)x.RevisionNumber).MaxAsync(ct) ?? 0;
        var response = new CheckpointResponseRevision { ActivityRevisionId = activityRevision.Id, CheckpointDefinitionSnapshotId = definition.Id, RevisionNumber = checked(next + 1), ApplicabilityResultCode = request.ApplicabilityResultCode?.Trim(), ResponseCode = request.ResponseCode?.Trim(), TechnicalConditionCode = request.TechnicalConditionCode?.Trim(), Observation = request.Observation?.Trim(), NumericValue = request.NumericValue, MeasurementUnitSnapshot = request.NumericValue.HasValue ? definition.MeasurementUnit : null, RecordedAt = clock.GetUtcNow(), RecordedByUserId = actor.Id };
        InspectionActivityRules.ValidateCheckpointRevision(response);
        activity.UpdatedAt = clock.GetUtcNow();
        db.CheckpointResponseRevisions.Add(response);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new InspectionRequestConflictException("Activity changed concurrently; reload and retry with the latest row version.", ex); }
        var outOfRange = response.NumericValue.HasValue && (definition.MinimumReading.HasValue && response.NumericValue < definition.MinimumReading || definition.MaximumReading.HasValue && response.NumericValue > definition.MaximumReading);
        return new(response.Id, definition.Id, response.RevisionNumber, response.ResponseCode, response.TechnicalConditionCode, response.NumericValue, response.MeasurementUnitSnapshot, definition.NumericReadingEnabled ? outOfRange : null, response.WorkflowState, response.RecordedAt);
    }

    public async Task<ActivityFindingResponse> AddFindingAsync(Guid activityId, AddActivityFindingRequest request, CancellationToken ct = default)
    {
        EnsureEnabled(); InspectionActivityContractValidation.Validate(request);
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Admin, UserRole.Manager);
        var (activity, task) = await authorizer.EnsureActivityScopeAsync(activityId, ct); EnsureTaskWriter(task, actor); EnsureEditable(activity);
        var checkpoint = await db.CheckpointResponseRevisions.SingleOrDefaultAsync(x => x.Id == request.CheckpointResponseRevisionId, ct)
            ?? throw new ArgumentException("Checkpoint response was not found.");
        var revisionBelongs = await db.InspectionActivityRevisions.AnyAsync(x => x.Id == checkpoint.ActivityRevisionId && x.InspectionActivityId == activityId, ct);
        if (!revisionBelongs) throw new UnauthorizedAccessException("Checkpoint response belongs to another activity.");
        InspectionActivityRules.EnsureFindingUsesFailedCheckpoint(checkpoint);
        if (request.ResponsibleUserId is Guid responsible && !await db.Users.AnyAsync(x => x.Id == responsible && x.IsActive && x.ApprovalStatus == ApprovalStatus.Approved, ct)) throw new ArgumentException("Responsible user is inactive or unapproved.");
        var finding = new ActivityFinding { InspectionActivityId = activityId, CheckpointResponseRevisionId = checkpoint.Id, Observation = request.Observation.Trim(), ProposedAction = request.ProposedAction?.Trim(), SeverityCode = request.SeverityCode?.Trim(), FaultCode = request.FaultCode?.Trim(), RequiredMaterialsOrSupport = request.RequiredMaterialsOrSupport?.Trim(), ResponsibleUserId = request.ResponsibleUserId, ResponsibleParty = request.ResponsibleParty?.Trim(), ReportedAt = clock.GetUtcNow(), ReportedByUserId = actor.Id, DueAt = request.DueAt };
        db.ActivityFindings.Add(finding); activity.UpdatedAt = clock.GetUtcNow(); await db.SaveChangesAsync(ct);
        return new(finding.Id, activity.Id, checkpoint.Id, finding.StatusCode);
    }

    public async Task<ActivityEvidenceResponse> AddEvidenceAsync(Guid activityId, AddActivityEvidenceRequest request, CancellationToken ct = default)
    {
        EnsureEnabled();
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Qc, UserRole.Manager, UserRole.Admin);
        var (_, task) = await authorizer.EnsureActivityScopeAsync(activityId, ct);
        EnsureTaskWriter(task, actor);
        var taskOwnership = await ownershipResolver.ResolveTaskAsync(task.Id, ct);
        await authorizer.EnsureActivityFileAccessAsync(request.SurveyFileId, actor.Id, taskOwnership, ct);
        var evidenceTargets = (request.CheckpointResponseRevisionId.HasValue ? 1 : 0) + (request.CorrectiveActionId.HasValue ? 1 : 0) + (request.CorrectiveActionEventId.HasValue ? 1 : 0);
        if (evidenceTargets != 1) throw new ArgumentException("Evidence must reference exactly one checkpoint response, corrective action, or action event.");
        if (request.CheckpointResponseRevisionId is Guid responseId && !await (from response in db.CheckpointResponseRevisions join revision in db.InspectionActivityRevisions on response.ActivityRevisionId equals revision.Id where response.Id == responseId && revision.InspectionActivityId == activityId select response.Id).AnyAsync(ct))
            throw new UnauthorizedAccessException("Checkpoint evidence target belongs to another activity.");
        CorrectiveActionEvent? generatedActionEvent = null;
        if (request.CorrectiveActionId is Guid actionId)
        {
            var action = await (from ca in db.CorrectiveActions join finding in db.ActivityFindings on ca.ActivityFindingId equals finding.Id where ca.Id == actionId && finding.InspectionActivityId == activityId select ca).SingleOrDefaultAsync(ct)
                ?? throw new UnauthorizedAccessException("Corrective action belongs to another activity.");
            generatedActionEvent = new CorrectiveActionEvent { CorrectiveActionId = action.Id, Kind = CorrectiveActionEventKind.EvidenceAdded, ResultingStatus = action.Status, ActorUserId = actor.Id, Comment = request.Caption?.Trim(), OccurredAt = clock.GetUtcNow() };
            db.CorrectiveActionEvents.Add(generatedActionEvent);
        }
        if (request.CorrectiveActionEventId is Guid eventId && !await (from ev in db.CorrectiveActionEvents join action in db.CorrectiveActions on ev.CorrectiveActionId equals action.Id join finding in db.ActivityFindings on action.ActivityFindingId equals finding.Id where ev.Id == eventId && finding.InspectionActivityId == activityId select ev.Id).AnyAsync(ct))
            throw new UnauthorizedAccessException("Corrective-action evidence target belongs to another activity.");
        var evidence = new ActivityEvidence { SurveyFileId = request.SurveyFileId, InspectionActivityId = activityId, AreaEquipmentId = task.AreaEquipmentId, LegacyEquipmentId = task.LegacyEquipmentId, CheckpointResponseRevisionId = request.CheckpointResponseRevisionId, CorrectiveActionEventId = generatedActionEvent?.Id ?? request.CorrectiveActionEventId, Role = request.Role, Caption = request.Caption?.Trim(), IntegrityHash = request.IntegrityHash?.Trim(), AddedByUserId = actor.Id, AddedAt = clock.GetUtcNow() };
        db.ActivityEvidence.Add(evidence); await db.SaveChangesAsync(ct); return new(evidence.Id, evidence.SurveyFileId, evidence.CheckpointResponseRevisionId, evidence.CorrectiveActionEventId, evidence.Role, evidence.AddedAt);
    }

    public async Task<CorrectiveActionResponse> CreateCorrectiveActionAsync(CreateCorrectiveActionRequest request, CancellationToken ct = default)
    {
        EnsureEnabled(); InspectionActivityContractValidation.Validate(request);
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Admin, UserRole.Manager);
        var finding = await db.ActivityFindings.SingleOrDefaultAsync(x => x.Id == request.ActivityFindingId, ct) ?? throw new KeyNotFoundException("Finding was not found.");
        var (_, task) = await authorizer.EnsureActivityScopeAsync(finding.InspectionActivityId, ct); EnsureTaskWriter(task, actor);
        if (await db.CorrectiveActions.AnyAsync(x => x.ActionReference == request.ActionReference.Trim(), ct)) throw new ArgumentException("Action reference already exists.");
        if (request.ResponsibleUserId is Guid responsible && !await db.Users.AnyAsync(x => x.Id == responsible && x.IsActive && x.ApprovalStatus == ApprovalStatus.Approved, ct)) throw new ArgumentException("Responsible user is inactive or unapproved.");
        var action = new CorrectiveAction { ActivityFindingId = finding.Id, ActionReference = request.ActionReference.Trim(), SupplementaryInformation = request.SupplementaryInformation?.Trim(), ProposedAction = request.ProposedAction?.Trim(), ExternalWorkOrderReference = request.ExternalWorkOrderReference?.Trim(), ResponsibleUserId = request.ResponsibleUserId ?? finding.ResponsibleUserId, ResponsibleParty = request.ResponsibleParty?.Trim() ?? finding.ResponsibleParty };
        db.CorrectiveActions.Add(action); db.CorrectiveActionEvents.Add(new CorrectiveActionEvent { CorrectiveActionId = action.Id, Kind = CorrectiveActionEventKind.Created, ResultingStatus = action.Status, ActorUserId = actor.Id, OccurredAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); return Map(action);
    }

    public async Task<CorrectiveActionResponse> ReportRectificationAsync(Guid actionId, CorrectiveActionReportRequest request, CancellationToken ct = default)
    {
        EnsureEnabled(); InspectionActivityContractValidation.Validate(request);
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Manager, UserRole.Admin);
        var (action, _, _, task) = await authorizer.EnsureActionScopeAsync(actionId, ct);
        EnsureTaskWriter(task, actor);
        if (action.Status is CorrectiveActionStatus.ClientClosed or CorrectiveActionStatus.SubmittedForVerification or CorrectiveActionStatus.Verified) throw new InvalidOperationException("This corrective action cannot be changed in its current state.");
        if (actor.Role == UserRole.Surveyor && action.ResponsibleUserId != actor.Id && task.AssignedUserId != actor.Id) throw new UnauthorizedAccessException("Surveyor is not responsible for this corrective action.");
        action.RectificationDescription = request.Description.Trim();
        action.ExternalWorkOrderReference = request.WorkOrderReference?.Trim() ?? action.ExternalWorkOrderReference;
        action.RectificationAt = request.RectificationAt;
        if (request.ResponsibleUserId.HasValue) action.ResponsibleUserId = request.ResponsibleUserId;
        if (request.ResponsibleUserId is Guid userId && !await db.Users.AnyAsync(x => x.Id == userId && x.IsActive && x.ApprovalStatus == ApprovalStatus.Approved, ct)) throw new ArgumentException("Responsible user is inactive or unapproved.");
        action.Status = CorrectiveActionStatus.InProgress;
        var auditPrefix = $"Rectification date {request.RectificationAt:O}; work order {request.WorkOrderReference?.Trim() ?? action.ExternalWorkOrderReference ?? "—"}. ";
        var auditComment = auditPrefix + (request.Comments?.Trim() ?? "");
        if (auditComment.Length > 4000) auditComment = auditComment[..4000];
        db.CorrectiveActionEvents.Add(new CorrectiveActionEvent { CorrectiveActionId = action.Id, Kind = CorrectiveActionEventKind.Commented, ResultingStatus = action.Status, ActorUserId = actor.Id, Comment = auditComment, RectificationDescription = request.Description.Trim(), RectificationAt = request.RectificationAt, WorkOrderReference = request.WorkOrderReference?.Trim() ?? action.ExternalWorkOrderReference, ResponsibleParty = action.ResponsibleParty ?? action.ResponsibleUserId?.ToString("D"), OccurredAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); return Map(action);
    }

    public async Task<InspectionActivityResponse> SubmitForReviewAsync(Guid activityId, CancellationToken ct = default)
    {
        EnsureEnabled(); var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Admin, UserRole.Manager);
        var (activity, task) = await authorizer.EnsureActivityScopeAsync(activityId, ct); EnsureTaskWriter(task, actor); EnsureEditable(activity);
        activity.CompletedAt ??= clock.GetUtcNow();
        var required = await db.CheckpointDefinitionSnapshots.Where(x => x.ChecklistVersionSnapshotId == activity.ChecklistVersionId && x.IsRequired).Select(x => x.Id).ToListAsync(ct);
        var latestRevision = await db.InspectionActivityRevisions.Where(x => x.InspectionActivityId == activityId).OrderByDescending(x => x.RevisionNumber).Select(x => x.Id).FirstAsync(ct);
        var answered = await db.CheckpointResponseRevisions.Where(x => x.ActivityRevisionId == latestRevision).Select(x => x.CheckpointDefinitionSnapshotId).Distinct().ToListAsync(ct);
        if (required.Except(answered).Any()) throw new InvalidOperationException("All required checkpoints need a response before submission.");
        activity.ReviewStatus = InspectionActivityReviewStatus.Submitted; activity.UpdatedAt = clock.GetUtcNow();
        db.ActivityReviewEvents.Add(new ActivityReviewEvent { InspectionActivityId = activity.Id, Decision = activity.ReviewStatus, ActorUserId = actor.Id, Comments = "Submitted for review", DecidedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); return Map(activity);
    }

    public async Task<InspectionActivityResponse> RecordReviewAsync(Guid activityId, ActivityReviewDecisionRequest request, CancellationToken ct = default)
    {
        EnsureEnabled(); var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Qc, UserRole.Manager, UserRole.Admin);
        var (activity, task) = await authorizer.EnsureActivityScopeAsync(activityId, ct);
        if (activity.ReviewStatus is not (InspectionActivityReviewStatus.Submitted or InspectionActivityReviewStatus.InReview)) throw new InvalidOperationException("Activity is not awaiting review.");
        if (request.Decision is not (InspectionActivityReviewStatus.InReview or InspectionActivityReviewStatus.ChangesRequested or InspectionActivityReviewStatus.Accepted)) throw new ArgumentException("Review decision is invalid.");
        if (request.Decision == InspectionActivityReviewStatus.ChangesRequested && string.IsNullOrWhiteSpace(request.Comments)) throw new ArgumentException("A reason is required when returning an activity.");
        var ev = InspectionActivityRules.RecordDecision(activity, request.Decision, actor.Id, request.Comments?.Trim(), clock.GetUtcNow());
        if (task.WorkPackageId.HasValue && request.Decision == InspectionActivityReviewStatus.Accepted) task.StatusCode = "Completed";
        else if (task.WorkPackageId.HasValue && request.Decision == InspectionActivityReviewStatus.ChangesRequested) task.StatusCode = "InProgress";
        activity.UpdatedAt = clock.GetUtcNow(); db.ActivityReviewEvents.Add(ev); await db.SaveChangesAsync(ct);
        return Map(activity);
    }

    public async Task<CheckpointReviewEvent> RecordCheckpointReviewAsync(Guid activityId, CheckpointReviewRequest request, CancellationToken ct = default)
    {
        EnsureEnabled();
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Qc, UserRole.Manager, UserRole.Admin);
        var (activity, _) = await authorizer.EnsureActivityScopeAsync(activityId, ct);
        if (activity.ReviewStatus is not (InspectionActivityReviewStatus.Submitted or InspectionActivityReviewStatus.InReview)) throw new InvalidOperationException("Checkpoint review requires an activity awaiting QC review.");
        if (!Enum.IsDefined(request.Decision)) throw new ArgumentException("Checkpoint review decision is invalid.");
        if (request.Decision == CheckpointReviewDecision.ChangesRequested && string.IsNullOrWhiteSpace(request.Comments)) throw new ArgumentException("A reason is required when returning a checkpoint.");
        var responseBelongs = await (from response in db.CheckpointResponseRevisions join revision in db.InspectionActivityRevisions on response.ActivityRevisionId equals revision.Id where response.Id == request.CheckpointResponseRevisionId && revision.InspectionActivityId == activityId select response.Id).AnyAsync(ct);
        if (!responseBelongs) throw new UnauthorizedAccessException("Checkpoint response belongs to another activity.");
        var review = new CheckpointReviewEvent { InspectionActivityId = activityId, CheckpointResponseRevisionId = request.CheckpointResponseRevisionId, ReviewerUserId = actor.Id, Decision = request.Decision, Comments = request.Comments?.Trim(), ReviewedAt = clock.GetUtcNow() };
        db.CheckpointReviewEvents.Add(review); await db.SaveChangesAsync(ct); return review;
    }

    public async Task<CorrectiveActionResponse> VerifyCorrectiveActionAsync(Guid actionId, CorrectiveActionVerificationRequest request, CancellationToken ct = default)
    {
        EnsureEnabled(); var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Qc, UserRole.Manager, UserRole.Admin);
        var (action, _, _, _) = await authorizer.EnsureActionScopeAsync(actionId, ct);
        if (action.Status is not CorrectiveActionStatus.SubmittedForVerification) throw new InvalidOperationException("Corrective action is not ready for Aries verification.");
        if (!request.AcceptCorrection && string.IsNullOrWhiteSpace(request.Comments)) throw new ArgumentException("A reason is required when rejecting rectification.");
        action.Status = request.AcceptCorrection ? CorrectiveActionStatus.Verified : CorrectiveActionStatus.Reopened; action.AriesVerifierUserId = actor.Id; action.AriesVerifiedAt = clock.GetUtcNow(); action.LastDecisionComment = request.Comments?.Trim();
        db.CorrectiveActionEvents.Add(new CorrectiveActionEvent { CorrectiveActionId = action.Id, Kind = request.AcceptCorrection ? CorrectiveActionEventKind.Verified : CorrectiveActionEventKind.VerificationRejected, ResultingStatus = action.Status, ActorUserId = actor.Id, Comment = request.Comments?.Trim(), OccurredAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); return Map(action);
    }

    public async Task<CorrectiveActionResponse> SubmitCorrectiveActionForVerificationAsync(Guid actionId, CancellationToken ct = default)
    {
        EnsureEnabled(); var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Manager, UserRole.Admin);
        var (action, _, _, task) = await authorizer.EnsureActionScopeAsync(actionId, ct);
        if (actor.Role == UserRole.Surveyor && action.ResponsibleUserId != actor.Id && task.AssignedUserId != actor.Id)
            throw new UnauthorizedAccessException("Surveyor is not responsible for this action or assigned to its task.");
        if (action.Status is not (CorrectiveActionStatus.Proposed or CorrectiveActionStatus.Assigned or CorrectiveActionStatus.InProgress or CorrectiveActionStatus.ClientRejected or CorrectiveActionStatus.Reopened))
            throw new InvalidOperationException("Corrective action cannot be submitted from its current state.");
        var hasCorrectionEvidence = await (from evidence in db.ActivityEvidence
                                           join ev in db.CorrectiveActionEvents on evidence.CorrectiveActionEventId equals ev.Id
                                           where ev.CorrectiveActionId == actionId && (evidence.Role == ActivityEvidenceRole.After || evidence.Role == ActivityEvidenceRole.Correction)
                                           select evidence.Id).AnyAsync(ct);
        if (!hasCorrectionEvidence) throw new InvalidOperationException("After/correction evidence is required before verification submission.");
        action.Status = CorrectiveActionStatus.SubmittedForVerification;
        db.CorrectiveActionEvents.Add(new CorrectiveActionEvent { CorrectiveActionId = action.Id, Kind = CorrectiveActionEventKind.SubmittedForVerification, ResultingStatus = action.Status, ActorUserId = actor.Id, OccurredAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); return Map(action);
    }

    public async Task<CorrectiveActionResponse> DecideClientClosureAsync(Guid actionId, ClientClosureDecisionRequest request, CancellationToken ct = default)
    {
        EnsureEnabled(); var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Client);
        var (action, _, _, task) = await authorizer.EnsureActionScopeAsync(actionId, ct);
        var owner = await ownershipResolver.ResolveTaskAsync(task.Id, ct);
        if (!owner.IsResolved || owner.ProjectId is not Guid projectId || !await db.Projects.AnyAsync(x => x.Id == projectId && x.AssignedClientId == actor.Id, ct))
            throw new UnauthorizedAccessException("Only the client assigned to the equipment's owning project may decide closure.");
        CorrectiveActionStatus next;
        CorrectiveActionEventKind eventKind;
        if (request.AcceptClosure)
        {
            closurePolicy.EnsureClientMayClose(actor.Role, isAssignedProjectClient: true, action.Status);
            next = CorrectiveActionStatus.ClientClosed; eventKind = CorrectiveActionEventKind.ClientAccepted;
        }
        else
        {
            if (action.Status == CorrectiveActionStatus.ClientClosed) { next = CorrectiveActionStatus.Reopened; eventKind = CorrectiveActionEventKind.Reopened; }
            else if (action.Status == CorrectiveActionStatus.Verified) { next = CorrectiveActionStatus.ClientRejected; eventKind = CorrectiveActionEventKind.ClientRejected; }
            else throw new InvalidOperationException("Client rejection requires Aries verification; reopening requires a closed action.");
            if (string.IsNullOrWhiteSpace(request.Comments)) throw new ArgumentException("A reason is required for rejection or reopening.");
        }
        action.Status = next; action.ClientDecisionUserId = actor.Id; action.ClientDecisionAt = clock.GetUtcNow(); action.LastDecisionComment = request.Comments?.Trim();
        action.IsOutstanding = next != CorrectiveActionStatus.ClientClosed;
        db.CorrectiveActionEvents.Add(new CorrectiveActionEvent { CorrectiveActionId = action.Id, Kind = eventKind, ResultingStatus = next, ActorUserId = actor.Id, Comment = request.Comments?.Trim(), OccurredAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); return Map(action);
    }

    private async Task<Guid?> ResolveEquipmentProjectAsync(Guid? pinId, Guid? legacyId, CancellationToken ct)
    {
        if (pinId is Guid modern) return await db.AreaEquipment.AsNoTracking().Where(x => x.Id == modern).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Equipment pin was not found.");
        if (legacyId is Guid legacy)
        {
            var maps = await db.LegacyEntityMaps.AsNoTracking().Where(x => x.SourceEntityType == "Equipment" && x.SourceRecordKey == legacy.ToString("D") && x.TargetEntityType == "Project" && x.Status == LegacyMappingStatus.Verified && x.TargetId.HasValue).Select(x => x.TargetId!.Value).Distinct().ToListAsync(ct);
            if (maps.Count != 1) throw new UnauthorizedAccessException("Legacy equipment has no unique verified project mapping.");
            return maps[0];
        }
        throw new ArgumentException("Exactly one equipment ID is required.");
    }

    private static void EnsureTaskWriter(InspectionTask task, User actor)
    {
        if (actor.Role == UserRole.Surveyor && task.AssignedUserId != actor.Id) throw new UnauthorizedAccessException("Surveyor is not assigned to this inspection task.");
    }
    private static void EnsureEditable(InspectionActivity activity)
    {
        if (activity.ReviewStatus is not (InspectionActivityReviewStatus.Draft or InspectionActivityReviewStatus.ChangesRequested))
            InspectionActivityRules.EnsureRevisionAllowed(activity.ReviewStatus);
        if (activity.ReviewStatus is InspectionActivityReviewStatus.Submitted or InspectionActivityReviewStatus.InReview)
            throw new InspectionRequestConflictException("Submitted activity is locked while under review.");
    }
    private static string HashActivity(InspectionActivity activity) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{activity.Id:D}|{activity.InspectionTaskId:D}|{activity.Grade}|{activity.ChecklistVersionId:D}|{activity.StartedAt:O}")));
    private static void RequireExpectedVersion(InspectionActivity activity, string rowVersion)
    {
        InspectionActivityRules.EnsureExpectedRowVersion(activity.RowVersion, rowVersion);
    }
    private static InspectionActivityResponse Map(InspectionActivity x) => new(x.Id, x.InspectionTaskId, x.Grade, x.ChecklistVersionId, x.ReviewStatus, x.StartedAt, x.CompletedAt, Convert.ToBase64String(x.RowVersion));
    private static CorrectiveActionResponse Map(CorrectiveAction x) => new(x.Id, x.ActionReference, x.Status, x.CreatedAt);
}

public static class InspectionActivityServiceCollectionExtensions
{
    public static IServiceCollection AddInspectionActivityFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InspectionActivityOptions>(configuration.GetSection("InspectionActivities"));
        services.AddScoped<IInspectionActivityActorContext, HttpInspectionActivityActorContext>();
        services.AddScoped<IInspectionTenantOwnershipResolver, InspectionTenantOwnershipResolver>();
        services.AddSingleton<ICorrectiveActionPolicy, CorrectiveActionPolicy>();
        services.AddScoped<InspectionActivityAuthorizer>();
        services.AddScoped<IInspectionActivityApplicationService, InspectionActivityApplicationService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
