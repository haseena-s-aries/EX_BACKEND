using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public sealed class ProjectDelayMonitor(IServiceScopeFactory scopes, ILogger<ProjectDelayMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var scheduling = scope.ServiceProvider.GetRequiredService<ProjectScheduling>();
                await scheduling.DetectOverdue(stoppingToken);
                await Deliver(scope.ServiceProvider, scheduling, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Project delay monitoring iteration failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task Deliver(IServiceProvider services, ProjectScheduling scheduling, CancellationToken ct)
    {
        var db = services.GetRequiredService<SurveyDbContext>();
        if (!db.Database.IsRelational()) return;
        var email = services.GetRequiredService<IEmailService>();
        var pending = await db.ProjectDelayEmails.AsNoTracking().Where(m => m.Status == "Pending")
            .OrderBy(m => m.Id).Take(100).ToListAsync(ct);
        foreach (var mail in pending)
        {
            var entry = await db.ProjectScheduleEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == mail.ScheduleEntryId, ct);
            if (entry is null) continue;
            var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == entry.ProjectId, ct);
            if (project is null) continue;
            var recipient = await scheduling.Recipients(project).AsNoTracking().SingleOrDefaultAsync(x => x.Id == mail.RecipientId, ct);
            var obsolete = entry.Kind == "Overdue" && (project.Status == ProjectStatus.Completed || project.TargetDate != entry.TargetDate);
            if (recipient is null || obsolete)
            {
                await db.ProjectDelayEmails.Where(x => x.Id == mail.Id && x.Status == "Pending")
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "Cancelled"), ct);
                continue;
            }
            var now = DateTimeOffset.UtcNow;
            var claimed = await db.ProjectDelayEmails.Where(x => x.Id == mail.Id && x.Status == "Pending")
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "Sending").SetProperty(x => x.AttemptedAt, now), ct);
            if (claimed == 0) continue;

            var accepted = false;
            logger.LogInformation("Attempting project delay email {EmailId} for project {ProjectId}.", mail.Id, project.Id);
            try
            {
                var surveyor = await db.Users.Where(u => u.Id == entry.ActorId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
                var body = $"Project: {project.ProjectName}\nJob Number: {project.JobNo}\n" +
                    $"Status: {(entry.Kind == "Overdue" ? "Overdue" : "Delay reported")}\nSurveyor: {surveyor ?? "—"}\n" +
                    $"Target Date: {entry.TargetDate:yyyy-MM-dd}\nExpected Work End Date: {entry.ExpectedWorkEndDate:yyyy-MM-dd}\n" +
                    $"Calendar days delayed: {entry.DelayDays}\nReason: {entry.DelayReason ?? "Awaiting Surveyor reason"}\n" +
                    $"Recorded: {entry.OccurredAt:yyyy-MM-dd HH:mm} UTC";
                await email.SendProjectDelayAsync(recipient.Email, recipient.DisplayName,
                    $"Project Delay Alert – {project.ProjectName} – {project.JobNo}", body, ct);
                accepted = true;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Project delay email {EmailId} was not confirmed by SMTP.", mail.Id);
            }
            if (!ct.IsCancellationRequested || accepted)
                await db.ProjectDelayEmails.Where(x => x.Id == mail.Id)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, accepted ? "Accepted" : "Failed")
                        .SetProperty(x => x.AcceptedAt, accepted ? DateTimeOffset.UtcNow : (DateTimeOffset?)null), CancellationToken.None);
        }
    }
}
