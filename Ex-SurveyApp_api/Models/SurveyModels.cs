namespace Ex_SurveyApp_api.Models;

public enum UserRole { Admin, Manager, Qc, Surveyor, Client }
public enum ApprovalStatus { Pending, Approved, Rejected }
public enum JobStatus { Draft, InProgress, Submitted, InReview, Approved, Closed }
public enum InspectionType { Visual, Close, Detailed }
public enum SurveyStatus { Draft, Submitted, Returned, Approved }
public enum FindingStatus { Open, RectificationSubmitted, Verified, Closed }
public enum FileCategory { Drawing, Checklist, EquipmentPhoto, FindingPhoto, RepairProof, Certificate, Report, ProjectCommonLayout }
public enum QcReviewStatus { PendingReview, InReview, ChangesRequested, Approved }
public enum QcSeverity { Low, Medium, High, Critical }
public enum EquipmentInspectionStatus { NotStarted, InProgress, Hold, Completed, PendingQcReview, ChangesRequested, QcApproved }
public enum ChecklistLifecycleStatus { Draft, PendingTechnicalApproval, Published, Archived }

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public Guid? ApprovalReviewedByUserId { get; set; }
    public DateTimeOffset? ApprovalReviewedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PhoneNumber { get; set; }
    public string? Designation { get; set; }
    public string? ProfilePhotoPath { get; set; }
    public int AuthVersion { get; set; }
    public DateTimeOffset? RegisteredAt { get; set; }
    public string? ApprovalRejectionReason { get; set; }
}

public class AccountActionToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Purpose { get; set; }
    public required string TokenHash { get; set; }
    public string? NewEmail { get; set; }
    public int AuthVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public class RegistrationOtp
{
    public const int MaximumVerificationAttempts = 5;
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public UserRole RequestedRole { get; set; }
    public required string Otp { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastSentAt { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public int VerificationAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public int ConcurrencyVersion { get; set; }
}

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? ContactEmail { get; set; }
    public Guid? LogoFileId { get; set; }
}

public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public required string JobNumber { get; set; }
    public required string Title { get; set; }
    public string? Uid { get; set; }
    public int Revision { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class JobAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Guid UserId { get; set; }
    public UserRole Role { get; set; }
}

public class Area
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class Equipment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AreaId { get; set; }
    public required string TagNumber { get; set; }
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? ExMarking { get; set; }
    public string? RfidTag { get; set; }
}

public class ChecklistTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public InspectionType InspectionType { get; set; }
    public int Version { get; set; } = 1;
    public int DraftRevision { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public Guid? FamilyId { get; set; }
    public ChecklistLifecycleStatus LifecycleStatus { get; set; } = ChecklistLifecycleStatus.Published;
    public string? StandardReference { get; set; }
    public string? StandardEdition { get; set; }
    public string? ControlledSourceReference { get; set; }
    public string? ApplicableProtectionTypes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
}

public class ChecklistQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChecklistTemplateId { get; set; }
    public required string Text { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool NumericReadingEnabled { get; set; }
    public string? MeasurementUnit { get; set; }
    public decimal? MinimumReading { get; set; }
    public decimal? MaximumReading { get; set; }
    public bool NumericReadingRequired { get; set; }
    public string? SectionCode { get; set; }
    public string? ApplicableProtectionTypes { get; set; }
    public string? SourceItemReference { get; set; }
    public int? SourceItemNumber { get; set; }
    public string? ApplicableGrades { get; set; }
    public string? SubChecksJson { get; set; }
    public bool RequireRemarkOnFail { get; set; }
    public string? ResponseScaleCode { get; set; }
}

public class SurveySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? JobId { get; set; }
    public Guid? EquipmentId { get; set; }
    public Guid? AreaEquipmentId { get; set; }
    public Guid SurveyorId { get; set; }
    public Guid? ChecklistTemplateId { get; set; }
    public SurveyStatus Status { get; set; } = SurveyStatus.Draft;
    public string? InspectorName { get; set; }
    public string? SurveyorRemarks { get; set; }
    public DateTimeOffset? InspectionTimestamp { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? LastModifiedByUserId { get; set; }
}

public class SurveyAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SurveySessionId { get; set; }
    public Guid ChecklistQuestionId { get; set; }
    public string? Value { get; set; }
    public string? Comment { get; set; }
}

public class Finding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SurveySessionId { get; set; }
    public Guid? RegisterEquipmentId { get; set; }
    public Guid? RegisterAuthorId { get; set; }
    public DateOnly? RegisterDate { get; set; }
    public InspectionType? RegisterGrade { get; set; }
    public string? AosdId { get; set; }
    public string? FaultCategory { get; set; }
    public string? FaultCode { get; set; }
    public string? Remarks { get; set; }
    public string? AdditionalRemarks { get; set; }
    public required string Observation { get; set; }
    public string? Recommendation { get; set; }
    public string? Severity { get; set; }
    public FindingStatus Status { get; set; } = FindingStatus.Open;
}

public class SurveyFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? JobId { get; set; }
    public Guid? SurveySessionId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? EquipmentId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? AreaEquipmentId { get; set; }
    public Guid? FindingId { get; set; }
    public string? EquipmentPhotoRole { get; set; }
    public FileCategory Category { get; set; }
    public required string FileName { get; set; }
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public Guid UploadedById { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class QcReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SurveySessionId { get; set; }
    public Guid ReviewedByUserId { get; set; }
    public QcReviewStatus Status { get; set; } = QcReviewStatus.PendingReview;
    public string? Comments { get; set; }
    public QcSeverity? Severity { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

public class ReportRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public int Revision { get; set; }
    public Guid? FileId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public required string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public required string Action { get; set; }
    public string? Comments { get; set; }
    public QcSeverity? Severity { get; set; }
    public QcReviewStatus? QcStatus { get; set; }
    public DateTimeOffset? ReviewStartedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public class SyncBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SurveyorId { get; set; }
    public required string ClientBatchId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
