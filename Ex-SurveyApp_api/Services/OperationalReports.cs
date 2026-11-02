using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.IO.Compression;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Ex_SurveyApp_api.Services;

public sealed record DailyProgressReport(Guid ProjectId, DateOnly Day, int Planned, int Completed, int CloseCompleted,
    int DetailedCompleted, int Submitted, int QcApproved, int Outstanding, int Inaccessible, int OpenDefects,
    int ClosedDefects, IReadOnlyList<object> Areas, IReadOnlyList<object> Inspectors, IReadOnlyList<object> Delays,
    IReadOnlyList<object> CarryForwardItems, int CumulativePlanned, int CumulativeCompleted, int CumulativeQcApproved, int CarryForward);

public sealed class OperationalReports(SurveyDbContext db, IProjectPhotoStorage? storage = null)
{
    public async Task<DailyProgressReport?> Daily(Guid projectId, DateOnly day, ClaimsPrincipal user, CancellationToken ct)
    {
        var project = await new ProjectReportData(db).AccessibleProjects(user).Where(x => x.Id == projectId).Select(x => new {x.Id}).SingleOrDefaultAsync(ct);
        if (project is null) return null;
        var tasks = await (from task in db.InspectionTasks.AsNoTracking()
            join plan in db.InspectionPlans.AsNoTracking() on task.InspectionPlanId equals plan.Id
            where plan.ProjectId == projectId select task).ToListAsync(ct);
        var taskIds = tasks.Select(x => x.Id).ToArray();
        var activities = await db.InspectionActivities.AsNoTracking().Where(x => taskIds.Contains(x.InspectionTaskId)).ToListAsync(ct);
        // Count one task/equipment once per day even when the equipment has repeated attempts.
        var completedToday = LatestCompletedByTaskForDay(activities,day);
        var submittedToday = activities.Where(x => x.ReviewStatus != InspectionActivityReviewStatus.Draft &&
            DateOnly.FromDateTime(x.UpdatedAt.UtcDateTime) == day).GroupBy(x => x.InspectionTaskId).Select(g => g.OrderByDescending(x => x.UpdatedAt).First()).ToArray();
        var plannedToday = tasks.Where(x => x.ScheduledAt.HasValue && DateOnly.FromDateTime(x.ScheduledAt.Value.UtcDateTime) == day).ToArray();
        var defects = await (from f in db.ActivityFindings.AsNoTracking()
            join a in db.InspectionActivities.AsNoTracking() on f.InspectionActivityId equals a.Id
            join t in db.InspectionTasks.AsNoTracking() on a.InspectionTaskId equals t.Id
            join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id
            where p.ProjectId == projectId select new { f.Id, f.StatusCode }).Distinct().ToListAsync(ct);
        var taskArea = tasks.Where(x => x.AreaEquipmentId.HasValue).ToDictionary(x => x.Id, x => x.AreaEquipmentId);
        var eqIds = taskArea.Values.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var equipment = await db.AreaEquipment.AsNoTracking().Where(x => eqIds.Contains(x.Id)).Select(x => new {x.Id,x.AreaId,x.TagNumber}).ToListAsync(ct);
        var areaIds = equipment.Select(x => x.AreaId).Distinct().ToArray();
        var areas = await db.ProjectAreas.AsNoTracking().Where(x => areaIds.Contains(x.Id)).Select(x => new {x.Id,x.AreaName}).ToListAsync(ct);
        var areaReport = areas.Select(area => (object)new { area = area.AreaName,
            planned = plannedToday.Count(t => taskArea.GetValueOrDefault(t.Id) is Guid e && equipment.Any(q => q.Id == e && q.AreaId == area.Id)),
            completed = completedToday.Count(a => taskArea.GetValueOrDefault(a.InspectionTaskId) is Guid e && equipment.Any(q => q.Id == e && q.AreaId == area.Id)) }).ToArray();
        var inspectorIds = tasks.Where(x => x.AssignedUserId.HasValue).Select(x => x.AssignedUserId!.Value).Distinct().ToArray();
        var inspectors = await db.Users.AsNoTracking().Where(x => inspectorIds.Contains(x.Id)).Select(x => new {x.Id,x.DisplayName}).ToListAsync(ct);
        var inspectorReport = inspectors.Select(i => (object)new { inspector = i.DisplayName,
            planned = plannedToday.Count(t => t.AssignedUserId == i.Id), completed = completedToday.Count(a => a.InspectorUserId == i.Id) }).ToArray();
        var delayed=tasks.Where(t=>t.DueAt.HasValue&&DateOnly.FromDateTime(t.DueAt.Value.UtcDateTime)<day&&t.StatusCode!="Completed"&&!activities.Any(a=>a.InspectionTaskId==t.Id&&a.CompletedAt.HasValue)).ToArray();
        var carryForward=tasks.Where(t=>t.IsInaccessible&&t.CarryForwardReason!=null).ToArray();
        var equipmentNames=equipment.ToDictionary(x=>x.Id,x=>x.TagNumber);
        var delays=delayed.Select(t=>(object)new{taskId=t.Id,equipment=t.AreaEquipmentId.HasValue?equipmentNames.GetValueOrDefault(t.AreaEquipmentId.Value):null,grade=t.PlannedGrade,dueAt=t.DueAt,status=t.StatusCode}).ToArray();
        var carryForwardItems=carryForward.Select(t=>(object)new{taskId=t.Id,equipment=t.AreaEquipmentId.HasValue?equipmentNames.GetValueOrDefault(t.AreaEquipmentId.Value):null,grade=t.PlannedGrade,reason=t.CarryForwardReason,dueAt=t.DueAt}).ToArray();
        var throughDayTasks = tasks.Where(x => !x.ScheduledAt.HasValue || DateOnly.FromDateTime(x.ScheduledAt.Value.UtcDateTime) <= day).ToArray();
        var throughIds = throughDayTasks.Select(x => x.Id).ToArray();
        var throughActivities = activities.Where(x => throughIds.Contains(x.InspectionTaskId)).ToArray();
        var unresolved = defects.Count(x => x.StatusCode is not ("Closed" or "ClientClosed"));
        var closed = defects.Count - unresolved;
        return new(projectId, day, plannedToday.Length, completedToday.Length,
            completedToday.Count(x => x.Grade == InspectionType.Close), completedToday.Count(x => x.Grade == InspectionType.Detailed),
            submittedToday.Length, submittedToday.Count(x => x.ReviewStatus == InspectionActivityReviewStatus.Accepted),
            Math.Max(0, plannedToday.Length - completedToday.Length), plannedToday.Count(x => x.IsInaccessible), unresolved, closed,
            areaReport, inspectorReport, delays, carryForwardItems, throughDayTasks.Length,
            throughDayTasks.Count(t => t.StatusCode == "Completed" || throughActivities.Any(a => a.InspectionTaskId == t.Id && a.CompletedAt.HasValue)),
            throughActivities.Where(a => a.ReviewStatus == InspectionActivityReviewStatus.Accepted).Select(a => a.InspectionTaskId).Distinct().Count(),
            tasks.Count(t => t.IsInaccessible && t.CarryForwardReason != null));
    }

    public async Task<byte[]?> InspectionPdf(Guid activityId, ClaimsPrincipal user, CancellationToken ct,int reportRevision=1,string? reportNumber=null)
    {
        ProjectReportPdf.EnsureFonts();
        var activity = await db.InspectionActivities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == activityId, ct);
        if (activity is null) return null;
        var info = await (from task in db.InspectionTasks.AsNoTracking()
            join plan in db.InspectionPlans.AsNoTracking() on task.InspectionPlanId equals plan.Id
            join equip in db.AreaEquipment.AsNoTracking() on task.AreaEquipmentId equals equip.Id
            join area in db.ProjectAreas.AsNoTracking() on equip.AreaId equals area.Id
            join project in new ProjectReportData(db).AccessibleProjects(user) on plan.ProjectId equals (Guid?)project.Id
            where task.Id == activity.InspectionTaskId
            select new { project.ProjectName, Client = project.ClientName ?? "Unspecified", Site = project.VesselOrSiteName,
                project.JobNo, equip.TagNumber, equip.Description, equip.Location, equip.Manufacturer, equip.Model, equip.SerialNumber,
                equip.ProtectionDetails, equip.CertificateNumber, equip.ExProtectionType, area.AreaName }).SingleOrDefaultAsync(ct);
        if (info is null) return null;
        var checklist = await db.ChecklistVersionSnapshots.AsNoTracking().Where(x => x.Id == activity.ChecklistVersionId).SingleOrDefaultAsync(ct);
        var rev = await db.InspectionActivityRevisions.AsNoTracking().Where(x => x.InspectionActivityId == activityId).OrderByDescending(x => x.RevisionNumber).FirstOrDefaultAsync(ct);
        var sections = await (from d in db.CheckpointDefinitionSnapshots.AsNoTracking()
            join r in db.CheckpointResponseRevisions.AsNoTracking() on d.Id equals r.CheckpointDefinitionSnapshotId
            where d.ChecklistVersionSnapshotId == activity.ChecklistVersionId && rev != null && r.ActivityRevisionId == rev.Id
            orderby d.SectionCode, d.SortOrder select new {d.SectionCode,d.QuestionCode,d.QuestionText,r.ResponseCode,r.Observation,r.NumericValue,r.MeasurementUnitSnapshot}).ToListAsync(ct);
        var findingRows = await db.ActivityFindings.AsNoTracking().Where(x => x.InspectionActivityId == activityId)
            .Select(x => new {x.Id,x.Observation,x.ProposedAction,x.SeverityCode,x.StatusCode}).ToListAsync(ct);
        var review = await db.ActivityReviewEvents.AsNoTracking().Where(x => x.InspectionActivityId == activityId).OrderBy(x => x.DecidedAt).ToListAsync(ct);
        var peopleIds=review.Select(x=>x.ActorUserId).Append(activity.InspectorUserId).Distinct().ToArray();
        var people=await db.Users.AsNoTracking().Where(x=>peopleIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.DisplayName,ct);
        var evidence = await (from e in db.ActivityEvidence.AsNoTracking() join f in db.Files.AsNoTracking() on e.SurveyFileId equals f.Id
            where e.InspectionActivityId == activityId select new { e.Role,e.Caption,f.FileName,f.StorageKey,f.ContentType }).ToListAsync(ct);
        reportNumber??=$"{info.JobNo}-ITR-{activity.Id.ToString("N")[..8].ToUpperInvariant()}";
        using var pdf = new PdfDocument(); pdf.Info.Title = $"Inspection Test Record {reportNumber} Revision {reportRevision}"; pdf.Info.Creator = "Ex-Survey";
        var page = pdf.AddPage(); var gfx = XGraphics.FromPdfPage(page); var font = new XFont("Arial", 9); double y = 36;
        void Line(string text, bool bold = false)
        {
            var f = bold ? new XFont("Arial", 10, XFontStyleEx.Bold) : font;
            var segments=Enumerable.Range(0,Math.Max(1,(text.Length+94)/95)).Select(i=>text.Substring(i*95,Math.Min(95,Math.Max(0,text.Length-i*95))));
            foreach(var segment in segments)
            { if (y > page.Height.Point - 45) { gfx.Dispose(); page = pdf.AddPage(); gfx = XGraphics.FromPdfPage(page); y = 36; }
              gfx.DrawString(segment, f, XBrushes.Black, new XRect(36d,y,page.Width.Point-72d,16d), XStringFormats.TopLeft); y += bold ? 20 : 15; }
        }
        Line("EQUIPMENT INSPECTION TEST RECORD", true); Line($"Report: {reportNumber} · Revision {reportRevision} · Generated {DateTimeOffset.UtcNow:O}");
        Line($"Status: {activity.ReviewStatus} · Activity: {activity.Id:D}"); Line($"Client: {info.Client} · Project: {info.ProjectName} · Site: {info.Site} · Area: {info.AreaName}");
        Line($"Equipment: {info.TagNumber} · {info.Description} · Location: {info.Location}"); Line($"Manufacturer/model/serial: {info.Manufacturer} / {info.Model} / {info.SerialNumber}");
        Line($"Ex marking: {info.ProtectionDetails} · Certificate: {info.CertificateNumber} · Protection: {info.ExProtectionType}");
        Line($"Grade: {activity.Grade} · Started: {activity.StartedAt:O} · Completed: {activity.CompletedAt:O} · Inspector: {people.GetValueOrDefault(activity.InspectorUserId,activity.InspectorNameSnapshot??"Unknown")} · Checklist: {checklist?.VersionNumber} {checklist?.StandardReference} {checklist?.StandardEdition}");
        foreach (var section in sections.GroupBy(x => x.SectionCode ?? "Unsectioned")) { Line(section.Key, true); foreach(var row in section) Line($"{row.QuestionCode} {row.QuestionText} | {row.ResponseCode ?? "Not recorded"} | {row.NumericValue?.ToString() ?? ""} {row.MeasurementUnitSnapshot} | {row.Observation}"); }
        Line("Findings / defects", true); foreach(var f in findingRows) Line($"{f.Id:D} · {f.SeverityCode ?? "Unclassified"} · {f.StatusCode} · {f.Observation} · Action: {f.ProposedAction}");
        Line("Evidence references (authenticated retrieval required)", true); foreach(var e in evidence) Line($"{e.Role} · {e.FileName} · {e.Caption}");
        if (storage is not null)
        foreach (var item in evidence.Where(x => x.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
            var stored = await storage.OpenReadAsync(item.StorageKey, ct); if (stored is null) continue;
            using (stored.Stream)
            try
            {
                using var image = XImage.FromStream(stored.Stream);
                gfx.Dispose(); page = pdf.AddPage(); gfx = XGraphics.FromPdfPage(page); y = 28;
                Line($"Evidence image · {item.Role} · {item.FileName}", true);
                var scale = Math.Min((page.Width.Point - 72) / image.PointWidth, (page.Height.Point - 100) / image.PointHeight);
                gfx.DrawImage(image, (page.Width.Point - image.PointWidth * scale) / 2, y + 8, image.PointWidth * scale, image.PointHeight * scale);
            }
            catch { /* retain the evidence reference even if an unsupported image cannot be embedded */ }
        }
        Line("QC review history", true); foreach(var r in review) Line($"{r.Decision} · {r.DecidedAt:O} · {people.GetValueOrDefault(r.ActorUserId,r.ActorUserId.ToString("D"))} · {r.Comments}");
        gfx.Dispose(); using var ms = new MemoryStream(); pdf.Save(ms, false); return ms.ToArray();
    }

    public static byte[] BuildHandoverZip(string jobNo, DateTimeOffset generatedAt, IReadOnlyDictionary<string, byte[]> files, string completionStatus,string? reportNumber=null,int revision=1)
    {
        using var ms = new MemoryStream(); using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var manifestRows = new List<object>();
            foreach(var (name, bytes) in files.OrderBy(x => x.Key, StringComparer.Ordinal))
            { var entry = zip.CreateEntry(name, CompressionLevel.Optimal); using(var stream=entry.Open()) stream.Write(bytes); manifestRows.Add(new {file=name,sha256=Convert.ToHexString(SHA256.HashData(bytes)),bytes=bytes.Length}); }
            var manifest = JsonSerializer.SerializeToUtf8Bytes(new {package="Ex-Survey campaign handover", jobNo,reportNumber,revision,generatedAt,status=completionStatus,approvalState="Draft",final=false,compliance="Not asserted by this package",files=manifestRows});
            using var manifestStream=zip.CreateEntry("manifest.json",CompressionLevel.Optimal).Open(); manifestStream.Write(manifest);
        }
        return ms.ToArray();
    }

    public static InspectionActivity[] LatestCompletedByTaskForDay(IEnumerable<InspectionActivity> activities,DateOnly day) => activities
        .Where(x=>x.CompletedAt.HasValue&&DateOnly.FromDateTime(x.CompletedAt.Value.UtcDateTime)==day)
        .GroupBy(x=>x.InspectionTaskId).Select(g=>g.OrderByDescending(x=>x.CompletedAt).First()).ToArray();
}
