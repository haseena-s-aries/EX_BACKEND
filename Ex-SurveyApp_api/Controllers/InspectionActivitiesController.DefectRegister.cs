using System.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

public sealed partial class InspectionActivitiesController
{
    [HttpPost("defects/reconcile/{projectId:guid}")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> ReconcileFailedCheckpoints(Guid projectId,
        [FromServices] InspectionActivityAuthorizer authorizer, CancellationToken ct) => ExecuteService(async () =>
    {
        if (!options.Value.Enabled) return NotFound();
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Manager, UserRole.Admin);
        await authorizer.EnsureProjectScopeAsync(projectId, ct);
        if (!await CanAccessProject(projectId, ct)) return NotFound();

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var surveyor = actor.Role == UserRole.Surveyor;
        var activitiesInProject = from activity in db.InspectionActivities.AsNoTracking()
                                  join task in db.InspectionTasks.AsNoTracking() on activity.InspectionTaskId equals task.Id
                                  join plan in db.InspectionPlans.AsNoTracking() on task.InspectionPlanId equals plan.Id
                                  where plan.ProjectId == projectId && (!surveyor || task.AssignedUserId == actor.Id)
                                  select activity;
        var failed = await (from activity in activitiesInProject
                            join revision in db.InspectionActivityRevisions.AsNoTracking() on activity.Id equals revision.InspectionActivityId
                            join response in db.CheckpointResponseRevisions.AsNoTracking() on revision.Id equals response.ActivityRevisionId
                            join definition in db.CheckpointDefinitionSnapshots.AsNoTracking() on response.CheckpointDefinitionSnapshotId equals definition.Id
                            where response.ResponseCode == "Fail" &&
                                  !db.InspectionActivityRevisions.Any(newer => newer.InspectionActivityId == activity.Id && newer.RevisionNumber > revision.RevisionNumber) &&
                                  !db.CheckpointResponseRevisions.Any(newer => newer.ActivityRevisionId == revision.Id &&
                                      newer.CheckpointDefinitionSnapshotId == definition.Id && newer.RevisionNumber > response.RevisionNumber)
                            select new {activity, definition, response}).ToListAsync(ct);
        foreach (var item in failed)
            await FailedCheckpointDefects.EnsureAsync(db, item.activity, item.definition, item.response,
                item.response.RecordedByUserId, ct);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return NoContent();
    });

    [HttpPut("defects/{findingId:guid}/register-details")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public Task<IActionResult> SaveInspectionDefectDetails(Guid findingId, InspectionDefectDetails request,
        [FromServices] InspectionActivityAuthorizer authorizer, CancellationToken ct) => ExecuteService(async () =>
    {
        if (!options.Value.Enabled) return NotFound();
        var actor = await authorizer.RequireActorAsync(ActorId, ct, UserRole.Surveyor, UserRole.Manager, UserRole.Admin);
        var finding = await db.ActivityFindings.SingleOrDefaultAsync(x => x.Id == findingId, ct);
        if (finding is null) return NotFound();
        var (_, task) = await authorizer.EnsureActivityScopeAsync(finding.InspectionActivityId, ct);
        if (actor.Role == UserRole.Surveyor && task.AssignedUserId != actor.Id) return Forbid();
        if (finding.StatusCode == "ClientClosed" || await db.CorrectiveActions.AnyAsync(a =>
            a.ActivityFindingId == finding.Id && a.Status == CorrectiveActionStatus.ClientClosed, ct))
            return Conflict(new {error = "Closed defects are read-only."});
        if (request.FaultCategory is not (null or "" or "A" or "B" or "C"))
            return BadRequest(new {error = "Fault category must be A, B, or C."});
        if (request.AosdId?.Length > 100 || request.FaultCode?.Length > 100 || request.Recommendation?.Length > 4000 ||
            request.Remarks?.Length > 2000 || request.AdditionalRemarks?.Length > 2000)
            return BadRequest(new {error = "Use at most 100 characters for AOSD ID and fault code, 4,000 for recommendation, and 2,000 per remark."});
        finding.AosdId = CleanRegisterValue(request.AosdId);
        finding.FaultCategory = CleanRegisterValue(request.FaultCategory);
        finding.FaultCode = CleanRegisterValue(request.FaultCode);
        finding.ProposedAction = CleanRegisterValue(request.Recommendation);
        finding.RegisterRemarks = CleanRegisterValue(request.Remarks);
        finding.FollowUpRemarks = CleanRegisterValue(request.AdditionalRemarks);
        await db.SaveChangesAsync(ct);
        return NoContent();
    });

    private static string? CleanRegisterValue(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record InspectionDefectDetails(string? AosdId, string? FaultCategory, string? FaultCode,
    string? Recommendation, string? Remarks, string? AdditionalRemarks);
