using System.Text.Json;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

internal static class FailedCheckpointDefects
{
    internal static Guid IdFor(Guid activityId, Guid definitionId) =>
        SampleChecklistImporter.StableId($"failed-checkpoint/{activityId:D}/{definitionId:D}");

    internal static async Task EnsureAsync(SurveyDbContext db, InspectionActivity activity,
        CheckpointDefinitionSnapshot definition, CheckpointResponseRevision response, Guid actorId, CancellationToken ct)
    {
        var automaticId = IdFor(activity.Id, definition.Id);
        var existing = await (from finding in db.ActivityFindings
                              join prior in db.CheckpointResponseRevisions on finding.CheckpointResponseRevisionId equals prior.Id
                              where finding.InspectionActivityId == activity.Id && prior.CheckpointDefinitionSnapshotId == definition.Id
                              orderby finding.ReportedAt, finding.Id
                              select finding).FirstOrDefaultAsync(ct);
        var failures = response.GradeResultsJson is { } json
            ? JsonSerializer.Deserialize<Dictionary<string, string?>>(json)!.Where(pair => pair.Value == "Fail").Select(pair => pair.Key)
            : activity.Grade switch { InspectionType.Detailed => ["D"], InspectionType.Close => ["C"], InspectionType.Visual => ["V"], _ => Array.Empty<string>() };
        var grades = (existing?.FailedGrades ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Concat(failures).ToHashSet();
        var failedGrades = string.Join(",", new[] {"D", "C", "V"}.Where(grades.Contains));
        var fallback = $"Failed checkpoint: {definition.QuestionText}";
        if (fallback.Length > 4000) fallback = fallback[..4000];
        var observation = !string.IsNullOrWhiteSpace(response.Observation) ? response.Observation : response.InspectionComment;
        if (existing is not null)
        {
            if (existing.Id == automaticId && existing.Observation == fallback && !string.IsNullOrWhiteSpace(observation))
                existing.Observation = observation;
            if (failedGrades.Length > 0) existing.FailedGrades = failedGrades;
            return;
        }
        db.ActivityFindings.Add(new ActivityFinding {
            Id = automaticId,
            InspectionActivityId = activity.Id, CheckpointResponseRevisionId = response.Id,
            FailedGrades = failedGrades.Length == 0 ? null : failedGrades,
            Observation = string.IsNullOrWhiteSpace(observation) ? fallback : observation,
            ReportedAt = response.RecordedAt, ReportedByUserId = actorId
        });
    }
}
