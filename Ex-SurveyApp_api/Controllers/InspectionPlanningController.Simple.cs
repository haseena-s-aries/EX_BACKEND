using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ex_SurveyApp_api.Controllers;

public sealed partial class InspectionPlanningController
{
    [HttpGet("projects/{projectId:guid}/overview")]
    [Authorize(Roles="Admin,Manager,Surveyor,Qc")]
    public async Task<IActionResult> Overview(Guid projectId,CancellationToken ct)
    {
        var data=new ProjectReportData(db);
        var project=await data.AccessibleProjects(User).SingleOrDefaultAsync(p=>p.Id==projectId,ct);
        if(project is null)return NotFound();
        var surveyor=User.IsInRole("Surveyor");
        var tasks=await (from t in db.InspectionTasks.AsNoTracking() join p in db.InspectionPlans on t.InspectionPlanId equals p.Id
            where p.ProjectId==projectId && (!surveyor||t.AssignedUserId==Actor) && t.StatusCode!="Cancelled"
            select t).ToListAsync(ct);
        var ids=tasks.Select(t=>t.Id).ToArray();
        var activities=await db.InspectionActivities.AsNoTracking().Where(a=>ids.Contains(a.InspectionTaskId)).OrderByDescending(a=>a.StartedAt).ToListAsync(ct);
        var assignedIds=tasks.Where(t=>t.AssignedUserId.HasValue).Select(t=>t.AssignedUserId!.Value).ToArray();
        var names=await db.Users.AsNoTracking().Where(u=>assignedIds.Contains(u.Id)).ToDictionaryAsync(u=>u.Id,u=>u.DisplayName,ct);
        var haer=await data.LoadHaer(projectId,User,ct);
        var areas=await db.ProjectAreas.AsNoTracking().Where(a=>a.ProjectId==projectId).Select(a=>new{a.Id,a.AreaName,PhotoId=db.ProjectAreaPhotos.Where(p=>p.ProjectAreaId==a.Id).OrderBy(p=>p.UploadedAt).Select(p=>(Guid?)p.Id).FirstOrDefault()}).ToArrayAsync(ct);
        var team=await db.Users.AsNoTracking().Where(u=>(u.Id==project.AssignedSurveyorId||u.Id==project.AdditionalSurveyorId)&&u.IsActive&&u.ApprovalStatus==ApprovalStatus.Approved).Select(u=>new{u.Id,u.DisplayName}).ToArrayAsync(ct);
        return Ok(new{Project= new{project.Id,project.ProjectName,project.TargetDate},Areas=areas,Equipment=haer!.Equipment,Surveyors=team,
            Tasks=tasks.Select(t=>{var a=activities.FirstOrDefault(a=>a.InspectionTaskId==t.Id);return new{t.Id,t.AreaEquipmentId,t.AssignedUserId,t.PlannedGrade,t.ScheduledAt,t.DueAt,t.CarryForwardReason,SurveyorName=names.GetValueOrDefault(t.AssignedUserId??Guid.Empty),InspectorName=a?.InspectorNameSnapshot,
                Status=a?.ReviewStatus==InspectionActivityReviewStatus.ChangesRequested?"Changes Requested":a?.CompletedAt!=null||t.StatusCode=="Completed"?"Completed":a!=null?"In Progress":"Pending",
                ReviewStatus=a?.ReviewStatus.ToString(),Delayed=t.DueAt<DateTimeOffset.UtcNow&&a?.CompletedAt==null&&t.StatusCode!="Completed",ActivityId=a?.Id};})});
    }
    [HttpPost("projects/{projectId:guid}/simple-plan")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> SimplePlan(Guid projectId,SimplePlanRequest r,CancellationToken ct)
    {
        if(!CanPlan)return Forbid();
        var project=await new ProjectReportData(db).AccessibleProjects(User).SingleOrDefaultAsync(p=>p.Id==projectId,ct);if(project is null)return NotFound();
        if(r.SurveyorId!=project.AssignedSurveyorId&&r.SurveyorId!=project.AdditionalSurveyorId)return BadRequest(new{error="Choose a Surveyor assigned to this project."});
        if(!await db.InspectionCampaigns.AnyAsync(c=>c.Id==r.CampaignId&&c.ProjectId==projectId,ct))return BadRequest(new{error="Select this project's approved planning allocation."});
        if(r.Date==default||r.EquipmentIds.Count==0||r.Notes?.Length>2000)return BadRequest(new{error="Select equipment and a planned date; notes allow 2,000 characters."});
        if(await db.AreaEquipment.CountAsync(e=>r.EquipmentIds.Contains(e.Id)&&e.ProjectId==projectId&&e.AreaId==r.AreaId,ct)!=r.EquipmentIds.Distinct().Count())return BadRequest(new{error="Equipment must belong to the selected project area."});
        var start=new DateTimeOffset(r.Date.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);
        var result=await CreatePackage(r.CampaignId,new WorkPackageRequest("PLAN-"+Guid.NewGuid().ToString("N"),project.VesselOrSiteName,null,null,r.Date,r.Date,
            r.EquipmentIds.Select((id,i)=>new PlannedEquipmentTask(id,r.Grade,r.SurveyorId,i+1,null,null,null,null,start,start.AddDays(1).AddTicks(-1))).ToList()),ct);
        if(result is CreatedAtActionResult){db.AuditLogs.Add(new AuditLog{EntityType="Project",EntityId=projectId,ActorId=Actor,Action="Inspection Plan Created",Comments=System.Text.Json.JsonSerializer.Serialize(r)});await db.SaveChangesAsync(ct);}
        return result;
    }
    [HttpPut("tasks/{taskId:guid}/schedule")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> ScheduleTask(Guid taskId,SimpleScheduleRequest r,CancellationToken ct)
    {
        if(!CanPlan)return Forbid();
        var t=await db.InspectionTasks.SingleOrDefaultAsync(t=>t.Id==taskId,ct);if(t is null)return NotFound();
        var id=await db.InspectionPlans.Where(p=>p.Id==t.InspectionPlanId).Select(p=>p.ProjectId).SingleAsync(ct);
        var project=await new ProjectReportData(db).AccessibleProjects(User).SingleOrDefaultAsync(p=>p.Id==id,ct);if(project is null)return NotFound();
        if(t.StatusCode=="Completed"||await db.InspectionActivities.AnyAsync(a=>a.InspectionTaskId==t.Id,ct))return Conflict(new{error="Inspection work has started. Use its existing workflow; completed or QC-reviewed work cannot be edited here."});
        if(r.Date==default||string.IsNullOrWhiteSpace(r.Reason))return BadRequest(new{error="A planned date and change reason are required."});
        t.ScheduledAt=new DateTimeOffset(r.Date.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);t.DueAt=t.ScheduledAt.Value.AddDays(1).AddTicks(-1);
        db.AuditLogs.Add(new AuditLog{EntityType="Project",EntityId=project.Id,ActorId=Actor,Action="Inspection Schedule Changed",Comments=System.Text.Json.JsonSerializer.Serialize(new{taskId,r.Date,r.Reason})});await db.SaveChangesAsync(ct);return NoContent();
    }
}
public sealed record SimplePlanRequest(Guid CampaignId,Guid AreaId,List<Guid> EquipmentIds,Guid SurveyorId,DateOnly Date,InspectionType Grade,string? Notes);
public sealed record SimpleScheduleRequest(DateOnly Date,string Reason);
