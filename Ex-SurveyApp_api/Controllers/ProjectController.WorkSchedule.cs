using System.ComponentModel.DataAnnotations;
using System.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

public partial class ProjectController
{
    private void RecordTargetDate(Project project, DateOnly? previous) =>
        db.ProjectScheduleEntries.Add(new ProjectScheduleEntry { ProjectId = project.Id, ActorId = CurrentUserId,
            Kind = "Target", EventKey = $"target/{Guid.NewGuid():N}", TargetDate = project.TargetDate, PreviousTargetDate = previous });

    private IQueryable<Project> ScheduleProjects() => User.IsInRole(nameof(UserRole.Surveyor)) || User.IsInRole(nameof(UserRole.Qc)) ? AccessibleProjects() : ManageSurveyors();

    [HttpGet("{id:guid}/work-schedule")]
    [Authorize(Roles = "Admin,Manager,Surveyor,Qc")]
    public async Task<IActionResult> WorkSchedule(Guid id, CancellationToken ct)
    {
        var project = await ScheduleProjects().AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return NotFound();
        var entries = await db.ProjectScheduleEntries.AsNoTracking().Where(e => e.ProjectId == id)
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).ToListAsync(ct);
        var actors = entries.Select(e => e.ActorId).OfType<Guid>().Distinct().ToArray();
        var names = await db.Users.Where(u => actors.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var mine = entries.FirstOrDefault(x => x.Kind == "Schedule" && x.ActorId == CurrentUserId);
        var today = ProjectScheduling.Today;
        var summary = ProjectScheduling.Summarize(project.Status, project.TargetDate, project.AssignedSurveyorId,
            project.AdditionalSurveyorId, entries, today);
        var mail = await (from m in db.ProjectDelayEmails.AsNoTracking() join e in db.ProjectScheduleEntries on m.ScheduleEntryId equals e.Id
                          where e.ProjectId == id select new { m.ScheduleEntryId, m.Status }).ToListAsync(ct);
        var mailByEntry = mail.ToLookup(x => x.ScheduleEntryId);
        return Ok(new
        {
            project.TargetDate, summary, today, mine,
            reasonRequired = summary.Status == "Overdue" && !entries.Any(x => x.ActorId == CurrentUserId &&
                x.Kind == "Schedule" && x.TargetDate == project.TargetDate && !string.IsNullOrWhiteSpace(x.DelayReason)),
            history = entries.Select(e => new { entry = e, submittedBy = e.ActorId is Guid actor ? names.GetValueOrDefault(actor) : null,
                emailStatuses = mailByEntry[e.Id].Select(m => m.Status) })
        });
    }

    [HttpPut("{id:guid}/work-schedule")]
    [Authorize(Roles = "Surveyor")]
    public async Task<IActionResult> SaveWorkSchedule(Guid id, WorkScheduleRequest request,
        [FromServices] ProjectScheduling scheduling, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.WorkStartDate == default || request.ExpectedWorkEndDate < request.WorkStartDate)
            return BadRequest(new { error = "Select a start date and an expected end date on or after the start date." });
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var project = await ScheduleProjects().AsTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return NotFound();
        var key = $"schedule/{CurrentUserId:N}/{request.RequestId:N}";
        var reason = string.IsNullOrWhiteSpace(request.DelayReason) ? null : request.DelayReason.Trim();
        var priorRequest = await db.ProjectScheduleEntries.AsNoTracking().SingleOrDefaultAsync(e => e.ProjectId == id && e.EventKey == key, ct);
        if (priorRequest is not null)
            return priorRequest.WorkStartDate == request.WorkStartDate && priorRequest.ExpectedWorkEndDate == request.ExpectedWorkEndDate &&
                priorRequest.DelayReason == reason ? NoContent() : Conflict(new { error = "This request ID already saved a different schedule." });
        if (project.Status == ProjectStatus.Completed || project.TargetDate is not DateOnly target)
            return Conflict(new { error = "An active project with a Target Date is required." });

        var previous = await db.ProjectScheduleEntries.AsNoTracking().Where(e => e.ProjectId == id &&
            e.ActorId == CurrentUserId && e.Kind == "Schedule").OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        var today = ProjectScheduling.Today;
        var forecastDelay = request.ExpectedWorkEndDate > target;
        if (reason is null && (forecastDelay || today > target && !await db.ProjectScheduleEntries.AnyAsync(e =>
                e.ProjectId == id && e.ActorId == CurrentUserId && e.Kind == "Schedule" && e.TargetDate == target && e.DelayReason != null, ct)))
            return BadRequest(new { error = "Delay Reason is required." });
        if (previous is not null && previous.WorkStartDate == request.WorkStartDate &&
            previous.ExpectedWorkEndDate == request.ExpectedWorkEndDate && previous.DelayReason == reason && previous.TargetDate == target)
            return NoContent();

        var entry = new ProjectScheduleEntry { ProjectId = id, ActorId = CurrentUserId, Kind = "Schedule", EventKey = key,
            TargetDate = target, WorkStartDate = request.WorkStartDate, ExpectedWorkEndDate = request.ExpectedWorkEndDate,
            DelayReason = reason, DelayDays = Math.Max(0,
                (forecastDelay ? request.ExpectedWorkEndDate.DayNumber : today.DayNumber) - target.DayNumber) };
        db.ProjectScheduleEntries.Add(entry);
        project.UpdatedAt = DateTimeOffset.UtcNow;
        var changedDelay = previous is null || previous.ExpectedWorkEndDate != entry.ExpectedWorkEndDate ||
            previous.TargetDate != entry.TargetDate || previous.DelayReason != entry.DelayReason;
        if (reason is not null && entry.DelayDays > 0 && changedDelay) await scheduling.QueueAlert(project, entry, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { error = "A matching schedule request was saved. Refresh before trying again." });
        }
        return await WorkSchedule(id, ct);
    }

    [HttpGet("delay-alerts")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DelayAlerts(CancellationToken ct)
    {
        var alerts = await (from mail in db.ProjectDelayEmails.AsNoTracking()
                            join entry in db.ProjectScheduleEntries on mail.ScheduleEntryId equals entry.Id
                            join project in ManageSurveyors().AsNoTracking() on entry.ProjectId equals project.Id
                            where mail.RecipientId == CurrentUserId
                            orderby entry.OccurredAt descending
                            select new { entry.Id, entry.ProjectId, project.ProjectName, project.JobNo, entry.Kind,
                                entry.TargetDate, entry.ExpectedWorkEndDate, entry.DelayDays, entry.DelayReason, entry.OccurredAt,
                                surveyorName = db.Users.Where(u => u.Id == entry.ActorId).Select(u => u.DisplayName).FirstOrDefault(),
                                emailStatus = mail.Status }).Take(100).ToListAsync(ct);
        return Ok(alerts);
    }

    public sealed record WorkScheduleRequest(Guid RequestId, DateOnly WorkStartDate, DateOnly ExpectedWorkEndDate,
        [StringLength(2000)] string? DelayReason);
}
