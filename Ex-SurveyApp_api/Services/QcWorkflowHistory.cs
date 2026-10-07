using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;

namespace Ex_SurveyApp_api.Services;

// Append-only snapshots, saved in the same transaction as the existing workflow change.
public static class QcWorkflowHistory
{
    public const string EntityType = "QcWorkflow";
    public static void Record(SurveyDbContext db, Guid inspectionId, Guid actorId,
        string action, QcReview? review = null, string? comments = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = EntityType, EntityId = inspectionId, ActorId = actorId,
            Action = action, Comments = comments ?? review?.Comments,
            Severity = review?.Severity, QcStatus = review?.Status,
            ReviewStartedAt = review?.StartedAt, ReviewedAt = review?.ReviewedAt
        });
    }
}
