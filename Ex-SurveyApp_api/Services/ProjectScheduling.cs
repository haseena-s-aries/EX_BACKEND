using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ex_SurveyApp_api.Services;

public sealed class ProjectScheduling(SurveyDbContext db, IOptions<TenantFoundationOptions> tenantOptions)
{
    // Dubai's business calendar uses UTC+04:00 year round.
    public static DateOnly Today => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(4)).DateTime);
    public static string OverdueKey(DateOnly target) => $"overdue/{target:yyyy-MM-dd}";

    public static ProjectScheduleSummary Summarize(ProjectStatus status, DateOnly? target, Guid primary,
        Guid? additional, IEnumerable<ProjectScheduleEntry> entries, DateOnly today)
    {
        var schedules = entries.Where(x => x.Kind == "Schedule").OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).ToArray();
        var current = schedules.Where(x => x.ActorId == primary || x.ActorId == additional)
            .GroupBy(x => x.ActorId).Select(x => x.First()).ToArray();
        var expected = current.Select(x => x.ExpectedWorkEndDate).DefaultIfEmpty().Max();
        var reason = current.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.DelayReason))?.DelayReason;
        if (status == ProjectStatus.Completed) return new("Completed", expected, 0, reason);
        if (target is null) return new("No Target Date", expected, 0, reason);
        if (today > target) return new("Overdue", expected, today.DayNumber - target.Value.DayNumber, reason);
        if (expected is null) return new("Not Scheduled", null, 0, reason);
        if (expected > target) return new("Delay Expected", expected, expected!.Value.DayNumber - target.Value.DayNumber, reason);
        return new("On Track", expected, 0, null);
    }

    public IQueryable<User> Recipients(Project project)
    {
        var users = db.Users.Where(u => u.IsActive && u.ApprovalStatus == ApprovalStatus.Approved &&
            (u.Role == UserRole.Admin || u.Role == UserRole.Manager &&
             (u.Id == project.CreatedByUserId || db.Users.Any(q => q.Id == project.AssignedQcId && q.ManagerId == u.Id))));
        if (tenantOptions.Value.Enabled)
            users = users.Where(u => db.TenantProjectBindings.Any(b => b.ProjectId == project.Id &&
                db.TenantMemberships.Any(m => m.TenantId == b.TenantId && m.UserId == u.Id && m.Status == TenantMembershipStatus.Active)));
        return users;
    }

    public async Task QueueAlert(Project project, ProjectScheduleEntry entry, CancellationToken ct)
    {
        var recipients = await Recipients(project).Select(u => u.Id).ToListAsync(ct);
        db.ProjectDelayEmails.AddRange(recipients.Select(id => new ProjectDelayEmail { ScheduleEntryId = entry.Id, RecipientId = id }));
    }

    public async Task DetectOverdue(CancellationToken ct)
    {
        var today = Today;
        var ids = await db.Projects.AsNoTracking().Where(p => p.TargetDate < today && p.Status != ProjectStatus.Completed &&
            !db.ProjectScheduleEntries.Any(e => e.ProjectId == p.Id && e.Kind == "Overdue" && e.TargetDate == p.TargetDate))
            .Select(p => p.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
            var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p =>
                p.Id == id && p.TargetDate < today && p.Status != ProjectStatus.Completed, ct);
            if (project?.TargetDate is not DateOnly target) continue;
            var key = OverdueKey(target);
            if (await db.ProjectScheduleEntries.AnyAsync(e => e.ProjectId == project.Id && e.EventKey == key, ct)) continue;
            var entries = await db.ProjectScheduleEntries.AsNoTracking().Where(e => e.ProjectId == id && e.Kind == "Schedule").ToListAsync(ct);
            var summary = Summarize(project.Status, target, project.AssignedSurveyorId, project.AdditionalSurveyorId, entries, today);
            var entry = new ProjectScheduleEntry { ProjectId = project.Id, Kind = "Overdue", EventKey = key,
                TargetDate = target, ExpectedWorkEndDate = summary.ExpectedCompletion, DelayReason = summary.LatestDelayReason,
                DelayDays = summary.DelayDays };
            db.ProjectScheduleEntries.Add(entry);
            await QueueAlert(project, entry, ct);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
    }
}
