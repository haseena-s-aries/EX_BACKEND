namespace Ex_SurveyApp_api.Models;

public enum CampaignStatus { Draft, Planned, InProgress, Completed, OnHold, Archived }
public enum SamplingMethod { Random, RiskBased, EquipmentType, Manual }
public enum AllocationRoundingRule { Ceiling, Floor, Nearest }
public enum PlanningApprovalKind { Allocation, Sampling, Campaign }

public sealed class InspectionCampaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? InspectionLeadUserId { get; set; }
    public required string Name { get; set; }
    public required string Reference { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public decimal? ClosePercent { get; set; }
    public decimal? DetailedPercent { get; set; }
    public decimal? MinimumDetailedCreditPercent { get; set; }
    public AllocationRoundingRule? RoundingRule { get; set; }
    public int Revision { get; set; } = 1;
    public bool AllocationApproved { get; set; }
    public Guid? AllocationApprovedByUserId { get; set; }
    public DateTimeOffset? AllocationApprovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedByUserId { get; set; }
}

public sealed class CampaignAnnualList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public int Year { get; set; }
    public int EquipmentCount { get; set; }
    public int? PlannedCloseCount { get; set; }
    public int? PlannedDetailedCount { get; set; }
    public int? ActualCloseCount { get; set; }
    public int? ActualDetailedCount { get; set; }
    public int Revision { get; set; } = 1;
}

public sealed class CampaignEquipment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Guid AnnualListId { get; set; }
    public Guid AreaEquipmentId { get; set; }
    public string? RiskCode { get; set; }
    public decimal? RiskScore { get; set; }
    public string? EquipmentTypeSnapshot { get; set; }
}

public sealed class SamplingApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public int CampaignRevision { get; set; }
    public Guid AnnualListId { get; set; }
    public SamplingMethod Method { get; set; }
    public int RuleVersion { get; set; }
    public int? RandomSeed { get; set; }
    public string? Justification { get; set; }
    public string SelectedEquipmentIdsJson { get; set; } = "[]";
    public string? OverridesJson { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset SelectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ApprovedAt { get; set; }
    public bool IsFrozen { get; set; }
}

public sealed class InspectionWorkPackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Guid ProjectId { get; set; }
    public string Reference { get; set; } = "";
    public string? Site { get; set; }
    public string? Area { get; set; }
    public string? Rotation { get; set; }
    public string Status { get; set; } = "Planned";
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InspectionTaskAssignmentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionTaskId { get; set; }
    public Guid? PreviousUserId { get; set; }
    public Guid? NewUserId { get; set; }
    public Guid ActorUserId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PlanningApprovalEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public PlanningApprovalKind Kind { get; set; }
    public int Revision { get; set; }
    public Guid ActorUserId { get; set; }
    public string Decision { get; set; } = "";
    public string? Comments { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Immutable metadata for a generated artifact; file bytes live in protected filesystem storage.</summary>
public sealed class GeneratedReportArtifact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid LinkedEntityId { get; set; }
    public string ReportType { get; set; } = "";
    public string ReportNumber { get; set; } = "";
    public string? UidNumber { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentRevision { get; set; }
    public int Revision { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public string StorageKey { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid GeneratedByUserId { get; set; }
    /// <summary>Generated records begin as Draft. No automatic final/compliant state exists.</summary>
    public string ApprovalState { get; set; } = "Draft";
}

public sealed class InspectionRecurrence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Guid AnnualListId { get; set; }
    public InspectionType Grade { get; set; }
    public int EveryMonths { get; set; }
    public DateOnly NextOccurrenceOn { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastGeneratedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OfflineSyncReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ClientActivityId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string PayloadHash { get; set; }
    public Guid ServerActivityId { get; set; }
    public required string ResponseMapJson { get; set; }
    public DateTimeOffset AcceptedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InspectionActivityDocumentUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionActivityId { get; set; }
    public Guid InspectionTaskId { get; set; }
    public Guid UserId { get; set; }
    public required string DocumentIdentity { get; set; }
    public required string DocumentRevision { get; set; }
    public required string Sha256 { get; set; }
    public required string Title { get; set; }
    public required string Scope { get; set; }
    public Guid ScopeId { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OfflinePhotoSyncReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string ContentHash { get; set; }
    public Guid ActivityId { get; set; }
    public Guid? CheckpointResponseRevisionId { get; set; }
    public Guid EvidenceId { get; set; }
    public DateTimeOffset AcceptedAt { get; set; } = DateTimeOffset.UtcNow;
}
