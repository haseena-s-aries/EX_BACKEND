using System.Text.Json;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;
namespace Ex_SurveyApp_api.Services;
public sealed record AssignmentNoticeData(Guid RecipientId, string AssignedTo, string AssignedBy, string AssignmentRole, string ProjectName, string JobNo);
public sealed record ProjectAssignmentNotice(Guid Id, Guid ProjectId, Guid? AssignedById, string AssignedBy, string AssignedTo, string AssignmentRole, string ProjectName, string JobNo, DateTimeOffset Time) {
    public string Title => "New Project Assigned";
    public string Message => $"Project: {ProjectName}\nJob Number: {JobNo}\nAssigned By: {AssignedBy}\nAssigned To: {AssignedTo} ({AssignmentRole})\nAssigned Date: {Time:dd MMM yyyy}";
}
public static class ProjectAssignmentNotices {
    public const string Action = "Project Assignment Notice";
    public static async Task<IReadOnlyList<ProjectAssignmentNotice>> Read(SurveyDbContext db, IQueryable<Project> accessible, Guid recipient, CancellationToken ct) {
        var events = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "Project" && a.Action == Action && accessible.Any(p => p.Id == a.EntityId))
            .OrderByDescending(a => a.OccurredAt).ToListAsync(ct);
        var notices = new List<ProjectAssignmentNotice>();
        foreach (var e in events) {
            AssignmentNoticeData? data;
            try { data = JsonSerializer.Deserialize<AssignmentNoticeData>(e.Comments ?? "null"); } catch (JsonException) { continue; }
            if (data?.RecipientId != recipient) continue;
            notices.Add(new(e.Id, e.EntityId, e.ActorId, data.AssignedBy, data.AssignedTo, data.AssignmentRole, data.ProjectName, data.JobNo, e.OccurredAt));
        }
        return notices.Take(100).ToArray();
    }
}
