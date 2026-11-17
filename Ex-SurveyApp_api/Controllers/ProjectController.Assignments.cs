using System.Text.Json;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;
public partial class ProjectController
{
    private static HashSet<(Guid UserId, string Role)> AssignmentMembers(Project p) {
        var members = new HashSet<(Guid, string)> { (p.AssignedSurveyorId, "Primary Surveyor"), (p.AssignedQcId, "Primary QC") };
        if (p.AdditionalSurveyorId is Guid surveyor) members.Add((surveyor, "Additional Surveyor"));
        if (p.AdditionalQcId is Guid qc) members.Add((qc, "Additional QC"));
        if (p.AssignedClientId is Guid client) members.Add((client, "Client"));
        foreach (var assignment in p.ClientAssignments.Where(a => a.IsActive)) members.Add((assignment.ClientUserId, "Client"));
        return members;
    }
    private async Task RecordAssignmentNotices(Project project, HashSet<(Guid UserId, string Role)> previous, CancellationToken ct) {
        var added = AssignmentMembers(project).Except(previous).Where(a => a.UserId != Guid.Empty).GroupBy(a => a.UserId).ToArray();
        if (added.Length == 0) return;
        var ids = added.Select(a => a.Key).Append(CurrentUserId).Distinct().ToArray();
        var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        foreach (var recipient in added) db.AuditLogs.Add(new AuditLog {
            EntityType = "Project", EntityId = project.Id, ActorId = CurrentUserId, Action = ProjectAssignmentNotices.Action,
            Comments = JsonSerializer.Serialize(new AssignmentNoticeData(recipient.Key, names.GetValueOrDefault(recipient.Key, "User"),
                names.GetValueOrDefault(CurrentUserId, "User"), string.Join(", ", recipient.Select(r => r.Role)), project.ProjectName, project.JobNo))
        });
    }
    [HttpGet("assignment-notifications")]
    public async Task<IActionResult> AssignmentNotifications(CancellationToken ct) =>
        Ok(await ProjectAssignmentNotices.Read(db, AccessibleProjects(), CurrentUserId, ct));
}
