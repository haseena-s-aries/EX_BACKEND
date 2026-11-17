namespace Ex_SurveyApp_api.Models;

/// <summary>Committed in the same transaction as a browser fieldwork mutation.</summary>
public sealed class FieldworkReceipt
{
    public Guid UserId { get; set; }
    public Guid OperationId { get; set; }
    public Guid ProjectId { get; set; }
    public string RequestHash { get; set; } = "";
    public string ResponseJson { get; set; } = "";
    public int StatusCode { get; set; }
    public DateTimeOffset CommittedAt { get; set; } = DateTimeOffset.UtcNow;
}
