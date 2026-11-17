using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public sealed class UserAccessWorkflow(SurveyDbContext db)
{
    public static IQueryable<User> Pending(SurveyDbContext db, bool admin) => db.Users.Where(u => u.IsActive && u.ApprovalStatus == ApprovalStatus.Pending && (admin || (u.Role != UserRole.Admin && u.Role != UserRole.Manager)));

    public async Task Registration(User user, CancellationToken ct)
    {
        var recipients = await db.Users.Where(u => u.IsActive && u.ApprovalStatus == ApprovalStatus.Approved && (u.Role == UserRole.Admin || (u.Role == UserRole.Manager && user.Role != UserRole.Admin && user.Role != UserRole.Manager))).Select(u => u.Id).ToListAsync(ct);
        foreach (var id in recipients) Add(id, "registration:" + user.Id, "Registration", "New User Registration", "New user registration is awaiting approval.", user.Id);
        Audit(user.Id, null, "Registration Pending", user.Role.ToString());
    }
    public void Add(Guid recipient, string key, string kind, string title, string message, Guid? target = null, Guid? request = null)
        => db.UserAccessNotifications.Add(new() {RecipientId = recipient, EventKey = key, Kind = kind, Title = title, Message = message, TargetUserId = target, RoleRequestId = request});
    public void Audit(Guid id, Guid? actor, string action, string? detail) => db.AuditLogs.Add(new() {EntityType = "User", EntityId = id, ActorId = actor, Action = action, Comments = detail});

    public async Task<string?> RoleProblem(User user, UserRole role, CancellationToken ct)
    {
        if (!Enum.IsDefined(role)) return "Invalid role.";
        if (user.Role == UserRole.Manager && role != UserRole.Manager && await db.Users.AnyAsync(u => u.ManagerId == user.Id, ct)) return "Reassign reporting QC users before changing this Manager's role.";
        if (user.Role == UserRole.Admin && role != UserRole.Admin && !await db.Users.AnyAsync(u => u.Id != user.Id && u.Role == UserRole.Admin && u.IsActive && u.ApprovalStatus == ApprovalStatus.Approved, ct)) return "The last active approved Admin cannot change role.";
        return null;
    }
    public void ApplyRole(User user, UserRole role, Guid actor)
    {
        var previous = user.Role; user.Role = role; user.AuthVersion++;
        if (role != UserRole.Qc) user.ManagerId = null;
        Audit(user.Id, actor, "Role Changed", previous + " → " + role);
    }
}
