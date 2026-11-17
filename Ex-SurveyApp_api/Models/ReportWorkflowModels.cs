namespace Ex_SurveyApp_api.Models;

/// <summary>Project reporting lifecycle, independent of project, inspection and QC statuses.</summary>
public sealed class ProjectReportWorkflow
{
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = "Ongoing";
    public int Revision { get; set; } = 1;
    public long Version { get; set; }
    public string? HoldReason { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public Guid? SubmittedArtifactId { get; set; }
}

public sealed class ReportWorkflowEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string FromStatus { get; set; } = "Ongoing";
    public required string ToStatus { get; set; }
    public int Revision { get; set; }
    public long Version { get; set; }
    public string? Reason { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ArtifactId { get; set; }
}
