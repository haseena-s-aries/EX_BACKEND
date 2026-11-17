using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController, Authorize, Route("api/user-access")]
[EndpointScopeInventory(EndpointScopeKind.IdentityLifecycle)]
public class UserAccessController(SurveyDbContext db) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool Admin => User.IsInRole(nameof(UserRole.Admin));
    private UserAccessWorkflow Flow => new(db);

    [HttpGet("counts"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Counts(CancellationToken ct) => Ok(new {pendingRegistrations = await UserAccessWorkflow.Pending(db, Admin).CountAsync(ct), pendingRoleChanges = Admin ? await db.RoleChangeRequests.CountAsync(r => r.Status == RoleRequestStatus.Pending, ct) : 0});

    [HttpGet("role-requests")]
    public Task<List<RoleRequestView>> Mine(CancellationToken ct) => Views(db.RoleChangeRequests.Where(r => r.UserId == Actor)).ToListAsync(ct);

    [HttpPost("role-requests")]
    public async Task<IActionResult> RequestRole(RoleRequestWrite input, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == Actor, ct);
        if (!user.IsActive || user.ApprovalStatus != ApprovalStatus.Approved) return Forbid();
        if (input.RequestedRole is null || !Enum.IsDefined(input.RequestedRole.Value) || input.RequestedRole == user.Role || string.IsNullOrWhiteSpace(input.Reason)) return BadRequest(new {error = "Choose a different role and enter a reason."});
        if (await db.RoleChangeRequests.AnyAsync(r => r.UserId == Actor && r.Status == RoleRequestStatus.Pending, ct)) return Conflict(new {error = "You already have a pending role change request."});
        var request = new RoleChangeRequest {UserId = Actor, CurrentRole = user.Role, RequestedRole = input.RequestedRole.Value, Reason = input.Reason.Trim()};
        db.RoleChangeRequests.Add(request);
        var admins = await db.Users.Where(u => u.Role == UserRole.Admin && u.IsActive && u.ApprovalStatus == ApprovalStatus.Approved).Select(u => u.Id).ToListAsync(ct);
        foreach (var id in admins) Flow.Add(id, "role-request:" + request.Id, "RoleRequest", "Role Change Request", "A role change request is awaiting Admin approval.", user.Id, request.Id);
        Flow.Audit(user.Id, Actor, "Role Change Requested", user.Role + " → " + request.RequestedRole + ": " + request.Reason);
        try {await db.SaveChangesAsync(ct);} catch (DbUpdateException) {return Conflict(new {error = "The request could not be saved. Check whether another request is pending."});}
        return Ok(new {message = "Your role change request has been submitted and is waiting for Admin approval.", requestId = request.Id});
    }

    [HttpGet("role-requests/all"), Authorize(Roles = "Admin")]
    public Task<List<RoleRequestView>> All(CancellationToken ct) => Views(db.RoleChangeRequests).ToListAsync(ct);

    private IQueryable<RoleRequestView> Views(IQueryable<RoleChangeRequest> query) => query.OrderByDescending(r => r.RequestedAt).Join(db.Users, r => r.UserId, u => u.Id, (r, u) => new RoleRequestView(r.Id, u.Id, u.DisplayName, u.Email, r.CurrentRole, r.RequestedRole, r.Reason, r.Status, r.RequestedAt, r.ReviewedBy, r.ReviewedAt, r.RejectionReason));

    [HttpPut("role-requests/{id:guid}/review"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Review(Guid id, RoleReviewWrite input, CancellationToken ct)
    {
        if (input.Decision is not (RoleRequestStatus.Approved or RoleRequestStatus.Rejected)) return BadRequest(new {error = "Choose Approve or Reject."});
        if (input.Decision == RoleRequestStatus.Rejected && string.IsNullOrWhiteSpace(input.Reason)) return BadRequest(new {error = "A rejection reason is required."});
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var request = await db.RoleChangeRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return NotFound();
        if (request.Status != RoleRequestStatus.Pending) return Conflict(new {error = "This request has already been reviewed."});
        var user = await db.Users.SingleAsync(u => u.Id == request.UserId, ct);
        if (input.Decision == RoleRequestStatus.Approved)
        {
            if (!user.IsActive || user.ApprovalStatus != ApprovalStatus.Approved || user.Role != request.CurrentRole) return Conflict(new {error = "The account changed. Reject this request and ask the user to submit a new one."});
            var problem = await Flow.RoleProblem(user, request.RequestedRole, ct); if (problem is not null) return Conflict(new {error = problem});
            Flow.ApplyRole(user, request.RequestedRole, Actor);
        }
        request.Status = input.Decision.Value; request.ReviewedAt = DateTimeOffset.UtcNow; request.ReviewedBy = Actor;
        request.RejectionReason = request.Status == RoleRequestStatus.Rejected ? input.Reason!.Trim() : null;
        Flow.Audit(user.Id, Actor, "Role Request " + request.Status, request.RejectionReason ?? request.RequestedRole.ToString());
        Flow.Add(user.Id, "role-decision:" + request.Id, "Decision", "Role Change " + request.Status, request.Status == RoleRequestStatus.Approved ? "Your role change was approved. Sign in again to use your updated permissions." : "Your role change request was rejected. " + request.RejectionReason);
        try {await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);} catch (DbUpdateConcurrencyException) {return Conflict(new {error = "This request changed. Refresh and try again."});}
        return Ok(new {message = "Role change request " + request.Status.ToString().ToLowerInvariant() + "."});
    }

    private IQueryable<UserAccessNotification> Notifications() => db.UserAccessNotifications.Where(n => n.RecipientId == Actor && n.DismissedAt == null &&
        (n.Kind != "RoleRequest" || Admin) && (n.Kind != "Registration" || (Admin || (User.IsInRole("Manager") && db.Users.Any(u => u.Id == n.TargetUserId && u.Role != UserRole.Admin && u.Role != UserRole.Manager)))));

    [HttpGet("notifications")]
    public async Task<IActionResult> Notices(CancellationToken ct) => Ok(await Notifications().OrderByDescending(n => n.CreatedAt).ToListAsync(ct));

    [HttpPut("notifications/{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        var notice = await Notifications().SingleOrDefaultAsync(n => n.Id == id, ct); if (notice is null) return NotFound();
        notice.ReadAt ??= DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpPut("notifications/{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct)
    {
        var notice = await Notifications().SingleOrDefaultAsync(n => n.Id == id, ct); if (notice is null) return NotFound();
        notice.DismissedAt = DateTimeOffset.UtcNow; notice.ReadAt ??= notice.DismissedAt; await db.SaveChangesAsync(ct); return NoContent();
    }
}
public record RoleRequestWrite([Required] UserRole? RequestedRole, [Required, StringLength(2000)] string Reason);
public record RoleReviewWrite([Required] RoleRequestStatus? Decision, [StringLength(2000)] string? Reason);
public record RoleRequestView(Guid Id, Guid UserId, string Name, string Email, UserRole CurrentRole, UserRole RequestedRole, string Reason, RoleRequestStatus Status, DateTimeOffset RequestedAt, Guid? ReviewedBy, DateTimeOffset? ReviewedAt, string? RejectionReason);
