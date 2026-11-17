namespace Ex_SurveyApp_api.Models;

public enum RoleRequestStatus { Pending, Approved, Rejected }
public class RoleChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public UserRole CurrentRole { get; set; }
    public UserRole RequestedRole { get; set; }
    public required string Reason { get; set; }
    public RoleRequestStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}
public class UserAccessNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientId { get; set; }
    public required string EventKey { get; set; }
    public required string Kind { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public Guid? TargetUserId { get; set; }
    public Guid? RoleRequestId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? EquipmentId { get; set; }
    public Guid? InspectionActivityId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
}
