using System.Security.Claims;
using Ex_SurveyApp_api.Contracts.V1;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ex_SurveyApp_api.Controllers;

/// <summary>Versioned additive workflow. Existing Surveyor/QC routes are unchanged.</summary>
[ApiController]
[Authorize]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
[Route("api/v1/inspection-activities")]
public sealed partial class InspectionActivitiesController(SurveyDbContext db, IInspectionActivityApplicationService activities, IOptions<InspectionActivityOptions> options, IProjectPhotoStorage photoStorage) : ControllerBase
{
    private Guid ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpGet("equipment/{equipmentId:guid}")]
    [Authorize(Roles = "Surveyor,Manager,Admin,Qc")]
    public async Task<IActionResult> History(Guid equipmentId, CancellationToken ct, [FromQuery] bool categoryOnly = false)
    {
        if (categoryOnly && !User.IsInRole(nameof(UserRole.Admin)) && !User.IsInRole(nameof(UserRole.Surveyor))) return Forbid();
        var projectId = await db.AreaEquipment.AsNoTracking().Where(x => x.Id == equipmentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct);
        if (projectId is null || !await CanAccessProject(projectId.Value, ct)) return NotFound();
        var rows = await (from task in db.InspectionTasks.AsNoTracking()
                          join plan in db.InspectionPlans.AsNoTracking() on task.InspectionPlanId equals plan.Id
                          join activity in db.InspectionActivities.AsNoTracking() on task.Id equals activity.InspectionTaskId
                          where task.AreaEquipmentId == equipmentId && plan.ProjectId == projectId
                                && (categoryOnly ? activity.Grade == null : activity.Grade != null)
                          orderby activity.StartedAt descending
                          select new { activity.Id, activity.Grade, activity.ReviewStatus, activity.StartedAt, activity.CompletedAt, activity.InspectorNameSnapshot, activity.ChecklistVersionId, activity.RowVersion })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("{activityId:guid}")]
    [Authorize(Roles = "Surveyor,Manager,Admin,Qc")]
    public async Task<IActionResult> Detail(Guid activityId, CancellationToken ct)
    {
        var activity = await db.InspectionActivities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == activityId, ct);
        if (activity is null) return NotFound();
        var projectId = await (from task in db.InspectionTasks join plan in db.InspectionPlans on task.InspectionPlanId equals plan.Id where task.Id == activity.InspectionTaskId select plan.ProjectId).SingleAsync(ct);
        if (projectId is null || !await CanAccessProject(projectId.Value, ct)) return NotFound();
        var checkpoints = await (from definition in db.CheckpointDefinitionSnapshots.AsNoTracking()
                                 where definition.ChecklistVersionSnapshotId == activity.ChecklistVersionId
                                 orderby definition.SortOrder
                                 select new { definition.Id, definition.QuestionCode, definition.QuestionText, definition.IsRequired, definition.SortOrder, definition.SectionCode, definition.RequireRemarkOnFail, definition.ResponseScaleCode, definition.ApplicabilityCode, definition.NumericReadingEnabled, definition.NumericReadingRequired, definition.MeasurementUnit, definition.MinimumReading, definition.MaximumReading })
            .ToListAsync(ct);
        var checklist = await db.ChecklistVersionSnapshots.AsNoTracking().Where(x => x.Id == activity.ChecklistVersionId)
            .Select(x => new {x.StandardReference, x.StandardEdition, x.ControlledSourceReference, x.ProtectionTypeSnapshot, x.VersionNumber}).SingleAsync(ct);
        var responses = await (from response in db.CheckpointResponseRevisions.AsNoTracking()
                                join revision in db.InspectionActivityRevisions.AsNoTracking() on response.ActivityRevisionId equals revision.Id
                                join definition in db.CheckpointDefinitionSnapshots.AsNoTracking() on response.CheckpointDefinitionSnapshotId equals definition.Id
                                where revision.InspectionActivityId == activityId
                                orderby response.RevisionNumber descending
                                select new { response.CheckpointDefinitionSnapshotId, response.Id, response.RevisionNumber, response.ResponseCode, response.GradeResultsJson, response.InspectionComment, response.Observation, response.NumericValue, response.MeasurementUnitSnapshot, response.RecordedAt, IsOutOfRange = definition.NumericReadingEnabled && response.NumericValue.HasValue && ((definition.MinimumReading.HasValue && response.NumericValue < definition.MinimumReading) || (definition.MaximumReading.HasValue && response.NumericValue > definition.MaximumReading)) })
            .ToListAsync(ct);
        var findings = await db.ActivityFindings.AsNoTracking().Where(x => x.InspectionActivityId == activityId).Select(x => new { x.Id, x.CheckpointResponseRevisionId, x.Observation, x.ProposedAction, x.SeverityCode, x.FaultCode, x.RequiredMaterialsOrSupport, x.ResponsibleUserId, x.StatusCode, x.ReportedAt, x.ReportedByUserId, x.DueAt }).ToListAsync(ct);
        var findingIds = findings.Select(x => x.Id).ToArray();
        var actionRows = await db.CorrectiveActions.AsNoTracking().Where(x => findingIds.Contains(x.ActivityFindingId)).Select(x => new { x.Id, x.ActivityFindingId, x.ActionReference, x.SupplementaryInformation, x.ProposedAction, x.RectificationDescription, x.ExternalWorkOrderReference, x.ResponsibleUserId, x.ResponsibleParty, x.Status, x.RectificationAt, x.AriesVerifierUserId, x.AriesVerifiedAt, x.ClientDecisionUserId, x.ClientDecisionAt, x.LastDecisionComment }).ToListAsync(ct);
        var actionIds = actionRows.Select(x => x.Id).ToArray();
        var actionHistory = await db.CorrectiveActionEvents.AsNoTracking().Where(x => actionIds.Contains(x.CorrectiveActionId)).OrderBy(x => x.OccurredAt).Select(x => new { x.Id, x.CorrectiveActionId, x.Kind, x.ResultingStatus, x.ActorUserId, x.Comment, x.RectificationDescription, x.RectificationAt, x.WorkOrderReference, x.ResponsibleParty, x.OccurredAt }).ToListAsync(ct);
        var actionEvidence = await (from actionEvidenceRow in db.ActivityEvidence.AsNoTracking() join file in db.Files.AsNoTracking() on actionEvidenceRow.SurveyFileId equals file.Id join ev in db.CorrectiveActionEvents.AsNoTracking() on actionEvidenceRow.CorrectiveActionEventId equals ev.Id where actionIds.Contains(ev.CorrectiveActionId) orderby actionEvidenceRow.AddedAt select new { actionEvidenceRow.Id, ev.CorrectiveActionId, actionEvidenceRow.Role, actionEvidenceRow.Caption, actionEvidenceRow.AddedByUserId, actionEvidenceRow.AddedAt, file.FileName, file.ContentType, Url = $"/api/v1/inspection-activities/{activityId:D}/photos/{actionEvidenceRow.Id:D}" }).ToListAsync(ct);
        var correctiveActions = actionRows.Select(x => new { x.Id, x.ActivityFindingId, x.ActionReference, x.SupplementaryInformation, x.ProposedAction, x.RectificationDescription, x.ExternalWorkOrderReference, x.ResponsibleUserId, x.ResponsibleParty, x.Status, x.RectificationAt, x.AriesVerifierUserId, x.AriesVerifiedAt, x.ClientDecisionUserId, x.ClientDecisionAt, x.LastDecisionComment, History = actionHistory.Where(e => e.CorrectiveActionId == x.Id), Evidence = actionEvidence.Where(e => e.CorrectiveActionId == x.Id) });
        var reviewHistory = await db.ActivityReviewEvents.AsNoTracking().Where(x => x.InspectionActivityId == activityId).OrderBy(x => x.DecidedAt).Select(x => new { x.Decision, x.Comments, x.ActorUserId, x.DecidedAt }).ToListAsync(ct);
        var checkpointReviews = await db.CheckpointReviewEvents.AsNoTracking().Where(x => x.InspectionActivityId == activityId).OrderBy(x => x.ReviewedAt).Select(x => new { x.CheckpointResponseRevisionId, x.Decision, x.Comments, x.ReviewerUserId, x.ReviewedAt }).ToListAsync(ct);
        var referenceDocumentsUsed=options.Value.DocumentUsageEnabled?await db.InspectionActivityDocumentUsages.AsNoTracking().Where(x=>x.InspectionActivityId==activityId).OrderBy(x=>x.OpenedAt).Select(x=>new{x.DocumentIdentity,x.DocumentRevision,x.Sha256,x.Title,x.Scope,x.ScopeId,x.OpenedAt,x.RecordedAt}).ToListAsync(ct):[];
        var evidence = await (from item in db.ActivityEvidence.AsNoTracking() join file in db.Files.AsNoTracking() on item.SurveyFileId equals file.Id where item.InspectionActivityId == activityId orderby item.AddedAt select new { item.Id, item.CheckpointResponseRevisionId, CheckpointDefinitionSnapshotId = db.CheckpointResponseRevisions.Where(r => r.Id == item.CheckpointResponseRevisionId).Select(r => (Guid?)r.CheckpointDefinitionSnapshotId).FirstOrDefault(), item.Role, item.Caption, item.AddedByUserId, item.AddedAt, file.FileName, file.ContentType, Url = $"/api/v1/inspection-activities/{activityId:D}/photos/{item.Id:D}" }).ToListAsync(ct);
        return Ok(new { activity.Id, activity.InspectionTaskId, activity.Grade, activity.EquipmentComments, activity.IsOnHold, activity.ReviewStatus, activity.StartedAt, activity.CompletedAt, RowVersion = Convert.ToBase64String(activity.RowVersion), checklist, checkpoints, responses = responses.GroupBy(x => x.CheckpointDefinitionSnapshotId).Select(x => x.First()), findings, correctiveActions, reviewHistory, checkpointReviews, evidence,referenceDocumentsUsed });
    }

    [HttpPost("tasks/{taskId:guid}/activities")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> Start(Guid taskId, CreateInspectionActivityRequest request, CancellationToken ct) => ExecuteService(async () =>
    {
        var result = await activities.CreateActivityAsync(taskId, request, ct);
        return CreatedAtAction(nameof(Detail), new { activityId = result.Id }, result);
    });

    /// <summary>Starts an occurrence against the existing active checklist; creates additive plan/task/snapshot records as needed.</summary>
    [HttpPost("equipment/{equipmentId:guid}/start")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> StartForEquipment(Guid equipmentId, StartEquipmentActivityRequest request, CancellationToken ct) => ExecuteService(() => StartForEquipmentCore(equipmentId, request, ct));

    private async Task<IActionResult> StartForEquipmentCore(Guid equipmentId, StartEquipmentActivityRequest request, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Conflict(new { error = "Inspection activity workflow is not enabled on this installation." });
        if (!Enum.IsDefined(request.Grade)) return BadRequest(new { error = "Select Visual, Close, or Detailed." });
        var equipment = await db.AreaEquipment.AsNoTracking().SingleOrDefaultAsync(x => x.Id == equipmentId, ct);
        if (equipment is null || !await CanAccessProject(equipment.ProjectId, ct)) return NotFound();
        if (!string.Equals(equipment.RegistrationStatus, "Verified", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { error = "Equipment registration is awaiting independent review. It cannot start an official inspection until verified." });
        var actor = await db.Users.AsNoTracking().SingleAsync(x => x.Id == ActorId, ct);
        var templates = await db.ChecklistTemplates.AsNoTracking().Where(x => x.IsActive && x.LifecycleStatus == ChecklistLifecycleStatus.Published && x.InspectionType == request.Grade).OrderByDescending(x => x.Version).ToListAsync(ct);
        var protectionType = equipment.ExProtectionType?.Trim();
        var template = templates.FirstOrDefault(x => ChecklistApplicability.TemplateMatches(x, request.Grade, protectionType));
        if (template is null) return Conflict(new { error = string.IsNullOrWhiteSpace(protectionType) ? "Equipment Ex protection type is not recorded and no legacy checklist applies. Add verified equipment Ex data or have an approved applicable checklist published." : $"No approved published checklist applies to the {request.Grade} grade and Ex protection type '{protectionType}'." });

        var snapshot = await db.ChecklistVersionSnapshots.SingleOrDefaultAsync(x => x.SourceChecklistTemplateId == template.Id && x.VersionNumber == template.Version && x.ProtectionTypeSnapshot == protectionType, ct);
        if (snapshot is null)
        {
            var allQuestions = await db.ChecklistQuestions.AsNoTracking().Where(x => x.ChecklistTemplateId == template.Id).OrderBy(x => x.SortOrder).ToListAsync(ct);
            var questions = allQuestions.Where(x => ChecklistApplicability.QuestionMatches(x, protectionType)).ToList();
            if (questions.Count == 0) return Conflict(new { error = "The selected checklist has no questions." });
            var hashInput = $"{template.Id:D}|{template.Version}|{template.StandardReference}|{template.StandardEdition}|{protectionType}\n" + string.Join("\n", questions.Select(x => $"{x.Id:D}|{x.SectionCode}|{x.SortOrder}|{x.Text}|{x.IsRequired}|{x.ResponseScaleCode}|{x.RequireRemarkOnFail}|{x.NumericReadingEnabled}|{x.MeasurementUnit}|{x.MinimumReading}|{x.MaximumReading}"));
            snapshot = new ChecklistVersionSnapshot { SourceChecklistTemplateId = template.Id, VersionNumber = template.Version, InspectionType = template.InspectionType, SnapshotHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hashInput))), PublishedAt = template.ApprovedAt ?? DateTimeOffset.UtcNow, PublishedByUserId = template.ApprovedByUserId ?? actor.Id, StandardReference = template.StandardReference, StandardEdition = template.StandardEdition, ControlledSourceReference = template.ControlledSourceReference, ProtectionTypeSnapshot = protectionType };
            db.ChecklistVersionSnapshots.Add(snapshot);
            foreach (var question in questions) db.CheckpointDefinitionSnapshots.Add(new CheckpointDefinitionSnapshot { ChecklistVersionSnapshotId = snapshot.Id, QuestionCode = question.Id.ToString("N"), QuestionVersion = template.Version, QuestionText = question.Text, SortOrder = question.SortOrder, SectionCode = question.SectionCode, ApplicabilityCode = question.ApplicableProtectionTypes ?? template.ApplicableProtectionTypes, IsRequired = question.IsRequired, ResponseScaleCode = question.ResponseScaleCode, RequireRemarkOnFail = question.RequireRemarkOnFail, NumericReadingEnabled = question.NumericReadingEnabled, MeasurementUnit = question.MeasurementUnit, MinimumReading = question.MinimumReading, MaximumReading = question.MaximumReading, NumericReadingRequired = question.NumericReadingRequired });
            await db.SaveChangesAsync(ct);
        }
        var scheduledTasks = await db.InspectionTasks.Where(x => x.AreaEquipmentId == equipment.Id && x.WorkPackageId != null && (x.StatusCode == "Planned" || x.StatusCode == "InProgress") && x.PlannedGrade == request.Grade).ToListAsync(ct);
        if (actor.Role == UserRole.Surveyor) scheduledTasks = scheduledTasks.Where(x => x.AssignedUserId == actor.Id).ToList();
        if (scheduledTasks.Count > 1) return Conflict(new { error = "More than one active task matches this equipment and grade. Ask the planner to resolve the duplicate assignment." });
        if (scheduledTasks.Count == 0)
            return Conflict(new { error = "No approved active work-package task exists for this equipment and grade. Ask the authorized Inspection Lead to verify registration and assign a task before starting." });
        var task = scheduledTasks[0];
        if (task.AssignedUserId != actor.Id && actor.Role == UserRole.Surveyor) return Forbid();
        if (task.PlannedGrade.HasValue && task.PlannedGrade.Value != request.Grade)
            return Conflict(new { error = "Inspection grade does not match the approved work package." });
        if (task.WorkPackageId.HasValue) task.StatusCode = "InProgress";
        var started = request.StartedAt ?? DateTimeOffset.UtcNow;
        var result = await activities.CreateActivityAsync(task.Id, new CreateInspectionActivityRequest(request.Grade, snapshot.Id, started, null), ct);
        return CreatedAtAction(nameof(Detail), new { activityId = result.Id }, result);
    }

    [HttpPost("{activityId:guid}/checkpoints")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> SaveCheckpoint(Guid activityId, SaveCheckpointResponseRequest request, CancellationToken ct) => ExecuteService(async () => Ok(await activities.SaveCheckpointResponseAsync(activityId, request, ct)));

    [HttpPost("{activityId:guid}/findings")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> AddFinding(Guid activityId, AddActivityFindingRequest request, CancellationToken ct) => ExecuteService(async () => Ok(await activities.AddFindingAsync(activityId, request, ct)));

    [HttpGet("defects")]
    [Authorize(Roles = "Surveyor,Qc,Manager,Admin,Client")]
    public async Task<IActionResult> DefectRegister([FromQuery] string? status, [FromQuery] bool overdueOnly = false, CancellationToken ct = default, [FromQuery] Guid? projectId = null)
    {
        if (!options.Value.Enabled) return Conflict(new { error = "Inspection activity workflow is not enabled on this installation." });
        var rows = await (from finding in db.ActivityFindings.AsNoTracking()
                          join activity in db.InspectionActivities.AsNoTracking() on finding.InspectionActivityId equals activity.Id
                          join task in db.InspectionTasks.AsNoTracking() on activity.InspectionTaskId equals task.Id
                          join plan in db.InspectionPlans.AsNoTracking() on task.InspectionPlanId equals plan.Id
                          join project in db.Projects.AsNoTracking() on plan.ProjectId equals project.Id
                          join response in db.CheckpointResponseRevisions.AsNoTracking() on finding.CheckpointResponseRevisionId equals response.Id
                          join definition in db.CheckpointDefinitionSnapshots.AsNoTracking() on response.CheckpointDefinitionSnapshotId equals definition.Id
                          join action in db.CorrectiveActions.AsNoTracking() on finding.Id equals action.ActivityFindingId into actionGroup
                          from action in actionGroup.DefaultIfEmpty()
                          where (User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Manager)) && (project.CreatedByUserId == ActorId || db.Users.Any(q => q.Id == project.AssignedQcId && q.ManagerId == ActorId)) || User.IsInRole(nameof(UserRole.Surveyor)) && (project.AssignedSurveyorId == ActorId || project.AdditionalSurveyorId == ActorId) || User.IsInRole(nameof(UserRole.Qc)) && (project.AssignedQcId == ActorId || project.AdditionalQcId == ActorId) || User.IsInRole(nameof(UserRole.Client)) && (project.AssignedClientId == ActorId || project.ClientAssignments.Any(a => a.IsActive && a.ClientUserId == ActorId)))
                          where projectId == null || project.Id == projectId
                          orderby finding.ReportedAt descending
                          select new { finding.Id, finding.InspectionActivityId, EquipmentId = task.AreaEquipmentId, EquipmentTag = task.AreaEquipmentId.HasValue ? db.AreaEquipment.Where(e => e.Id == task.AreaEquipmentId).Select(e => e.TagNumber).FirstOrDefault() : null, ItemNumber = db.AreaEquipment.Where(e => e.Id == task.AreaEquipmentId).Select(e => e.EquipmentCode).FirstOrDefault(),
                              EquipmentDescription = db.AreaEquipment.Where(e => e.Id == task.AreaEquipmentId).Select(e => e.Description ?? e.EquipmentName ?? e.Type).FirstOrDefault(),
                              AreaName = (from equipment in db.AreaEquipment join area in db.ProjectAreas on equipment.AreaId equals area.Id where equipment.Id == task.AreaEquipmentId select area.AreaName).FirstOrDefault(),
                              LegacyEquipmentId = task.LegacyEquipmentId, ProjectId = project.Id, Project = project.ProjectName, Grade = activity.Grade, InspectionDate = activity.StartedAt, Checkpoint = definition.QuestionText, DefinitionId = definition.Id, finding.FailedGrades, finding.AosdId, finding.FaultCategory, finding.RegisterRemarks, finding.FollowUpRemarks, finding.FaultCode, finding.Observation, finding.ProposedAction, finding.SeverityCode, finding.ResponsibleParty, finding.RequiredMaterialsOrSupport, finding.StatusCode, finding.ReportedAt, finding.ReportedByUserId, finding.DueAt, ActionId = action == null ? (Guid?)null : action.Id, ActionReference = action == null ? null : action.ActionReference, ActionStatus = action == null ? (CorrectiveActionStatus?)null : action.Status, ActionResponsibleParty = action == null ? null : action.ResponsibleParty, RectificationDescription = action == null ? null : action.RectificationDescription, WorkOrderReference = action == null ? null : action.ExternalWorkOrderReference, RectificationAt = action == null ? null : action.RectificationAt })
            .ToListAsync(ct);
        var filtered = rows.Where(x => (string.IsNullOrWhiteSpace(status) || string.Equals(status, "Outstanding", StringComparison.OrdinalIgnoreCase) && x.ActionStatus != CorrectiveActionStatus.ClientClosed || string.Equals(status, "Overdue", StringComparison.OrdinalIgnoreCase) && x.DueAt < DateTimeOffset.UtcNow && x.ActionStatus != CorrectiveActionStatus.ClientClosed || !string.Equals(status, "Outstanding", StringComparison.OrdinalIgnoreCase) && !string.Equals(status, "Overdue", StringComparison.OrdinalIgnoreCase) && string.Equals(status, x.ActionStatus?.ToString() ?? x.StatusCode, StringComparison.OrdinalIgnoreCase)) && (!overdueOnly || x.DueAt < DateTimeOffset.UtcNow && x.ActionStatus != CorrectiveActionStatus.ClientClosed)).ToArray();
        var actionIds = filtered.Where(x => x.ActionId.HasValue).Select(x => x.ActionId!.Value).Distinct().ToArray();
        var history = await db.CorrectiveActionEvents.AsNoTracking().Where(x => actionIds.Contains(x.CorrectiveActionId)).OrderBy(x => x.OccurredAt).Select(x => new { x.CorrectiveActionId, x.Kind, x.ResultingStatus, x.ActorUserId, x.Comment, x.RectificationDescription, x.RectificationAt, x.WorkOrderReference, x.ResponsibleParty, x.OccurredAt }).ToListAsync(ct);
        var actionEvidence = await (from evidence in db.ActivityEvidence.AsNoTracking() join file in db.Files.AsNoTracking() on evidence.SurveyFileId equals file.Id join ev in db.CorrectiveActionEvents.AsNoTracking() on evidence.CorrectiveActionEventId equals ev.Id where actionIds.Contains(ev.CorrectiveActionId) orderby evidence.AddedAt select new { ev.CorrectiveActionId, evidence.Id, evidence.Role, evidence.Caption, evidence.AddedByUserId, evidence.AddedAt, file.FileName, file.ContentType }).ToListAsync(ct);
        var activityIds = filtered.Select(x => x.InspectionActivityId).Distinct().ToArray();
        var sourcePhotos = await (from evidence in db.ActivityEvidence.AsNoTracking()
                                  join file in db.Files.AsNoTracking() on evidence.SurveyFileId equals file.Id
                                  where evidence.InspectionActivityId.HasValue && activityIds.Contains(evidence.InspectionActivityId.Value) &&
                                        evidence.CorrectiveActionEventId == null && file.ContentType.StartsWith("image/")
                                  orderby evidence.AddedAt, evidence.Id
                                  select new {evidence.Id, evidence.InspectionActivityId, file.FileName,
                                      DefinitionId = db.CheckpointResponseRevisions.Where(r => r.Id == evidence.CheckpointResponseRevisionId)
                                          .Select(r => (Guid?)r.CheckpointDefinitionSnapshotId).FirstOrDefault()}).ToListAsync(ct);
        var photoLookup = sourcePhotos.ToLookup(photo => photo.InspectionActivityId);
        var result = filtered.Select(x =>
        {
            var actionHistory = x.ActionId.HasValue ? history.Where(h => h.CorrectiveActionId == x.ActionId).Select(h => (object)new { h.Kind, h.ResultingStatus, h.ActorUserId, h.Comment, h.RectificationDescription, h.RectificationAt, h.WorkOrderReference, h.ResponsibleParty, h.OccurredAt }).ToArray() : Array.Empty<object>();
            var evidenceRows = x.ActionId.HasValue ? actionEvidence.Where(e => e.CorrectiveActionId == x.ActionId).Select(e => (object)new { e.Id, e.Role, e.Caption, e.AddedByUserId, e.AddedAt, e.FileName, e.ContentType, Url = $"/api/v1/inspection-activities/{x.InspectionActivityId:D}/photos/{e.Id:D}" }).ToArray() : Array.Empty<object>();
            var photos = photoLookup[x.InspectionActivityId]
                .Where(p => p.DefinitionId == x.DefinitionId || p.DefinitionId == null)
                .OrderByDescending(p => p.DefinitionId.HasValue).Take(2)
                .Select(p => new {p.Id, p.FileName, Url = $"/api/v1/inspection-activities/{x.InspectionActivityId:D}/photos/{p.Id:D}"});
            return new { x.Id, x.InspectionActivityId, x.EquipmentId, x.EquipmentTag, x.ItemNumber, x.EquipmentDescription, x.AreaName, x.LegacyEquipmentId, x.ProjectId, x.Project, x.Grade, x.FailedGrades, x.InspectionDate, x.AosdId, x.FaultCategory, x.Checkpoint, Remarks = x.RegisterRemarks, AdditionalRemarks = x.FollowUpRemarks, Photos = photos, x.FaultCode, x.Observation, x.ProposedAction, x.SeverityCode, x.ResponsibleParty, x.RequiredMaterialsOrSupport, x.StatusCode, x.ReportedAt, x.ReportedByUserId, x.DueAt, IsOutstanding = x.ActionStatus != CorrectiveActionStatus.ClientClosed, IsOverdue = x.DueAt < DateTimeOffset.UtcNow && x.ActionStatus != CorrectiveActionStatus.ClientClosed, x.ActionId, x.ActionReference, x.ActionStatus, x.ActionResponsibleParty, x.RectificationDescription, x.WorkOrderReference, x.RectificationAt, History = actionHistory, Evidence = evidenceRows };
        });
        return Ok(result);
    }

    [HttpPost("findings/{findingId:guid}/corrective-actions")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> CreateCorrectiveAction(Guid findingId, CreateCorrectiveActionRequest request, CancellationToken ct) => ExecuteService(async () =>
    {
        if (request.ActivityFindingId != findingId) return BadRequest(new { error = "Finding ID in route and body must match." });
        return Ok(await activities.CreateCorrectiveActionAsync(request, ct));
    });

    [HttpPost("corrective-actions/{actionId:guid}/rectification")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> ReportRectification(Guid actionId, CorrectiveActionReportRequest request, CancellationToken ct) => ExecuteService(async () => Ok(await activities.ReportRectificationAsync(actionId, request, ct)));

    [HttpPost("corrective-actions/{actionId:guid}/submit-verification")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> SubmitCorrectiveAction(Guid actionId, CancellationToken ct) => ExecuteService(async () => Ok(await activities.SubmitCorrectiveActionForVerificationAsync(actionId, ct)));

    [HttpPost("corrective-actions/{actionId:guid}/verification")]
    [Authorize(Roles = "Qc,Manager,Admin")]
    public Task<IActionResult> VerifyCorrectiveAction(Guid actionId, CorrectiveActionVerificationRequest request, CancellationToken ct) => ExecuteService(async () => Ok(await activities.VerifyCorrectiveActionAsync(actionId, request, ct)));

    [HttpPost("corrective-actions/{actionId:guid}/client-closure")]
    [Authorize(Roles = "Client")]
    public Task<IActionResult> ClientClosure(Guid actionId, ClientClosureDecisionRequest request, CancellationToken ct) => ExecuteService(async () => Ok(await activities.DecideClientClosureAsync(actionId, request, ct)));

    [HttpPost("{activityId:guid}/evidence")]
    [Authorize(Roles = "Surveyor,Manager,Admin,Qc")]
    public Task<IActionResult> AddEvidence(Guid activityId, AddActivityEvidenceRequest request, CancellationToken ct) => ExecuteService(async () => Ok(await activities.AddEvidenceAsync(activityId, request, ct)));

    [HttpPost("{activityId:guid}/photos")]
    [Authorize(Roles = "Surveyor,Manager,Admin,Qc")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPhoto(Guid activityId, IFormFile photo, [FromForm] Guid? checkpointResponseRevisionId, [FromForm] string? caption, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Conflict(new { error = "Inspection activity workflow is not enabled on this installation." });
        var activity = await db.InspectionActivities.SingleOrDefaultAsync(x => x.Id == activityId, ct);
        if (activity is null) return NotFound();
        if (activity.ReviewStatus is not (InspectionActivityReviewStatus.Draft or InspectionActivityReviewStatus.ChangesRequested)) return Conflict(new { error = "Evidence cannot be added after submission; create a new activity or request correction." });
        var task = await db.InspectionTasks.SingleAsync(x => x.Id == activity.InspectionTaskId, ct);
        if (task.AreaEquipmentId is not Guid equipmentId) return Conflict(new { error = "Photo upload currently requires modern project equipment." });
        var projectId = await db.AreaEquipment.Where(x => x.Id == equipmentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct);
        if (projectId is null || !await CanAccessProject(projectId.Value, ct)) return NotFound();
        if (User.IsInRole(nameof(UserRole.Surveyor)) && !await IsProjectSurveyor(projectId.Value, ct)) return Forbid();
        if (checkpointResponseRevisionId is Guid responseId && !await (from response in db.CheckpointResponseRevisions join revision in db.InspectionActivityRevisions on response.ActivityRevisionId equals revision.Id where response.Id == responseId && revision.InspectionActivityId == activityId select response.Id).AnyAsync(ct)) return BadRequest(new { error = "Selected checkpoint does not belong to this activity." });
        if (photo is null) return BadRequest(new { error = "Select an image to upload." });
        var idempotencyKey=Request.Headers["Idempotency-Key"].FirstOrDefault();string? contentHash=null;
        if(!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            if(!options.Value.Enabled||idempotencyKey.Length>100)return BadRequest(new{error="Invalid offline photo idempotency key."});
            if(photo.Length>10*1024*1024)return BadRequest(new{error="Photo exceeds the 10 MB upload limit."});
            await using var input=photo.OpenReadStream();using var bytes=new MemoryStream();await input.CopyToAsync(bytes,ct);contentHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray()));
            var prior=await db.OfflinePhotoSyncReceipts.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==ActorId&&x.IdempotencyKey==idempotencyKey,ct);
            if(prior is not null)
            {
                if(prior.ContentHash!=contentHash||prior.ActivityId!=activityId||prior.CheckpointResponseRevisionId!=checkpointResponseRevisionId)return Conflict(new{error="This photo idempotency key was previously used with different content or association."});
                var priorEvidence=await (from evidence in db.ActivityEvidence.AsNoTracking() join file in db.Files.AsNoTracking() on evidence.SurveyFileId equals file.Id where evidence.Id==prior.EvidenceId select new{evidence.Id,evidence.InspectionActivityId,evidence.AreaEquipmentId,evidence.CheckpointResponseRevisionId,evidence.Role,evidence.Caption,evidence.AddedAt,file.FileName,file.ContentType}).SingleAsync(ct);
                return Ok(new{priorEvidence.Id,priorEvidence.InspectionActivityId,priorEvidence.AreaEquipmentId,priorEvidence.CheckpointResponseRevisionId,priorEvidence.Role,priorEvidence.Caption,priorEvidence.AddedAt,priorEvidence.FileName,priorEvidence.ContentType,Url=$"/api/v1/inspection-activities/{activityId:D}/photos/{priorEvidence.Id:D}",duplicate=true});
            }
        }
        StoredProjectPhoto stored;
        try { stored = await photoStorage.SaveInspectionAsync(photo, projectId.Value, activityId, ct); }
        catch (ProjectPhotoValidationException ex) { return BadRequest(new { error = ex.Message }); }
        try
        {
            var actor = await db.Users.AsNoTracking().SingleAsync(x => x.Id == ActorId, ct);
            var file = new SurveyFile { Category = FileCategory.FindingPhoto, FileName = stored.OriginalFileName, StorageKey = stored.RelativePath, ContentType = stored.IsPdf ? "application/pdf" : photo.ContentType, UploadedById = actor.Id };
            db.Files.Add(file);
            var evidence = new ActivityEvidence { SurveyFileId = file.Id, InspectionActivityId = activityId, AreaEquipmentId = equipmentId, CheckpointResponseRevisionId = checkpointResponseRevisionId, Role = ActivityEvidenceRole.Checkpoint, Caption = caption?.Trim(), AddedByUserId = actor.Id, AddedAt = DateTimeOffset.UtcNow };
            db.ActivityEvidence.Add(evidence);
            activity.UpdatedAt = DateTimeOffset.UtcNow;
            activity.LastModifiedByUserId = ActorId;
            if(idempotencyKey is not null&&contentHash is not null)db.OfflinePhotoSyncReceipts.Add(new OfflinePhotoSyncReceipt{UserId=ActorId,IdempotencyKey=idempotencyKey,ContentHash=contentHash,ActivityId=activityId,CheckpointResponseRevisionId=checkpointResponseRevisionId,EvidenceId=evidence.Id});
            await db.SaveChangesAsync(ct);
            FieldworkFileTransaction.For(HttpContext)?.DeleteIfRolledBack(stored.RelativePath);
            return Ok(new { evidence.Id, evidence.InspectionActivityId, evidence.AreaEquipmentId, evidence.CheckpointResponseRevisionId, evidence.Role, evidence.Caption, evidence.AddedAt, file.FileName, file.ContentType, Url = $"/api/v1/inspection-activities/{activityId:D}/photos/{evidence.Id:D}" });
        }
        catch (DbUpdateConcurrencyException)
        {
            await photoStorage.DeleteAsync(stored.RelativePath, ct);
            return Conflict(new {error = "The inspection changed. Reload it before adding photos."});
        }
        catch
        {
            await photoStorage.DeleteAsync(stored.RelativePath, ct);
            throw;
        }
    }

    [HttpGet("{activityId:guid}/photos")]
    [Authorize(Roles = "Surveyor,Manager,Admin,Qc,Client")]
    public async Task<IActionResult> Photos(Guid activityId, CancellationToken ct)
    {
        if (!await CanAccessActivity(activityId, ct)) return NotFound();
        var items = await (from evidence in db.ActivityEvidence.AsNoTracking() join file in db.Files.AsNoTracking() on evidence.SurveyFileId equals file.Id where evidence.InspectionActivityId == activityId orderby evidence.AddedAt select new { evidence.Id, evidence.AreaEquipmentId, evidence.LegacyEquipmentId, evidence.InspectionActivityId, evidence.CheckpointResponseRevisionId, evidence.AddedByUserId, evidence.AddedAt, evidence.Caption, file.FileName, file.ContentType, Url = $"/api/v1/inspection-activities/{activityId:D}/photos/{evidence.Id:D}" }).ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{activityId:guid}/photos/{evidenceId:guid}")]
    [Authorize(Roles = "Surveyor,Manager,Admin,Qc,Client")]
    public async Task<IActionResult> Photo(Guid activityId, Guid evidenceId, CancellationToken ct)
    {
        if (!await CanAccessActivity(activityId, ct)) return NotFound();
        var row = await (from evidence in db.ActivityEvidence.AsNoTracking() join file in db.Files.AsNoTracking() on evidence.SurveyFileId equals file.Id where evidence.InspectionActivityId == activityId && evidence.Id == evidenceId select new { file.StorageKey, file.FileName, file.ContentType }).SingleOrDefaultAsync(ct);
        if (row is null) return NotFound();
        var content = await photoStorage.OpenReadAsync(row.StorageKey, ct);
        return content is null ? NotFound() : File(content.Stream, content.ContentType, row.FileName, enableRangeProcessing: true);
    }

    [HttpPut("{activityId:guid}/equipment-comments")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> SaveEquipmentComments(Guid activityId, EquipmentCommentsRequest request,
        [FromServices] InspectionActivityAuthorizer authorizer, CancellationToken ct) => ExecuteService(async () =>
    {
        if (!options.Value.Enabled) return NotFound();
        if (request.Comments?.Length > 4000) return BadRequest(new {error = "Equipment comments must be 4,000 characters or fewer."});
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Manager, UserRole.Admin);
        var (activity, task) = await authorizer.EnsureActivityScopeAsync(activityId, ct);
        if (actor.Role == UserRole.Surveyor &&
            (await FindActivityProject(activityId, ct) is not Guid projectId || !await IsProjectSurveyor(projectId, ct))) return Forbid();
        if (activity.ReviewStatus is not (InspectionActivityReviewStatus.Draft or InspectionActivityReviewStatus.ChangesRequested))
            return Conflict(new {error = "This inspection is read-only."});
        InspectionActivityRules.EnsureExpectedRowVersion(activity.RowVersion, request.RowVersion);
        activity.EquipmentComments = request.Comments?.Trim();
        if (request.IsOnHold.HasValue) activity.IsOnHold = request.IsOnHold.Value;
        activity.UpdatedAt = DateTimeOffset.UtcNow;
        activity.LastModifiedByUserId = ActorId;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new {error = "The inspection changed. Reopen it and retry."}); }
        return NoContent();
    });

    [HttpDelete("{activityId:guid}/photos/{evidenceId:guid}")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> RemoveDraftPhoto(Guid activityId, Guid evidenceId,
        [FromServices] InspectionActivityAuthorizer authorizer, CancellationToken ct) => ExecuteService(async () =>
    {
        if (!options.Value.Enabled) return NotFound();
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Manager, UserRole.Admin);
        var (activity, task) = await authorizer.EnsureActivityScopeAsync(activityId, ct);
        if (actor.Role == UserRole.Surveyor &&
            (await FindActivityProject(activityId, ct) is not Guid projectId || !await IsProjectSurveyor(projectId, ct))) return Forbid();
        if (activity.ReviewStatus != InspectionActivityReviewStatus.Draft)
            return Conflict(new {error = "Submitted inspection evidence is preserved."});
        var evidence = await db.ActivityEvidence.SingleOrDefaultAsync(e => e.Id == evidenceId &&
            e.InspectionActivityId == activityId && e.CorrectiveActionEventId == null, ct);
        if (evidence is null) return NotFound();
        if (await db.OfflinePhotoSyncReceipts.AnyAsync(x => x.EvidenceId == evidenceId, ct))
            return Conflict(new {error = "Photos synchronized from an offline inspection are preserved."});
        db.ActivityEvidence.Remove(evidence);
        activity.UpdatedAt = DateTimeOffset.UtcNow;
        activity.LastModifiedByUserId = ActorId;
        await db.SaveChangesAsync(ct);
        return NoContent();
    });

    [HttpPost("corrective-actions/{actionId:guid}/photos")]
    [Authorize(Roles = "Surveyor,Manager,Admin,Qc")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadCorrectiveActionPhoto(Guid actionId, IFormFile photo, [FromForm] ActivityEvidenceRole role, [FromForm] string? caption, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Conflict(new { error = "Inspection activity workflow is not enabled on this installation." });
        if (role is not (ActivityEvidenceRole.Before or ActivityEvidenceRole.After or ActivityEvidenceRole.Correction)) return BadRequest(new { error = "Corrective-action evidence must be Before, After, or Correction." });
        var row = await (from action in db.CorrectiveActions join finding in db.ActivityFindings on action.ActivityFindingId equals finding.Id join activity in db.InspectionActivities on finding.InspectionActivityId equals activity.Id join task in db.InspectionTasks on activity.InspectionTaskId equals task.Id where action.Id == actionId select new { Action = action, Finding = finding, Activity = activity, Task = task }).SingleOrDefaultAsync(ct);
        if (row is null) return NotFound();
        if (row.Task.AreaEquipmentId is not Guid equipmentId) return Conflict(new { error = "Corrective-action photo upload requires modern project equipment." });
        var projectId = await db.AreaEquipment.Where(x => x.Id == equipmentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct);
        if (projectId is null || !await CanAccessProject(projectId.Value, ct)) return NotFound();
        if (User.IsInRole(nameof(UserRole.Surveyor)) && row.Task.AssignedUserId != ActorId && row.Action.ResponsibleUserId != ActorId) return Forbid();
        if (photo is null) return BadRequest(new { error = "Select an image to upload." });
        StoredProjectPhoto stored;
        try { stored = await photoStorage.SaveInspectionAsync(photo, projectId.Value, row.Activity.Id, ct); }
        catch (ProjectPhotoValidationException ex) { return BadRequest(new { error = ex.Message }); }
        try
        {
            var actor = await db.Users.AsNoTracking().SingleAsync(x => x.Id == ActorId, ct);
            var file = new SurveyFile { Category = FileCategory.FindingPhoto, FileName = stored.OriginalFileName, StorageKey = stored.RelativePath, ContentType = stored.IsPdf ? "application/pdf" : photo.ContentType, UploadedById = actor.Id };
            var actionEvent = new CorrectiveActionEvent { CorrectiveActionId = row.Action.Id, Kind = CorrectiveActionEventKind.EvidenceAdded, ResultingStatus = row.Action.Status, ActorUserId = actor.Id, Comment = caption?.Trim(), OccurredAt = DateTimeOffset.UtcNow };
            db.Files.Add(file); db.CorrectiveActionEvents.Add(actionEvent);
            var evidence = new ActivityEvidence { SurveyFileId = file.Id, InspectionActivityId = row.Activity.Id, AreaEquipmentId = equipmentId, CorrectiveActionEventId = actionEvent.Id, Role = role, Caption = caption?.Trim(), AddedByUserId = actor.Id, AddedAt = DateTimeOffset.UtcNow };
            db.ActivityEvidence.Add(evidence); await db.SaveChangesAsync(ct);
            return Ok(new { evidence.Id, ActionId = actionId, evidence.InspectionActivityId, evidence.AreaEquipmentId, evidence.Role, evidence.Caption, evidence.AddedAt, file.FileName, file.ContentType, Url = $"/api/v1/inspection-activities/{row.Activity.Id:D}/photos/{evidence.Id:D}" });
        }
        catch { await photoStorage.DeleteAsync(stored.RelativePath, ct); throw; }
    }

    [HttpPost("{activityId:guid}/submit")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> Submit(Guid activityId, CancellationToken ct) => ExecuteService(async () => Ok(await activities.SubmitForReviewAsync(activityId, ct)));

    [HttpGet("qc/queue")]
    [Authorize(Roles = "Qc,Manager,Admin")]
    public async Task<IActionResult> QcQueue(CancellationToken ct)
    {
        var actor = ActorId;
        var rows = await (from activity in db.InspectionActivities.AsNoTracking()
                          join task in db.InspectionTasks.AsNoTracking() on activity.InspectionTaskId equals task.Id
                          join plan in db.InspectionPlans.AsNoTracking() on task.InspectionPlanId equals plan.Id
                          join project in db.Projects.AsNoTracking() on plan.ProjectId equals project.Id
                          join equipment in db.AreaEquipment.AsNoTracking() on task.AreaEquipmentId equals equipment.Id
                          join area in db.ProjectAreas.AsNoTracking() on equipment.AreaId equals area.Id
                          join inspector in db.Users.AsNoTracking() on activity.InspectorUserId equals inspector.Id
                          where activity.ReviewStatus == InspectionActivityReviewStatus.Submitted || activity.ReviewStatus == InspectionActivityReviewStatus.InReview
                          where (User.IsInRole(nameof(UserRole.Admin)) ||
                                 (User.IsInRole(nameof(UserRole.Qc)) && (project.AssignedQcId == actor || project.AdditionalQcId == actor)) ||
                                 (User.IsInRole(nameof(UserRole.Manager)) && (project.CreatedByUserId == actor ||
                                     db.Users.Any(q => q.Id == project.AssignedQcId && q.ManagerId == actor))))
                          orderby activity.UpdatedAt descending
                          select new {activity.Id, activity.Grade, activity.ReviewStatus, activity.StartedAt,
                              SubmittedAt = activity.CompletedAt ?? activity.UpdatedAt,
                              InspectorName = activity.InspectorNameSnapshot ?? inspector.DisplayName,
                              ProjectId = project.Id, project.ProjectName, project.JobNo, project.Location,
                              AreaId = area.Id, area.AreaName, EquipmentId = equipment.Id, equipment.TagNumber,
                              equipment.EquipmentCode, equipment.Type, equipment.MarkingNo})
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("{activityId:guid}/review")]
    [Authorize(Roles = "Qc,Manager,Admin")]
    public Task<IActionResult> Review(Guid activityId, ActivityReviewDecisionRequest request, CancellationToken ct) => ExecuteService(async () => Ok(await activities.RecordReviewAsync(activityId, request, ct)));

    [HttpPost("{activityId:guid}/checkpoint-review")]
    [Authorize(Roles = "Qc,Manager,Admin")]
    public Task<IActionResult> ReviewCheckpoint(Guid activityId, CheckpointReviewRequest request, CancellationToken ct) => ExecuteService(async () => Ok(await activities.RecordCheckpointReviewAsync(activityId, request, ct)));

    [HttpGet("checklist-templates/{templateId:guid}/questions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChecklistMeasurementSettings(Guid templateId, CancellationToken ct)
    {
        var template = await db.ChecklistTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == templateId, ct);
        if (template is null) return NotFound();
        var questions = await db.ChecklistQuestions.AsNoTracking().Where(x => x.ChecklistTemplateId == templateId).OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.Text, x.SortOrder, x.NumericReadingEnabled, x.MeasurementUnit, x.MinimumReading, x.MaximumReading, x.NumericReadingRequired }).ToListAsync(ct);
        return Ok(new { template.Id, template.Name, template.InspectionType, template.Version, questions });
    }

    [HttpPut("checklist-templates/{templateId:guid}/questions/{questionId:guid}/measurement")]
    [Authorize(Roles = "Admin")]
    public Task<IActionResult> ConfigureMeasurement(Guid templateId, Guid questionId, ChecklistMeasurementConfigurationRequest request, CancellationToken ct) => ExecuteService(() => ConfigureMeasurementCore(templateId, questionId, request, ct));

    private async Task<IActionResult> ConfigureMeasurementCore(Guid templateId, Guid questionId, ChecklistMeasurementConfigurationRequest request, CancellationToken ct)
    {
        InspectionActivityContractValidation.Validate(request);
        var template = await db.ChecklistTemplates.SingleOrDefaultAsync(x => x.Id == templateId, ct);
        var question = await db.ChecklistQuestions.SingleOrDefaultAsync(x => x.Id == questionId && x.ChecklistTemplateId == templateId, ct);
        if (template is null || question is null) return NotFound();
        if (template.Version != request.ExpectedTemplateVersion) return Conflict(new { error = "Checklist version changed. Reload configuration before saving." });
        question.NumericReadingEnabled = request.Enabled;
        question.MeasurementUnit = request.Enabled ? request.Unit!.Trim() : null;
        question.MinimumReading = request.Enabled ? request.Minimum : null;
        question.MaximumReading = request.Enabled ? request.Maximum : null;
        question.NumericReadingRequired = request.Enabled && request.Required;
        template.Version = checked(template.Version + 1);
        await db.SaveChangesAsync(ct);
        return Ok(new { TemplateId = template.Id, template.Version, QuestionId = question.Id, question.NumericReadingEnabled, question.MeasurementUnit, question.MinimumReading, question.MaximumReading, question.NumericReadingRequired });
    }

    private Task<bool> IsProjectSurveyor(Guid projectId, CancellationToken ct) =>
        db.Projects.AnyAsync(x => x.Id == projectId &&
            (x.AssignedSurveyorId == ActorId || x.AdditionalSurveyorId == ActorId), ct);

    private Task<Guid?> FindActivityProject(Guid activityId, CancellationToken ct) =>
        (from activity in db.InspectionActivities.AsNoTracking()
         join task in db.InspectionTasks on activity.InspectionTaskId equals task.Id
         join plan in db.InspectionPlans on task.InspectionPlanId equals plan.Id
         where activity.Id == activityId
         select plan.ProjectId).SingleOrDefaultAsync(ct);

    private async Task<bool> CanAccessActivity(Guid activityId, CancellationToken ct)
    {
        var projectId = await FindActivityProject(activityId, ct);
        return projectId.HasValue && await CanAccessProject(projectId.Value, ct);
    }

    private async Task<bool> CanAccessProject(Guid projectId, CancellationToken ct)
    {
        if (User.IsInRole(nameof(UserRole.Admin))) return await db.Projects.AnyAsync(x => x.Id == projectId, ct);
        if (User.IsInRole(nameof(UserRole.Surveyor))) return await db.Projects.AnyAsync(x => x.Id == projectId && ((x.AssignedSurveyorId == ActorId || x.AdditionalSurveyorId == ActorId) || db.InspectionTasks.Any(t => t.AssignedUserId == ActorId && db.InspectionPlans.Any(p => p.Id == t.InspectionPlanId && p.ProjectId == x.Id))), ct);
        if (User.IsInRole(nameof(UserRole.Qc))) return await db.Projects.AnyAsync(x => x.Id == projectId && (x.AssignedQcId == ActorId || x.AdditionalQcId == ActorId), ct);
        if (User.IsInRole(nameof(UserRole.Client))) return await db.Projects.AnyAsync(x => x.Id == projectId && (x.AssignedClientId == ActorId || x.ClientAssignments.Any(a => a.IsActive && a.ClientUserId == ActorId)), ct);
        if (User.IsInRole(nameof(UserRole.Manager))) return await db.Projects.AnyAsync(x => x.Id == projectId && (x.CreatedByUserId == ActorId || db.Users.Any(q => q.Id == x.AssignedQcId && q.ManagerId == ActorId)), ct);
        return false;
    }

    private async Task<IActionResult> ExecuteService(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (InspectionRequestConflictException ex) { return Conflict(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

public sealed record StartEquipmentActivityRequest(InspectionType Grade, DateTimeOffset? StartedAt = null);

public sealed record EquipmentCommentsRequest(string? Comments, string RowVersion, bool? IsOnHold = null);
