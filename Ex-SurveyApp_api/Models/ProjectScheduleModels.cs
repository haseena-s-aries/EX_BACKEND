namespace Ex_SurveyApp_api.Models;

public sealed class ProjectScheduleEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? ActorId { get; set; }
    public string Kind { get; set; } = "";
    public string EventKey { get; set; } = "";
    public DateOnly? TargetDate { get; set; }
    public DateOnly? PreviousTargetDate { get; set; }
    public DateOnly? WorkStartDate { get; set; }
    public DateOnly? ExpectedWorkEndDate { get; set; }
    public string? DelayReason { get; set; }
    public int DelayDays { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProjectDelayEmail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScheduleEntryId { get; set; }
    public Guid RecipientId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset? AttemptedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}
