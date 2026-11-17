using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

public partial class ProjectController
{
    private sealed record QcAssignmentAudit(Guid PreviousPrimary, Guid? PreviousAdditional, Guid Primary, Guid? Additional);
    private const string SurveyorAssignment = "Surveyor Assignment";

    private IQueryable<Project> ManageSurveyors()
    {
        var projects = db.Projects.AsQueryable();
        return User.IsInRole(nameof(UserRole.Admin)) ? projects :
            projects.Where(p => p.CreatedByUserId == CurrentUserId ||
                db.Users.Any(q => q.Id == p.AssignedQcId && q.ManagerId == CurrentUserId));
    }

    [HttpGet("{id:guid}/surveyors")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Surveyors(Guid id, CancellationToken ct)
    {
        var project = await ManageSurveyors().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null) return NotFound();
        var events = await db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityId == id && x.EntityType == "Project" && x.Action == SurveyorAssignment)
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new { x.Id, x.ActorId, x.OccurredAt, x.Comments }).ToListAsync(ct);
        var history = events.Select(x => new { Event = x, Assignment = JsonSerializer.Deserialize<AssignmentAudit>(x.Comments!)! }).ToList();
        var ids = history.SelectMany(x => new[] { x.Assignment.PreviousSurveyorId, x.Assignment.NewSurveyorId, x.Event.ActorId ?? Guid.Empty }).Distinct().ToArray();
        var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var qcEvents = await db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityId == id && x.EntityType == "Project" && x.Action == "QC Assignment")
            .OrderByDescending(x => x.OccurredAt).ToListAsync(ct);
        var qcHistory = qcEvents.Select(x => new { Event = x, Assignment = JsonSerializer.Deserialize<QcAssignmentAudit>(x.Comments!)! }).ToArray();
        var qcIds = qcHistory.SelectMany(x => new Guid?[] { x.Event.ActorId, x.Assignment.PreviousPrimary,
            x.Assignment.PreviousAdditional, x.Assignment.Primary, x.Assignment.Additional }).Distinct().ToArray();
        var qcNames = await db.Users.Where(u => qcIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        string? QcName(Guid? userId) => userId is Guid value ? qcNames.GetValueOrDefault(value, value.ToString()) : null;
        return Ok(new
        {
                        primarySurveyorId = project.AssignedSurveyorId, project.AdditionalSurveyorId,
            qcHistory = qcHistory.Select(x => new { x.Event.Id, x.Event.OccurredAt, actorName = QcName(x.Event.ActorId),
                previousPrimary = QcName(x.Assignment.PreviousPrimary), previousAdditional = QcName(x.Assignment.PreviousAdditional),
                primary = QcName(x.Assignment.Primary), additional = QcName(x.Assignment.Additional) }),
            history = history.Select(x => new { x.Event.Id, x.Event.ActorId, x.Event.OccurredAt,
                x.Assignment, actorName = names.GetValueOrDefault(x.Event.ActorId ?? Guid.Empty),
                previousName = names.GetValueOrDefault(x.Assignment.PreviousSurveyorId),
                newName = names.GetValueOrDefault(x.Assignment.NewSurveyorId), status = "Applied" })
        });
    }

    [HttpPost("{id:guid}/surveyors")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> ChangeSurveyors(Guid id, SurveyorAssignmentRequest request, CancellationToken ct)
    {
        var project = await ManageSurveyors().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null) return NotFound();
        if (project.AssignedSurveyorId != request.PrimarySurveyorId || project.AdditionalSurveyorId != request.AdditionalSurveyorId)
            return Conflict(new { error = "The assignment changed. Reload the project and try again." });
        if (request.Type is not ("Shared" or "Full" or "Remove") || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "Select an assignment type and enter a reason." });
        if (request.Type != "Remove" && (request.NewSurveyorId == project.AssignedSurveyorId ||
            !await db.Users.AnyAsync(x => x.Id == request.NewSurveyorId && x.Role == UserRole.Surveyor &&
                x.IsActive && x.ApprovalStatus == ApprovalStatus.Approved, ct)))
            return BadRequest(new { error = "Choose another active, approved Surveyor." });
        var tenant = HttpContext.RequestServices.GetService<ITenantContextAccessor>()?.Current;
        if (request.Type != "Remove" && tenant is not null && !await db.TenantMemberships.AnyAsync(m =>
            m.TenantId == tenant.TenantId && m.UserId == request.NewSurveyorId && m.Status == TenantMembershipStatus.Active, ct))
            return BadRequest(new { error = "Choose a Surveyor with an active membership in this workspace." });
        if (request.Type == "Shared" && project.AdditionalSurveyorId.HasValue ||
            request.Type == "Remove" && !project.AdditionalSurveyorId.HasValue ||
            request.Type == "Full" && request.KeepPrevious && project.AdditionalSurveyorId.HasValue &&
                project.AdditionalSurveyorId != request.NewSurveyorId)
            return Conflict(new { error = "This project supports one additional Surveyor. Review the current assignment first." });

        var previousAssignments = AssignmentMembers(project);
        var previous = request.Type == "Remove" ? project.AdditionalSurveyorId!.Value : project.AssignedSurveyorId;
        var next = request.Type == "Remove" ? project.AssignedSurveyorId : request.NewSurveyorId;
        var reason = request.Reason.Trim();
        if (request.Type == "Full")
            await TransferPrimary(project, next, request.KeepPrevious, reason, ct);
        else
        {
            project.AdditionalSurveyorId = request.Type == "Shared" ? next : null;
            if (request.Type == "Remove") await TransferPendingTasks(id, previous, next, reason, ct);
            RecordAssignment(project.Id, previous, next, request.Type, reason);
        }

        project.UpdatedAt = DateTimeOffset.UtcNow;
        await RecordAssignmentNotices(project, previousAssignments, ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "The assignment changed. Reload and try again." }); }
        return await Surveyors(id, ct);
    }

    private async Task TransferPrimary(Project project, Guid next, bool keepPrevious, string reason, CancellationToken ct)
    {
        var previous = project.AssignedSurveyorId;
        await TransferPendingTasks(project.Id, previous, next, reason, ct);
        project.AssignedSurveyorId = next;
        if (keepPrevious) project.AdditionalSurveyorId = previous;
        else if (project.AdditionalSurveyorId == next) project.AdditionalSurveyorId = null;
        RecordAssignment(project.Id, previous, next, "Full", reason);
    }

    private async Task TransferPendingTasks(Guid projectId, Guid previous, Guid next, string reason, CancellationToken ct)
    {
        var tasks = await db.InspectionTasks.Where(t => t.AssignedUserId == previous &&
            (t.StatusCode == "Planned" || t.StatusCode == "InProgress" || t.StatusCode == "CategoryChecklist" || t.StatusCode == "CarryForward") &&
            db.InspectionPlans.Any(p => p.Id == t.InspectionPlanId && p.ProjectId == projectId) &&
            !db.InspectionActivities.Any(a => a.InspectionTaskId == t.Id &&
                a.ReviewStatus != InspectionActivityReviewStatus.Draft && a.ReviewStatus != InspectionActivityReviewStatus.ChangesRequested))
            .ToListAsync(ct);
        foreach (var task in tasks)
        {
            task.AssignedUserId = next;
            db.InspectionTaskAssignmentEvents.Add(new InspectionTaskAssignmentEvent
            {
                InspectionTaskId = task.Id, PreviousUserId = previous, NewUserId = next, ActorUserId = CurrentUserId, Reason = reason
            });
        }
    }

    private void RecordAssignment(Guid id, Guid previous, Guid next, string type, string reason) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = CurrentUserId, EntityId = id, EntityType = "Project", Action = SurveyorAssignment,
            Comments = JsonSerializer.Serialize(new AssignmentAudit(previous, next, type, reason))
        });

    public sealed record AssignmentAudit(Guid PreviousSurveyorId, Guid NewSurveyorId, string Type, string Reason);
    public sealed record SurveyorAssignmentRequest(Guid PrimarySurveyorId, Guid? AdditionalSurveyorId,
        Guid NewSurveyorId, string Type, [StringLength(1000)] string Reason, bool KeepPrevious);
}
