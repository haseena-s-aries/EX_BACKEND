namespace Ex_SurveyApp_api.Models;

public enum ReferenceDocumentStatus { Draft, Submitted, Approved, Superseded, Archived }

/// <summary>One immutable file revision attached to existing project/area/equipment identities.</summary>
public sealed class EquipmentReferenceDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? EquipmentId { get; set; }
    public Guid? SupersedesDocumentId { get; set; }
    public required string Title { get; set; }
    public required string CategoryCode { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Revision { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? IssuingOrganization { get; set; }
    public required string OriginalFileName { get; set; }
    public required string StoragePath { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public ReferenceDocumentStatus Status { get; set; } = ReferenceDocumentStatus.Draft;
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovalComment { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}

/// <summary>Required-document policies are inert until explicitly approved.</summary>
public sealed class EquipmentDocumentRequirementPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public string? EquipmentType { get; set; }
    public Guid? WorkPackageId { get; set; }
    public required string CategoryCode { get; set; }
    public bool IsRequired { get; set; }
    public bool IsApproved { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class EquipmentDocumentUseEvent
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }
    public Guid UserId { get; set; }
    public Guid? EquipmentId { get; set; }
    public Guid? WorkPackageId { get; set; }
    public Guid DocumentId { get; set; }
    public required string DocumentRevision { get; set; }
    public required string Sha256 { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EquipmentReferenceDocumentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Guid ActorUserId { get; set; }
    public required string EventCode { get; set; }
    public string? Comments { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
