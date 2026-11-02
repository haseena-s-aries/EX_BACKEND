using Ex_SurveyApp_api.Models;

namespace Ex_SurveyApp_api.Contracts.V1;

// Tenant ownership is deliberately absent from every request. The server resolves it through persisted parents.
public sealed record CreateInspectionPlanRequest(Guid ProjectId, string Name, string? SourceReference, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt);
public sealed record CreateInspectionTaskRequest(Guid PlanId, Guid? AreaEquipmentId, Guid? LegacyEquipmentId, InspectionTaskKind Kind, string? ScheduleReference, string? WorkPackageReference, Guid? AssignedUserId, DateTimeOffset? ScheduledAt);
public sealed record CreateInspectionActivityRequest(InspectionType Grade, Guid ChecklistVersionId, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt);
public sealed record SaveCheckpointResponseRequest(Guid CheckpointDefinitionSnapshotId, string? ApplicabilityResultCode, string? ResponseCode, string? TechnicalConditionCode, string? Observation, string ActivityRowVersion, decimal? NumericValue = null);
public sealed record AddActivityFindingRequest(Guid CheckpointResponseRevisionId, string Observation, string? ProposedAction, string? SeverityCode, string? FaultCode = null, string? RequiredMaterialsOrSupport = null, Guid? ResponsibleUserId = null, DateTimeOffset? DueAt = null, string? ResponsibleParty = null);
public sealed record AddActivityEvidenceRequest(Guid SurveyFileId, Guid? CheckpointResponseRevisionId, Guid? CorrectiveActionId, Guid? CorrectiveActionEventId, ActivityEvidenceRole Role, string? Caption, string? IntegrityHash);
public sealed record CreateCorrectiveActionRequest(Guid ActivityFindingId, string ActionReference, string? SupplementaryInformation, string? ProposedAction, string? ExternalWorkOrderReference, Guid? ResponsibleUserId, string? ResponsibleParty = null);
public sealed record ActivityReviewDecisionRequest(InspectionActivityReviewStatus Decision, string? Comments);
public sealed record CorrectiveActionVerificationRequest(string? Comments, bool AcceptCorrection = true);
public sealed record CorrectiveActionReportRequest(string Description, string? WorkOrderReference, DateTimeOffset RectificationAt, Guid? ResponsibleUserId, string? Comments);
public sealed record ClientClosureDecisionRequest(bool AcceptClosure, string? Comments);

public sealed record InspectionActivityResponse(Guid Id, Guid TaskId, InspectionType Grade, Guid ChecklistVersionId, InspectionActivityReviewStatus ReviewStatus, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string RowVersion);
public sealed record InspectionPlanResponse(Guid Id, Guid ProjectId, string Name, string? SourceReference, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt);
public sealed record InspectionTaskResponse(Guid Id, Guid PlanId, Guid? AreaEquipmentId, Guid? LegacyEquipmentId, InspectionTaskKind Kind, string? ScheduleReference, string? WorkPackageReference, Guid? AssignedUserId, DateTimeOffset? ScheduledAt, string StatusCode);
public sealed record CheckpointResponseResponse(Guid Id, Guid CheckpointDefinitionSnapshotId, int RevisionNumber, string? ResponseCode, string? TechnicalConditionCode, decimal? NumericValue, string? MeasurementUnit, bool? IsOutOfRange, CheckpointWorkflowState WorkflowState, DateTimeOffset RecordedAt);
public sealed record ActivityFindingResponse(Guid Id, Guid InspectionActivityId, Guid CheckpointResponseRevisionId, string StatusCode);
public sealed record ActivityEvidenceResponse(Guid Id, Guid SurveyFileId, Guid? CheckpointResponseRevisionId, Guid? CorrectiveActionEventId, ActivityEvidenceRole Role, DateTimeOffset AddedAt);
public sealed record CorrectiveActionResponse(Guid Id, string ActionReference, CorrectiveActionStatus Status, DateTimeOffset CreatedAt);
public sealed record DefectStatusResponse(Guid FindingId, Guid ActionId, string ActionReference, string FaultCode, string Observation, string? Severity, string Status, DateTimeOffset ReportedAt, DateTimeOffset? DueAt, string? WorkOrderReference, DateTimeOffset? RectificationAt, string? ResponsibleName, IReadOnlyList<object> History, IReadOnlyList<object> Evidence);
public sealed record CheckpointReviewRequest(Guid CheckpointResponseRevisionId, CheckpointReviewDecision Decision, string? Comments);
public sealed record ChecklistMeasurementConfigurationRequest(int ExpectedTemplateVersion, bool Enabled, string? Unit, decimal? Minimum, decimal? Maximum, bool Required);

public static class InspectionActivityContractValidation
{
    public static void Validate(CreateInspectionPlanRequest request)
    {
        if (request.ProjectId == Guid.Empty) throw new ArgumentException("A project is required.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200) throw new ArgumentException("Plan name must be 1 to 200 characters.");
        if (request.StartsAt.HasValue && request.EndsAt.HasValue && request.EndsAt < request.StartsAt) throw new ArgumentException("Plan end must not precede its start.");
    }

    public static void Validate(CreateInspectionTaskRequest request)
    {
        if (request.PlanId == Guid.Empty) throw new ArgumentException("A plan is required.");
        if (!Enum.IsDefined(request.Kind)) throw new ArgumentException("Task kind is invalid.");
        if ((request.AreaEquipmentId.HasValue ? 1 : 0) + (request.LegacyEquipmentId.HasValue ? 1 : 0) != 1) throw new ArgumentException("Select exactly one equipment record.");
        if (request.AreaEquipmentId == Guid.Empty || request.LegacyEquipmentId == Guid.Empty) throw new ArgumentException("Equipment ID cannot be empty.");
    }

    public static void Validate(CreateInspectionActivityRequest request)
    {
        if (!Enum.IsDefined(request.Grade)) throw new ArgumentException("Inspection grade is invalid.");
        if (request.ChecklistVersionId == Guid.Empty) throw new ArgumentException("A published checklist version is required.");
        if (request.StartedAt == default || request.CompletedAt is not null && request.CompletedAt < request.StartedAt) throw new ArgumentException("Activity timestamps are invalid.");
        if (request.Grade is not (InspectionType.Visual or InspectionType.Close or InspectionType.Detailed)) throw new ArgumentException("Inspection grade is invalid.");
    }

    public static void Validate(SaveCheckpointResponseRequest request)
    {
        if (request.CheckpointDefinitionSnapshotId == Guid.Empty) throw new ArgumentException("Checkpoint is required.");
        if (string.IsNullOrWhiteSpace(request.ActivityRowVersion) || !Convert.TryFromBase64String(request.ActivityRowVersion, new byte[64], out _)) throw new ArgumentException("A valid activity concurrency token is required.");
        if (request.Observation?.Length > 4000) throw new ArgumentException("Observation exceeds 4000 characters.");
        if (request.NumericValue is decimal numeric && (numeric > 99999999999999.9999m || numeric < -99999999999999.9999m)) throw new ArgumentException("Numeric reading exceeds supported precision.");
    }

    public static void Validate(AddActivityFindingRequest request)
    {
        if (request.CheckpointResponseRevisionId == Guid.Empty || string.IsNullOrWhiteSpace(request.Observation) || request.Observation.Length > 4000) throw new ArgumentException("A checkpoint response and finding observation are required.");
        if (request.ProposedAction?.Length > 4000) throw new ArgumentException("Proposed action exceeds 4000 characters.");
        if (request.FaultCode?.Length > 100 || request.RequiredMaterialsOrSupport?.Length > 2000 || request.ResponsibleParty?.Length > 200) throw new ArgumentException("Defect metadata exceeds supported length.");
        if (request.DueAt is DateTimeOffset due && due < DateTimeOffset.UtcNow.AddYears(-1)) throw new ArgumentException("Defect due date is invalid.");
    }

    public static void Validate(CreateCorrectiveActionRequest request)
    {
        if (request.ActivityFindingId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActionReference) || request.ActionReference.Length > 100) throw new ArgumentException("Finding and action reference are required.");
        if (request.ExternalWorkOrderReference?.Length > 200) throw new ArgumentException("External work-order reference exceeds 200 characters.");
        if (request.ResponsibleParty?.Length > 200) throw new ArgumentException("Responsible party exceeds 200 characters.");
    }

    public static void Validate(ChecklistMeasurementConfigurationRequest request)
    {
        if (request.ExpectedTemplateVersion < 1) throw new ArgumentException("Expected checklist version is required.");
        if (request.Enabled && (string.IsNullOrWhiteSpace(request.Unit) || request.Unit.Length > 40)) throw new ArgumentException("An engineering unit of 1 to 40 characters is required for numeric readings.");
        if (request.Minimum.HasValue && request.Maximum.HasValue && request.Minimum > request.Maximum) throw new ArgumentException("Minimum reading cannot be greater than maximum reading.");
        if (!request.Enabled && (request.Required || request.Unit is not null || request.Minimum.HasValue || request.Maximum.HasValue)) throw new ArgumentException("Disable the reading and clear its unit/limits/required setting together.");
    }

    public static void Validate(CorrectiveActionReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 4000) throw new ArgumentException("Corrective-action description is required (maximum 4000 characters).");
        if (request.WorkOrderReference?.Length > 200 || request.Comments?.Length > 4000) throw new ArgumentException("Rectification details exceed supported length.");
        if (request.RectificationAt == default || request.RectificationAt > DateTimeOffset.UtcNow.AddDays(1)) throw new ArgumentException("Rectification date is invalid.");
    }
}
