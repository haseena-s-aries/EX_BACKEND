namespace Ex_SurveyApp_api.Models;

/// <summary>New activity vocabulary is additive; legacy SurveySession remains the active workflow.</summary>
public enum InspectionTaskKind { Regular, Corrective }
public enum InspectionActivityReviewStatus { Draft, Submitted, InReview, ChangesRequested, Accepted }
public enum CheckpointWorkflowState { Open, Reviewed, Closed }
public enum CorrectiveActionStatus { Proposed, Assigned, InProgress, SubmittedForVerification, Verified, ClientRejected, ClientClosed, Reopened }
public enum CorrectiveActionEventKind { Created, Assigned, EvidenceAdded, SubmittedForVerification, Verified, VerificationRejected, ClientAccepted, ClientRejected, Reopened, Commented }
public enum ActivityEvidenceRole { Checkpoint, Original, Before, After, Correction, Certificate, Other }

public sealed class InspectionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public required string Name { get; set; }
    public string? SourceReference { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>One planned occurrence on either a modern AreaEquipment or legacy Equipment row.</summary>
public sealed class InspectionTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionPlanId { get; set; }
    public Guid? AreaEquipmentId { get; set; }
    public Guid? LegacyEquipmentId { get; set; }
    public InspectionTaskKind Kind { get; set; } = InspectionTaskKind.Regular;
    public string? ScheduleReference { get; set; }
    public string? WorkPackageReference { get; set; }
    public Guid? AssignedUserId { get; set; }
    public Guid? WorkPackageId { get; set; }
    public InspectionType? PlannedGrade { get; set; }
    public int? RouteOrder { get; set; }
    public string? Rotation { get; set; }
    public string? PriorityCode { get; set; }
    public string? RequiredPermits { get; set; }
    public string? IsolationPrerequisites { get; set; }
    public Guid? ChecklistVersionId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public bool IsInaccessible { get; set; }
    public string? CarryForwardReason { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public string StatusCode { get; set; } = "Planned";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Stable activity ID is server-generated. Review acceptance never changes linked defect states.</summary>
public sealed class InspectionActivity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid InspectionTaskId { get; set; }
    public Guid? LegacySurveySessionId { get; set; }
    public InspectionType? Grade { get; set; }
    public Guid InspectorUserId { get; set; }
    public string? InspectorNameSnapshot { get; set; }
    public string? EquipmentComments { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid ChecklistVersionId { get; set; }
    public InspectionActivityReviewStatus ReviewStatus { get; set; } = InspectionActivityReviewStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public ICollection<InspectionActivityRevision> Revisions { get; set; } = new List<InspectionActivityRevision>();
}

/// <summary>Append-only submitted content snapshot. A reviewed snapshot is never overwritten.</summary>
public sealed class InspectionActivityRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionActivityId { get; set; }
    public int RevisionNumber { get; set; } = 1;
    public string? ChangeReason { get; set; }
    public string ContentHash { get; set; } = "";
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public InspectionActivity? Activity { get; set; }
}

/// <summary>Immutable published checklist snapshot; no condition/response labels are seeded here.</summary>
public sealed class ChecklistVersionSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SourceChecklistTemplateId { get; set; }
    public int VersionNumber { get; set; }
    public InspectionType? InspectionType { get; set; }
    public string SnapshotHash { get; set; } = "";
    public DateTimeOffset PublishedAt { get; set; }
    public Guid PublishedByUserId { get; set; }
    public string? StandardReference { get; set; }
    public string? StandardEdition { get; set; }
    public string? ControlledSourceReference { get; set; }
    public string? ProtectionTypeSnapshot { get; set; }
}

public sealed class CheckpointDefinitionSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChecklistVersionSnapshotId { get; set; }
    public required string QuestionCode { get; set; }
    public int QuestionVersion { get; set; } = 1;
    public required string QuestionText { get; set; }
    public int SortOrder { get; set; }
    public string? ApplicabilityCode { get; set; }
    public string? ResponseScaleCode { get; set; }
    public bool IsRequired { get; set; }
    public bool NumericReadingEnabled { get; set; }
    public string? MeasurementUnit { get; set; }
    public decimal? MinimumReading { get; set; }
    public decimal? MaximumReading { get; set; }
    public bool NumericReadingRequired { get; set; }
    public string? SectionCode { get; set; }
    public bool RequireRemarkOnFail { get; set; }
}

/// <summary>Immutable response revision; later corrections append another revision number.</summary>
public sealed class CheckpointResponseRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivityRevisionId { get; set; }
    public Guid CheckpointDefinitionSnapshotId { get; set; }
    public int RevisionNumber { get; set; } = 1;
    public string? ApplicabilityResultCode { get; set; }
    public string? GradeResultsJson { get; set; }
    public string? InspectionComment { get; set; }
    public string? ResponseCode { get; set; }
    public string? TechnicalConditionCode { get; set; }
    public CheckpointWorkflowState WorkflowState { get; set; } = CheckpointWorkflowState.Open;
    public string? Observation { get; set; }
    public decimal? NumericValue { get; set; }
    public string? MeasurementUnitSnapshot { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RecordedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
}

public sealed class ActivityReviewEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionActivityId { get; set; }
    public InspectionActivityReviewStatus Decision { get; set; }
    public string? Comments { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTimeOffset DecidedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>New defect bridge; existing Finding IDs and status transitions remain untouched.</summary>
public sealed class ActivityFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionActivityId { get; set; }
    public Guid CheckpointResponseRevisionId { get; set; }
    public Guid? LegacyFindingId { get; set; }
    public required string Observation { get; set; }
    public string? ProposedAction { get; set; }
    public string? SeverityCode { get; set; }
    public string? AosdId { get; set; }
    public string? FaultCategory { get; set; }
    public string? RegisterRemarks { get; set; }
    public string? FollowUpRemarks { get; set; }
    public string? FailedGrades { get; set; }
    public string? FaultCode { get; set; }
    public string? RequiredMaterialsOrSupport { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public string? ResponsibleParty { get; set; }
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ReportedByUserId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public string StatusCode { get; set; } = "Open";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CorrectiveAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivityFindingId { get; set; }
    public required string ActionReference { get; set; }
    public string? SupplementaryInformation { get; set; }
    public string? ProposedAction { get; set; }
    public string? ExternalWorkOrderReference { get; set; }
    public string? RectificationDescription { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public string? ResponsibleParty { get; set; }
    public DateTimeOffset? RectificationAt { get; set; }
    public CorrectiveActionStatus Status { get; set; } = CorrectiveActionStatus.Proposed;
    public Guid? AriesVerifierUserId { get; set; }
    public DateTimeOffset? AriesVerifiedAt { get; set; }
    public Guid? ClientDecisionUserId { get; set; }
    public DateTimeOffset? ClientDecisionAt { get; set; }
    public string? LastDecisionComment { get; set; }
    public bool IsOutstanding { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CorrectiveActionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CorrectiveActionId { get; set; }
    public CorrectiveActionEventKind Kind { get; set; }
    public CorrectiveActionStatus ResultingStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public string? Comment { get; set; }
    public string? RectificationDescription { get; set; }
    public DateTimeOffset? RectificationAt { get; set; }
    public string? WorkOrderReference { get; set; }
    public string? ResponsibleParty { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>References existing SurveyFile blob metadata; does not duplicate file content.</summary>
public sealed class ActivityEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SurveyFileId { get; set; }
    public Guid? InspectionActivityId { get; set; }
    public Guid? AreaEquipmentId { get; set; }
    public Guid? LegacyEquipmentId { get; set; }
    public Guid? CheckpointResponseRevisionId { get; set; }
    public Guid? CorrectiveActionEventId { get; set; }
    public ActivityEvidenceRole Role { get; set; }
    public string? Caption { get; set; }
    public string? IntegrityHash { get; set; }
    public Guid AddedByUserId { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum CheckpointReviewDecision { Accepted, ChangesRequested }

public sealed class CheckpointReviewEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionActivityId { get; set; }
    public Guid CheckpointResponseRevisionId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public CheckpointReviewDecision Decision { get; set; }
    public string? Comments { get; set; }
    public DateTimeOffset ReviewedAt { get; set; } = DateTimeOffset.UtcNow;
}
