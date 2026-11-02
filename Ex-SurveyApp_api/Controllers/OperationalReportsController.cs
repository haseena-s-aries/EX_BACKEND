using System.Security.Claims;
using System.Text.Json;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Manager,Qc,Surveyor,Client")]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
[Route("api/reports")]
public sealed class OperationalReportsController(SurveyDbContext db, IProjectPhotoStorage storage, IGeneratedReportArtifactStorage artifactStorage, ILogger<OperationalReportsController> logger) : ControllerBase
{
    private ProjectReportData Data => new(db);
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpGet("projects/{projectId:guid}/inspection-activities")]
    public async Task<IActionResult> InspectionActivities(Guid projectId,CancellationToken ct)
    {
        if(!await Data.AccessibleProjects(User).AnyAsync(x=>x.Id==projectId,ct)) return NotFound();
        var rows=await (from a in db.InspectionActivities.AsNoTracking() join t in db.InspectionTasks.AsNoTracking() on a.InspectionTaskId equals t.Id
            join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id join e in db.AreaEquipment.AsNoTracking() on t.AreaEquipmentId equals e.Id
            where p.ProjectId==projectId orderby a.StartedAt descending select new {a.Id,a.Grade,a.StartedAt,a.CompletedAt,a.ReviewStatus,e.TagNumber,EquipmentId=e.Id}).Take(250).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("activities/{activityId:guid}/inspection-record.pdf")]
    public async Task<IActionResult> InspectionRecord(Guid activityId, CancellationToken ct)
    {
        var activity = await db.InspectionActivities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == activityId, ct);
        if(activity is null)return NotFound();
        var projectId=await (from t in db.InspectionTasks join p in db.InspectionPlans on t.InspectionPlanId equals p.Id where t.Id==activity.InspectionTaskId select p.ProjectId).SingleOrDefaultAsync(ct);
        if(!projectId.HasValue||!await Data.AccessibleProjects(User).AnyAsync(x=>x.Id==projectId.Value,ct))return NotFound();
        var jobNo=await db.Projects.Where(x=>x.Id==projectId.Value).Select(x=>x.JobNo).SingleAsync(ct);
        var reportNumber=$"{Safe(jobNo)}-ITR-{activityId.ToString("N")[..8].ToUpperInvariant()}";
        var revision=await db.GeneratedReportArtifacts.CountAsync(x=>x.ProjectId==projectId.Value&&x.ReportType=="InspectionTestRecord"&&x.LinkedEntityId==activityId,ct)+1;
        var bytes = await new OperationalReports(db, storage).InspectionPdf(activityId, User, ct,revision,reportNumber);
        if (bytes is null) return NotFound();
        var artifact=await Archive(projectId.Value,activityId,"InspectionTestRecord",reportNumber,revision,$"ITR-{activityId.ToString("N")[..8].ToUpperInvariant()}-r{revision}.pdf","application/pdf",bytes,ct);
        await RecordExport("Inspection Test Record Generated", projectId.Value, $"activity={activityId:D}; revision={revision}; artifact={artifact.Id:D}; sha256={artifact.Sha256}", ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(bytes, "application/pdf", artifact.FileName);
    }

    [HttpGet("projects/{projectId:guid}/daily-progress")]
    public async Task<IActionResult> DailyProgress(Guid projectId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var report = await new OperationalReports(db).Daily(projectId, day, User, ct);
        if (report is null) return NotFound();
        await RecordExport("Daily Progress Report Generated", projectId, day.ToString("yyyy-MM-dd"), ct);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(report);
    }

    [HttpGet("projects/{projectId:guid}/defects")]
    public async Task<IActionResult> DefectReport(Guid projectId, [FromQuery] string? status, CancellationToken ct)
    {
        if (!await Data.AccessibleProjects(User).AnyAsync(x => x.Id == projectId, ct)) return NotFound();
        var rows = await (from f in db.ActivityFindings.AsNoTracking()
            join a in db.InspectionActivities.AsNoTracking() on f.InspectionActivityId equals a.Id
            join t in db.InspectionTasks.AsNoTracking() on a.InspectionTaskId equals t.Id
            join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id
            join e in db.AreaEquipment.AsNoTracking() on t.AreaEquipmentId equals e.Id
            join area in db.ProjectAreas.AsNoTracking() on e.AreaId equals area.Id
            where p.ProjectId == projectId
            select new { f.Id, f.FaultCode, f.Observation, f.ProposedAction, f.SeverityCode, f.StatusCode, f.ReportedAt, f.DueAt,
                ActivityId=a.Id, Grade=a.Grade, EquipmentId=e.Id, EquipmentTag=e.TagNumber, Area=area.AreaName }).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(status)) rows = rows.Where(x => x.StatusCode.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var ids = rows.Select(x => x.Id).ToArray();
        var actions = await db.CorrectiveActions.AsNoTracking().Where(x => ids.Contains(x.ActivityFindingId))
            .Select(x => new {x.Id,x.ActivityFindingId,x.ActionReference,x.ExternalWorkOrderReference,x.RectificationDescription,x.ResponsibleParty,x.Status,x.RectificationAt,x.AriesVerifiedAt,x.ClientDecisionAt,x.LastDecisionComment}).ToListAsync(ct);
        var history = await db.CorrectiveActionEvents.AsNoTracking().Where(x => actions.Select(a => a.Id).Contains(x.CorrectiveActionId)).OrderBy(x => x.OccurredAt)
            .Select(x => new {x.CorrectiveActionId,x.Kind,x.ResultingStatus,x.ActorUserId,x.Comment,x.OccurredAt}).ToListAsync(ct);
        var activityIds=rows.Select(x=>x.ActivityId).Distinct().ToArray();
        var actionEvents=await db.CorrectiveActionEvents.AsNoTracking().Where(x=>actions.Select(a=>a.Id).Contains(x.CorrectiveActionId)).Select(x=>new{x.Id,x.CorrectiveActionId}).ToListAsync(ct);
        var actionEventIds=actionEvents.Select(x=>x.Id).ToArray();
        var evidence=await (from e in db.ActivityEvidence.AsNoTracking() join file in db.Files.AsNoTracking() on e.SurveyFileId equals file.Id
            where (e.InspectionActivityId.HasValue&&activityIds.Contains(e.InspectionActivityId.Value))||(e.CorrectiveActionEventId.HasValue&&actionEventIds.Contains(e.CorrectiveActionEventId.Value))
            select new {e.Id,e.InspectionActivityId,e.CorrectiveActionEventId,e.Role,e.Caption,e.AddedAt,file.FileName,file.ContentType}).ToListAsync(ct);
        var result = rows.Select(x => new {x.Id,x.FaultCode,x.Observation,x.ProposedAction,x.SeverityCode,
            StatusCode=actions.Where(a=>a.ActivityFindingId==x.Id).Any()?actions.Where(a=>a.ActivityFindingId==x.Id).All(a=>a.Status==CorrectiveActionStatus.ClientClosed)?"ClientClosed":actions.Any(a=>a.ActivityFindingId==x.Id&&a.Status==CorrectiveActionStatus.SubmittedForVerification)?"AwaitingAriesVerification":actions.Any(a=>a.ActivityFindingId==x.Id&&a.Status==CorrectiveActionStatus.Verified)?"AwaitingClientClosure":x.StatusCode:x.StatusCode,
            x.ReportedAt,x.DueAt,x.ActivityId,x.Grade,x.EquipmentId,x.EquipmentTag,x.Area,
            AgeDays=Math.Max(0,(int)(DateTimeOffset.UtcNow-x.ReportedAt).TotalDays),
            Evidence=evidence.Where(e=>e.InspectionActivityId==x.ActivityId),
            CorrectiveActions=actions.Where(a=>a.ActivityFindingId==x.Id).Select(a=>new {a.Id,a.ActionReference,a.ExternalWorkOrderReference,a.RectificationDescription,a.ResponsibleParty,a.Status,a.RectificationAt,a.AriesVerifiedAt,a.ClientDecisionAt,a.LastDecisionComment,History=history.Where(h=>h.CorrectiveActionId==a.Id),Evidence=evidence.Where(e=>e.CorrectiveActionEventId.HasValue&&actionEvents.Any(ev=>ev.Id==e.CorrectiveActionEventId&&ev.CorrectiveActionId==a.Id))})});
        var defectItems=result.ToArray();
        await RecordExport("Defect Report Generated", projectId, status ?? "all", ct);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new {ProjectId=projectId,GeneratedAt=DateTimeOffset.UtcNow,Summary=new{Total=defectItems.Length,Open=defectItems.Count(x=>x.StatusCode is not ("Closed" or "ClientClosed")),Overdue=defectItems.Count(x=>x.DueAt.HasValue&&x.DueAt.Value<DateTimeOffset.UtcNow&&x.StatusCode is not ("Closed" or "ClientClosed")),AwaitingAriesVerification=defectItems.Sum(x=>x.CorrectiveActions.Count(a=>a.Status==CorrectiveActionStatus.SubmittedForVerification)),AwaitingClientClosure=defectItems.Sum(x=>x.CorrectiveActions.Count(a=>a.Status==CorrectiveActionStatus.Verified)),ClientClosed=defectItems.Sum(x=>x.CorrectiveActions.Count(a=>a.Status==CorrectiveActionStatus.ClientClosed))},Items=defectItems});
    }

    [HttpGet("projects/{projectId:guid}/handover.zip")]
    public async Task<IActionResult> Handover(Guid projectId, CancellationToken ct)
    {
        var project = await Data.AccessibleProjects(User).SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null) return NotFound();
        var now=DateTimeOffset.UtcNow; var files=new Dictionary<string,byte[]>();
        var aggregate=await Data.Load(projectId,User,ct);
        if(aggregate is not null) files["reports/project-inspection-report.pdf"]=await new ProjectReportPdf(storage,logger).Generate(aggregate,ct);
        var haer=await Data.LoadHaer(projectId,User,ct);
        if(haer is not null) files["register/haer.csv"]=HaerCsv(haer);
        var defects=await DefectPayload(projectId,ct); files["defects/defect-register.json"]=JsonSerializer.SerializeToUtf8Bytes(defects);
        var tasks=await (from t in db.InspectionTasks.AsNoTracking() join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id where p.ProjectId==projectId select t).ToListAsync(ct);
        var taskIds=tasks.Select(x=>x.Id).ToArray();
        var activities=await db.InspectionActivities.AsNoTracking().Where(x=>taskIds.Contains(x.InspectionTaskId)).OrderBy(x=>x.StartedAt).ToListAsync(ct);
        var campaign=await db.InspectionCampaigns.AsNoTracking().Where(x=>x.ProjectId==projectId).OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync(ct);
        if(campaign is not null)
        {
            var annual=await db.CampaignAnnualLists.AsNoTracking().Where(x=>x.CampaignId==campaign.Id).ToListAsync(ct);
            files["campaign/summary.json"]=JsonSerializer.SerializeToUtf8Bytes(new {campaign.Id,campaign.Name,campaign.Reference,campaign.Status,campaign.Revision,AnnualLists=annual,
                Planned=tasks.Count,Completed=tasks.Count(t=>t.StatusCode=="Completed"||activities.Any(a=>a.InspectionTaskId==t.Id&&a.CompletedAt.HasValue)),QcApproved=activities.Where(a=>a.ReviewStatus==InspectionActivityReviewStatus.Accepted).Select(a=>a.InspectionTaskId).Distinct().Count(),
                OutstandingTasks=tasks.Where(t=>t.StatusCode!="Completed"&&!activities.Any(a=>a.InspectionTaskId==t.Id&&a.CompletedAt.HasValue)).Select(t=>new{t.Id,t.AreaEquipmentId,t.PlannedGrade,t.DueAt,t.IsInaccessible,t.CarryForwardReason})});
        }
        files["campaign/qc-summary.json"]=JsonSerializer.SerializeToUtf8Bytes(new {Activities=activities.Count,Submitted=activities.Count(x=>x.ReviewStatus!=InspectionActivityReviewStatus.Draft),Accepted=activities.Count(x=>x.ReviewStatus==InspectionActivityReviewStatus.Accepted),Returned=activities.Count(x=>x.ReviewStatus==InspectionActivityReviewStatus.ChangesRequested),InReview=activities.Count(x=>x.ReviewStatus==InspectionActivityReviewStatus.InReview),Drafts=activities.Count(x=>x.ReviewStatus==InspectionActivityReviewStatus.Draft),Compliant=(int?)null});
        foreach(var item in activities)
        {
            var pdf=await new OperationalReports(db,storage).InspectionPdf(item.Id,User,ct);
            if(pdf is not null) files[$"inspection-records/ITR-{item.Id.ToString("N")[..8].ToUpperInvariant()}.pdf"]=pdf;
        }
        files["campaign/checklist-versions.json"]=JsonSerializer.SerializeToUtf8Bytes(await db.ChecklistVersionSnapshots.AsNoTracking().Where(x=>activities.Select(a=>a.ChecklistVersionId).Contains(x.Id)).Select(x=>new{x.Id,x.VersionNumber,x.StandardReference,x.StandardEdition,x.SnapshotHash}).Distinct().ToListAsync(ct));
        var todayProgress=await new OperationalReports(db).Daily(projectId,DateOnly.FromDateTime(now.UtcDateTime),User,ct);
        files["campaign/daily-progress.json"]=JsonSerializer.SerializeToUtf8Bytes(todayProgress);
        var outstanding=tasks.Any(t=>t.StatusCode!="Completed"&&!activities.Any(a=>a.InspectionTaskId==t.Id&&a.CompletedAt.HasValue));
        var openDefects=await (from f in db.ActivityFindings.AsNoTracking() join a in db.InspectionActivities.AsNoTracking() on f.InspectionActivityId equals a.Id
            join t in db.InspectionTasks.AsNoTracking() on a.InspectionTaskId equals t.Id join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id
            where p.ProjectId==projectId&&f.StatusCode!="Closed"&&f.StatusCode!="ClientClosed" select f.Id).AnyAsync(ct);
        var status=outstanding||openDefects?"Prepared with outstanding items":"Prepared for review";
        var linkedEntityId=campaign?.Id??projectId;
        var revision=await db.GeneratedReportArtifacts.CountAsync(x=>x.ProjectId==projectId&&x.ReportType=="CampaignHandover"&&x.LinkedEntityId==linkedEntityId,ct)+1;
        var reportNumber=$"{Safe(project.JobNo)}-HANDOVER-{linkedEntityId.ToString("N")[..8].ToUpperInvariant()}";
        files["campaign/handover-summary.json"]=JsonSerializer.SerializeToUtf8Bytes(new {project.ProjectName,project.JobNo,project.VesselOrSiteName,generatedAt=now,status,reportNumber,revision,approvalState="Draft",final=false,compliant=false,
            note="QC acceptance is not a compliance determination. Client closure and campaign handover review remain separate decisions."});
        var zip=OperationalReports.BuildHandoverZip(project.JobNo,now,files,status,reportNumber,revision);
        var artifact=await Archive(projectId,linkedEntityId,"CampaignHandover",reportNumber,revision,$"{Safe(project.JobNo)}_handover_r{revision}_{now:yyyyMMdd_HHmmss}.zip","application/zip",zip,ct);
        await RecordExport("Campaign Handover Generated",projectId,$"campaign={campaign?.Id}; revision={revision}; artifact={artifact.Id:D}; sha256={artifact.Sha256}; {status}; files={files.Count}",ct);
        Response.Headers.CacheControl="private, no-store";
        return File(zip,"application/zip",artifact.FileName);
    }

    [HttpGet("projects/{projectId:guid}/history")]
    public async Task<IActionResult> ExportHistory(Guid projectId,CancellationToken ct)
    {
        if(!await Data.AccessibleProjects(User).AnyAsync(x=>x.Id==projectId,ct)) return NotFound();
        var artifacts=await db.GeneratedReportArtifacts.AsNoTracking().Where(x=>x.ProjectId==projectId).OrderByDescending(x=>x.GeneratedAt).Take(100)
            .Select(x=>new{x.Id,x.ReportType,x.ReportNumber,x.Revision,x.FileName,x.ContentType,x.Sha256,x.GeneratedAt,x.GeneratedByUserId,x.ApprovalState,DownloadUrl=$"/api/reports/projects/{projectId:D}/artifacts/{x.Id:D}"}).ToListAsync(ct);
        var events=await db.AuditLogs.AsNoTracking().Where(x=>x.EntityId==projectId&&x.EntityType=="Project"&&(x.Action.Contains("Report")||x.Action.Contains("Handover")||x.Action.Contains("Inspection Test Record")))
            .OrderByDescending(x=>x.OccurredAt).Select(x=>new{x.Id,x.Action,x.Comments,x.ActorId,x.OccurredAt}).Take(100).ToListAsync(ct);
        return Ok(new{Artifacts=artifacts,Events=events});
    }

    [HttpGet("projects/{projectId:guid}/artifacts/{artifactId:guid}")]
    public async Task<IActionResult> DownloadArtifact(Guid projectId,Guid artifactId,CancellationToken ct)
    {
        if(!await Data.AccessibleProjects(User).AnyAsync(x=>x.Id==projectId,ct))return NotFound();
        var artifact=await db.GeneratedReportArtifacts.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==artifactId&&x.ProjectId==projectId,ct);
        if(artifact is null)return NotFound();
        var file=await artifactStorage.OpenReadAsync(artifact.StorageKey,ct);if(file is null)return NotFound();
        var verifiedBytes=new MemoryStream();await file.Stream.CopyToAsync(verifiedBytes,ct);await file.Stream.DisposeAsync();
        var actualHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(verifiedBytes.ToArray()));
        if(!string.Equals(actualHash,artifact.Sha256,StringComparison.OrdinalIgnoreCase))return Problem("The archived report failed its integrity check.",statusCode:500);
        verifiedBytes.Position=0;
        Response.Headers.CacheControl="private, no-store";Response.Headers["X-Content-Type-Options"]="nosniff";
        await RecordExport("Archived Report Downloaded",projectId,$"artifact={artifact.Id:D}; sha256={artifact.Sha256}",ct);
        return File(verifiedBytes,artifact.ContentType,artifact.FileName,enableRangeProcessing:true);
    }

    private async Task<object> DefectPayload(Guid projectId,CancellationToken ct)
    {
        var findings=await (from f in db.ActivityFindings.AsNoTracking() join a in db.InspectionActivities.AsNoTracking() on f.InspectionActivityId equals a.Id
            join t in db.InspectionTasks.AsNoTracking() on a.InspectionTaskId equals t.Id join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id
            where p.ProjectId==projectId select new {f.Id,f.FaultCode,f.Observation,f.ProposedAction,f.SeverityCode,f.StatusCode,f.ReportedAt,f.DueAt,ActivityId=a.Id,t.AreaEquipmentId}).ToListAsync(ct);
        var ids=findings.Select(x=>x.Id).ToArray();
        var actions=await db.CorrectiveActions.AsNoTracking().Where(x=>ids.Contains(x.ActivityFindingId)).Select(x=>new{x.Id,x.ActivityFindingId,x.ActionReference,x.ExternalWorkOrderReference,x.RectificationDescription,x.ResponsibleParty,x.Status,x.RectificationAt,x.AriesVerifiedAt,x.ClientDecisionAt,x.LastDecisionComment}).ToListAsync(ct);
        var actionIdValues=actions.Select(x=>x.Id).ToArray();
        var history=await db.CorrectiveActionEvents.AsNoTracking().Where(x=>actionIdValues.Contains(x.CorrectiveActionId)).OrderBy(x=>x.OccurredAt).Select(x=>new{x.Id,x.CorrectiveActionId,x.Kind,x.ResultingStatus,x.ActorUserId,x.Comment,x.WorkOrderReference,x.OccurredAt}).ToListAsync(ct);
        var activityIds=findings.Select(x=>x.ActivityId).Distinct().ToArray();var eventIds=await db.CorrectiveActionEvents.AsNoTracking().Where(x=>actionIdValues.Contains(x.CorrectiveActionId)).Select(x=>x.Id).ToArrayAsync(ct);
        var evidence=await (from e in db.ActivityEvidence.AsNoTracking() join file in db.Files.AsNoTracking() on e.SurveyFileId equals file.Id
            where (e.InspectionActivityId.HasValue&&activityIds.Contains(e.InspectionActivityId.Value))||(e.CorrectiveActionEventId.HasValue&&eventIds.Contains(e.CorrectiveActionEventId.Value))
            select new{e.Id,e.InspectionActivityId,e.CorrectiveActionEventId,e.Role,e.Caption,e.AddedAt,file.FileName,file.ContentType}).ToListAsync(ct);
        return new {projectId,generatedAt=DateTimeOffset.UtcNow,items=findings.Select(x=>new{x.Id,x.FaultCode,x.Observation,x.ProposedAction,x.SeverityCode,x.StatusCode,x.ReportedAt,x.DueAt,x.ActivityId,x.AreaEquipmentId,Evidence=evidence.Where(e=>e.InspectionActivityId==x.ActivityId),CorrectiveActions=actions.Where(a=>a.ActivityFindingId==x.Id).Select(a=>new{a.Id,a.ActivityFindingId,a.ActionReference,a.ExternalWorkOrderReference,a.RectificationDescription,a.ResponsibleParty,a.Status,a.RectificationAt,a.AriesVerifiedAt,a.ClientDecisionAt,a.LastDecisionComment,History=history.Where(h=>h.CorrectiveActionId==a.Id),Evidence=evidence.Where(e=>e.CorrectiveActionEventId.HasValue&&history.Any(h=>h.Id==e.CorrectiveActionEventId&&h.CorrectiveActionId==a.Id))})})};
    }
    private async Task RecordExport(string action,Guid entityId,string? comments,CancellationToken ct)
    { if(Actor==Guid.Empty)return; db.AuditLogs.Add(new AuditLog{EntityType="Project",EntityId=entityId,ActorId=Actor,Action=action,Comments=comments});await db.SaveChangesAsync(ct); }
    private async Task<GeneratedReportArtifact> Archive(Guid projectId,Guid linkedEntityId,string reportType,string reportNumber,int revision,string fileName,string contentType,byte[] content,CancellationToken ct)
    {
        var id=Guid.NewGuid();var stored=await artifactStorage.SaveAsync(projectId,id,fileName,content,ct);
        var artifact=new GeneratedReportArtifact{Id=id,ProjectId=projectId,LinkedEntityId=linkedEntityId,ReportType=reportType,ReportNumber=reportNumber,Revision=revision,FileName=fileName,ContentType=contentType,StorageKey=stored.StorageKey,Sha256=stored.Sha256,GeneratedAt=DateTimeOffset.UtcNow,GeneratedByUserId=Actor,ApprovalState="Draft"};
        db.GeneratedReportArtifacts.Add(artifact);await db.SaveChangesAsync(ct);return artifact;
    }
    private static string Safe(string value)=>string.Concat(value.Where(c=>char.IsLetterOrDigit(c)||c is '-' or '_')) is {Length:>0} s?s:"project";
    private static byte[] HaerCsv(HaerReport report)
    { var lines=new List<string>{"Equipment ID,Tag,Area,Description,Type,Location,Manufacturer,Model,Serial,Ex Protection,Certificate,Latest Grade,Latest Date,Latest Result,Open Defects,Reconciliation"};
      foreach(var x in report.Equipment)lines.Add(string.Join(",",new[]{x.EquipmentId,x.TagNumber,x.Area,x.Description,x.EquipmentType,x.Location,x.Manufacturer,x.Model,x.SerialNumber,x.ExProtectionType,x.CertificateNumber,x.LastInspectionGrade,x.LastInspectionAt?.ToString("O"),x.LastInspectionResult,x.OutstandingDefects.ToString(),x.ReconciliationStatus}.Select(v=>"\""+(v??"").Replace("\"","\"\"")+"\"")));
      return System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n",lines))).ToArray(); }
}
