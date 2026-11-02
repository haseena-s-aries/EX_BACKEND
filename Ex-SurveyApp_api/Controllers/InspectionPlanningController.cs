using System.Security.Claims;
using System.Text.Json;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController, Authorize, Ex_SurveyApp_api.Attributes.EndpointScopeInventory(Ex_SurveyApp_api.Attributes.EndpointScopeKind.TenantResource), Route("api/v1/inspection-planning")]
public sealed class InspectionPlanningController(SurveyDbContext db) : ControllerBase
{
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private bool CanPlan => User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Manager));
    private bool CanApprove => User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Qc));

    [HttpGet("campaigns")]
    [Authorize(Roles = "Admin,Manager,Qc,Surveyor")]
    public async Task<IActionResult> Campaigns(CancellationToken ct)
    {
        var query = db.InspectionCampaigns.AsNoTracking().AsQueryable();
        var isAdmin=User.IsInRole(nameof(UserRole.Admin));var isManager=User.IsInRole(nameof(UserRole.Manager));var isQc=User.IsInRole(nameof(UserRole.Qc));var isSurveyor=User.IsInRole(nameof(UserRole.Surveyor));
        if (!isAdmin)
            query = query.Where(x => db.Projects.Any(p => p.Id == x.ProjectId &&
                (isManager && (p.CreatedByUserId == Actor || db.Users.Any(q => q.Id == p.AssignedQcId && q.ManagerId == Actor)) ||
                isQc && (p.AssignedQcId == Actor || x.InspectionLeadUserId==Actor) || isSurveyor && (p.AssignedSurveyorId == Actor || db.InspectionTasks.Any(t=>t.AssignedUserId==Actor&&db.InspectionPlans.Any(plan=>plan.Id==t.InspectionPlanId&&plan.ProjectId==p.Id)) ))));
        var rows = await query.OrderByDescending(x => x.CreatedAt).Select(x => new {x.Id, x.ProjectId, x.InspectionLeadUserId, x.Name, x.Reference, x.StartsOn, x.EndsOn, x.Status, x.Revision, x.AllocationApproved, x.ClosePercent, x.DetailedPercent, x.MinimumDetailedCreditPercent, ProjectName = db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ProjectName).FirstOrDefault(), Site = db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.VesselOrSiteName).FirstOrDefault(), Client = db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ClientName).FirstOrDefault()}).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("campaigns")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateCampaign(CreateCampaignRequest r, CancellationToken ct)
    {
        if (r.StartsOn > r.EndsOn || string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Reference)) return BadRequest(new {error="Name, reference and a valid date range are required."});
        if (!await CanAccessProject(r.ProjectId, ct)) return NotFound();
        if (await db.InspectionCampaigns.AnyAsync(x => x.ProjectId == r.ProjectId && x.Reference == r.Reference.Trim(), ct)) return Conflict(new {error="Campaign reference already exists for this project."});
        var leadId=r.InspectionLeadUserId ?? await db.Projects.Where(p=>p.Id==r.ProjectId).Select(p=>(Guid?)p.AssignedQcId).SingleAsync(ct);
        if(!await IsApprovedQc(leadId,ct))return BadRequest(new {error="Select an active, approved QC user as the Inspection Lead."});
        var c = new InspectionCampaign {ProjectId=r.ProjectId, InspectionLeadUserId=leadId, Name=r.Name.Trim(), Reference=r.Reference.Trim(), StartsOn=r.StartsOn, EndsOn=r.EndsOn, CreatedByUserId=Actor};
        db.InspectionCampaigns.Add(c); db.PlanningApprovalEvents.Add(new PlanningApprovalEvent {CampaignId=c.Id, Kind=PlanningApprovalKind.Campaign, Revision=c.Revision, ActorUserId=Actor, Decision="Created"}); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Campaigns), new {id=c.Id}, c);
    }

    [HttpPut("campaigns/{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> EditCampaign(Guid id, EditCampaignRequest r, CancellationToken ct)
    {
        var c=await db.InspectionCampaigns.SingleOrDefaultAsync(x=>x.Id==id,ct); if(c is null||!await CanAccessProject(c.ProjectId,ct))return NotFound();
        if(c.Status is CampaignStatus.InProgress or CampaignStatus.Completed or CampaignStatus.Archived)return Conflict(new {error="Execution has started or campaign is closed."});
        if(r.StartsOn>r.EndsOn||string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Reference))return BadRequest();
        if(await db.InspectionCampaigns.AnyAsync(x=>x.Id!=id&&x.ProjectId==c.ProjectId&&x.Reference==r.Reference.Trim(),ct))return Conflict(new {error="Campaign reference already exists."});
        var leadId=r.InspectionLeadUserId ?? c.InspectionLeadUserId;
        if(!await IsApprovedQc(leadId,ct))return BadRequest(new {error="Select an active, approved QC user as the Inspection Lead."});
        c.InspectionLeadUserId=leadId;c.Name=r.Name.Trim();c.Reference=r.Reference.Trim();c.StartsOn=r.StartsOn;c.EndsOn=r.EndsOn;c.Revision++;c.AllocationApproved=false;c.AllocationApprovedAt=null;c.AllocationApprovedByUserId=null;
        db.PlanningApprovalEvents.Add(new PlanningApprovalEvent {CampaignId=id,Kind=PlanningApprovalKind.Campaign,Revision=c.Revision,ActorUserId=Actor,Decision="Revised",Comments=r.ChangeReason}); await db.SaveChangesAsync(ct);return Ok(c);
    }

    [HttpPut("campaigns/{id:guid}/status")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> SetCampaignStatus(Guid id,CampaignStatusRequest r,CancellationToken ct)
    {
        var c=await db.InspectionCampaigns.SingleOrDefaultAsync(x=>x.Id==id,ct);if(c is null||!await CanAccessProject(c.ProjectId,ct))return NotFound();
        if(!Enum.IsDefined(r.Status))return BadRequest(new {error="Unknown campaign status."});
        if(c.Status is CampaignStatus.Completed or CampaignStatus.Archived)return Conflict(new {error="Completed or archived campaigns are immutable."});
        if(r.Status is CampaignStatus.Planned or CampaignStatus.InProgress&&!c.AllocationApproved)return Conflict(new {error="Approve campaign allocation before planning or execution."});
        if(r.Status==CampaignStatus.InProgress&&!await db.InspectionWorkPackages.AnyAsync(x=>x.CampaignId==id,ct))return Conflict(new {error="Create at least one work package before execution."});
        if(r.Status is CampaignStatus.Completed or CampaignStatus.Archived)
        {
            var active=await (from t in db.InspectionTasks join p in db.InspectionPlans on t.InspectionPlanId equals p.Id where p.ProjectId==c.ProjectId&&t.ScheduleReference==c.Reference&&t.StatusCode!="Completed"&&t.StatusCode!="Cancelled" select t.Id).AnyAsync(ct);
            if(active)return Conflict(new {error="Complete or cancel all campaign tasks before closing or archiving."});
        }
        var old=c.Status;c.Status=r.Status;c.Revision++;db.PlanningApprovalEvents.Add(new PlanningApprovalEvent{CampaignId=id,Kind=PlanningApprovalKind.Campaign,Revision=c.Revision,ActorUserId=Actor,Decision="StatusChanged",Comments=$"{old} -> {r.Status}. {r.Reason}"});await db.SaveChangesAsync(ct);return Ok(new{c.Id,c.Status,c.Revision});
    }

    [HttpPut("campaigns/{id:guid}/allocation")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> ConfigureAllocation(Guid id, AllocationRequest r, CancellationToken ct)
    {
        var c=await db.InspectionCampaigns.SingleOrDefaultAsync(x=>x.Id==id,ct);if(c is null||!await CanAccessProject(c.ProjectId,ct))return NotFound();if(c.AllocationApproved)return Conflict(new {error="Approved allocation is frozen. Create a planning revision to change it."});
        if(r.ClosePercent<0||r.DetailedPercent<0||r.MinimumDetailedCreditPercent<0||r.ClosePercent+r.DetailedPercent>100||!Enum.IsDefined(r.RoundingRule))return BadRequest(new {error="Provide valid percentages and an approved rounding rule."});
        c.ClosePercent=r.ClosePercent;c.DetailedPercent=r.DetailedPercent;c.MinimumDetailedCreditPercent=r.MinimumDetailedCreditPercent;c.RoundingRule=r.RoundingRule;c.Revision++;
        foreach(var list in await db.CampaignAnnualLists.Where(x=>x.CampaignId==id).ToListAsync(ct))ApplyAllocation(c,list);
        db.PlanningApprovalEvents.Add(new PlanningApprovalEvent{CampaignId=id,Kind=PlanningApprovalKind.Allocation,Revision=c.Revision,ActorUserId=Actor,Decision="Configured"});
        await db.SaveChangesAsync(ct);return Ok(await AllocationView(id,ct));
    }

    [HttpPost("campaigns/{id:guid}/annual-lists")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> SetAnnualList(Guid id, AnnualListRequest r, CancellationToken ct)
    {
        var c=await db.InspectionCampaigns.SingleOrDefaultAsync(x=>x.Id==id,ct);if(c is null||!await CanAccessProject(c.ProjectId,ct))return NotFound();if(c.AllocationApproved)return Conflict(new {error="Approved allocation is frozen."});
        if(await db.CampaignAnnualLists.AnyAsync(x=>x.CampaignId==id&&x.Year==r.Year,ct))return Conflict(new {error="An annual list already exists for this year."});
        if(r.Year<2000||r.EquipmentIds.Count==0||r.EquipmentIds.Distinct().Count()!=r.EquipmentIds.Count)return BadRequest(new {error="Annual list requires unique existing equipment IDs."});
        if(await db.AreaEquipment.CountAsync(x=>r.EquipmentIds.Contains(x.Id)&&x.ProjectId==c.ProjectId,ct)!=r.EquipmentIds.Count)return BadRequest(new {error="Every equipment ID must belong to the selected project."});
        if(await db.CampaignEquipment.AnyAsync(x=>x.CampaignId==id&&r.EquipmentIds.Contains(x.AreaEquipmentId),ct))return Conflict(new {error="An equipment item is already assigned to an annual list in this campaign."});
        c.Revision++;var list=new CampaignAnnualList{CampaignId=id,Year=r.Year,EquipmentCount=r.EquipmentIds.Count,Revision=c.Revision};db.CampaignAnnualLists.Add(list);await db.SaveChangesAsync(ct);
        var equipment=await db.AreaEquipment.Where(x=>r.EquipmentIds.Contains(x.Id)).Select(x=>new {x.Id,x.Type}).ToListAsync(ct);
        db.CampaignEquipment.AddRange(equipment.Select(x=>new CampaignEquipment{CampaignId=id,AnnualListId=list.Id,AreaEquipmentId=x.Id,EquipmentTypeSnapshot=x.Type,RiskScore=r.RiskScores?.GetValueOrDefault(x.Id.ToString())}));
        if(c.RoundingRule.HasValue)ApplyAllocation(c,list);
        db.PlanningApprovalEvents.Add(new PlanningApprovalEvent{CampaignId=id,Kind=PlanningApprovalKind.Campaign,Revision=c.Revision,ActorUserId=Actor,Decision="AnnualListAdded",Comments=$"Year {list.Year}, {list.EquipmentCount} equipment"});await db.SaveChangesAsync(ct);return Ok(new {list.Id,list.Year,list.EquipmentCount,list.PlannedCloseCount,list.PlannedDetailedCount,list.ActualCloseCount,list.ActualDetailedCount});
    }

    [HttpGet("campaigns/{id:guid}/allocation")]
    [Authorize(Roles = "Admin,Manager,Qc,Surveyor")]
    public async Task<IActionResult> Allocation(Guid id,CancellationToken ct){if(!await CanReadCampaign(id,ct))return NotFound();return Ok(await AllocationView(id,ct));}

    [HttpPost("campaigns/{id:guid}/allocation/approve")]
    [Authorize(Roles = "Admin,Qc")]
    public async Task<IActionResult> ApproveAllocation(Guid id,ApprovalRequest r,CancellationToken ct)
    {
        var c=await db.InspectionCampaigns.SingleOrDefaultAsync(x=>x.Id==id,ct);if(c is null||!await CanAccessProject(c.ProjectId,ct))return NotFound();if(!IsCampaignApprover(c))return Forbid();
        if(c.ClosePercent is null||c.DetailedPercent is null||c.MinimumDetailedCreditPercent is null||c.RoundingRule is null)return Conflict(new {error="Allocation rules are not configured."});
        var lists=await db.CampaignAnnualLists.Where(x=>x.CampaignId==id).ToListAsync(ct);if(lists.Count==0)return Conflict(new {error="Add annual equipment lists before approval."});foreach(var x in lists)ApplyAllocation(c,x);
        c.AllocationApproved=true;c.AllocationApprovedByUserId=Actor;c.AllocationApprovedAt=DateTimeOffset.UtcNow;c.Status=CampaignStatus.Planned;
        var allocationAudit=$"Close={c.ClosePercent}%; Detailed={c.DetailedPercent}%; MinimumDetailedCredit={c.MinimumDetailedCreditPercent}%; Rounding={c.RoundingRule}; Annual="+string.Join(",",lists.Select(x=>$"{x.Year}:{x.EquipmentCount}/{x.PlannedCloseCount}C/{x.PlannedDetailedCount}D"));
        db.PlanningApprovalEvents.Add(new PlanningApprovalEvent{CampaignId=id,Kind=PlanningApprovalKind.Allocation,Revision=c.Revision,ActorUserId=Actor,Decision="Approved",Comments=string.Join(" | ",new[]{r.Comments,allocationAudit}.Where(x=>!string.IsNullOrWhiteSpace(x)))});await db.SaveChangesAsync(ct);return Ok(await AllocationView(id,ct));
    }

    [HttpPost("campaigns/{campaignId:guid}/annual-lists/{listId:guid}/sampling")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> PreviewSampling(Guid campaignId,Guid listId,SamplingRequest r,CancellationToken ct)
    {
        if(!await CanReadCampaign(campaignId,ct))return NotFound();var c=await db.InspectionCampaigns.SingleAsync(x=>x.Id==campaignId,ct);var list=await db.CampaignAnnualLists.SingleOrDefaultAsync(x=>x.Id==listId&&x.CampaignId==campaignId,ct);if(list is null)return NotFound();if(!c.AllocationApproved||!list.PlannedDetailedCount.HasValue)return Conflict(new {error="Approve allocation before sampling."});
        if(await db.SamplingApprovals.AnyAsync(x=>x.AnnualListId==listId&&x.CampaignRevision==c.Revision&&x.IsFrozen,ct))return Conflict(new {error="Approved sample is frozen for this planning revision."});
        var population=await db.CampaignEquipment.Where(x=>x.AnnualListId==listId).ToListAsync(ct);var count=list.PlannedDetailedCount.Value;Guid[] selected;
        switch(r.Method){case SamplingMethod.Random: if(r.RandomSeed is null)return BadRequest(new {error="Record a seed for reproducible random sampling."}); selected=InspectionPlanningCalculator.SelectRandom(population.Select(x=>x.AreaEquipmentId),count,r.RandomSeed.Value);break;
          case SamplingMethod.RiskBased: if(population.Any(x=>!x.RiskScore.HasValue))return BadRequest(new {error="Risk scores are required for every item before risk-based sampling."});selected=population.OrderByDescending(x=>x.RiskScore).ThenBy(x=>x.AreaEquipmentId).Take(count).Select(x=>x.AreaEquipmentId).ToArray();break;
          case SamplingMethod.EquipmentType: selected=RoundRobinTypes(population,count);break;
          case SamplingMethod.Manual: if(string.IsNullOrWhiteSpace(r.Justification)||r.SelectedEquipmentIds is null||r.SelectedEquipmentIds.Distinct().Count()!=count||r.SelectedEquipmentIds.Any(x=>population.All(p=>p.AreaEquipmentId!=x)))return BadRequest(new {error="Manual sample needs a justification and exactly the planned number of unique in-list items."});selected=r.SelectedEquipmentIds.ToArray();break;
          default:return BadRequest();}
        if(r.RuleVersion<1)return BadRequest(new {error="Sampling rule version must be positive."});
        var sample=new SamplingApproval{CampaignId=campaignId,CampaignRevision=c.Revision,AnnualListId=listId,Method=r.Method,RuleVersion=r.RuleVersion,RandomSeed=r.RandomSeed,Justification=r.Justification,SelectedEquipmentIdsJson=JsonSerializer.Serialize(selected),OverridesJson=r.Overrides is null?null:JsonSerializer.Serialize(r.Overrides)};db.SamplingApprovals.Add(sample);await db.SaveChangesAsync(ct);return Ok(new {sample.Id,sample.CampaignRevision,sample.Method,sample.RuleVersion,sample.RandomSeed,sample.SelectedAt,EquipmentIds=selected,IsFrozen=false});
    }

    [HttpPost("sampling/{id:guid}/approve")]
    [Authorize(Roles = "Admin,Qc")]
    public async Task<IActionResult> ApproveSampling(Guid id,ApprovalRequest r,CancellationToken ct)
    {
        var s=await db.SamplingApprovals.SingleOrDefaultAsync(x=>x.Id==id,ct);if(s is null)return NotFound();var campaign=await db.InspectionCampaigns.SingleAsync(x=>x.Id==s.CampaignId,ct);if(!await CanAccessProject(campaign.ProjectId,ct))return NotFound();if(!IsCampaignApprover(campaign))return Forbid();if(s.IsFrozen)return Conflict(new {error="Sample already approved and frozen."});if(s.CampaignRevision!=campaign.Revision||!campaign.AllocationApproved)return Conflict(new {error="Sample belongs to a superseded planning revision or unapproved allocation."});if(await db.SamplingApprovals.AnyAsync(x=>x.AnnualListId==s.AnnualListId&&x.CampaignRevision==campaign.Revision&&x.IsFrozen,ct))return Conflict(new {error="A different sample is already approved for this annual list and planning revision."});
        var list=await db.CampaignAnnualLists.SingleAsync(x=>x.Id==s.AnnualListId,ct);var selectedIds=JsonSerializer.Deserialize<Guid[]>(s.SelectedEquipmentIdsJson)??[];if(selectedIds.Length!=list.PlannedDetailedCount||selectedIds.Distinct().Count()!=selectedIds.Length)return Conflict(new {error="Sample no longer matches the approved detailed allocation."});s.IsFrozen=true;s.ApprovedByUserId=Actor;s.ApprovedAt=DateTimeOffset.UtcNow;
        db.PlanningApprovalEvents.Add(new PlanningApprovalEvent{CampaignId=s.CampaignId,Kind=PlanningApprovalKind.Sampling,Revision=s.RuleVersion,ActorUserId=Actor,Decision="Approved",Comments=r.Comments});await db.SaveChangesAsync(ct);return Ok(new {s.Id,s.IsFrozen,s.ApprovedByUserId,s.ApprovedAt,EquipmentIds=JsonSerializer.Deserialize<Guid[]>(s.SelectedEquipmentIdsJson)});
    }

    [HttpGet("campaigns/{campaignId:guid}/sampling")]
    [Authorize(Roles = "Admin,Manager,Qc")]
    public async Task<IActionResult> SamplingHistory(Guid campaignId,CancellationToken ct)
    {if(!await CanReadCampaign(campaignId,ct))return NotFound();return Ok(await db.SamplingApprovals.AsNoTracking().Where(x=>x.CampaignId==campaignId).OrderByDescending(x=>x.SelectedAt).Select(x=>new{x.Id,x.AnnualListId,x.CampaignRevision,x.Method,x.RuleVersion,x.RandomSeed,x.Justification,x.SelectedEquipmentIdsJson,x.OverridesJson,x.SelectedAt,x.IsFrozen,x.ApprovedByUserId,x.ApprovedAt}).ToListAsync(ct));}

    [HttpPost("campaigns/{campaignId:guid}/work-packages")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreatePackage(Guid campaignId,WorkPackageRequest r,CancellationToken ct)
    {
        var c=await db.InspectionCampaigns.SingleOrDefaultAsync(x=>x.Id==campaignId,ct);if(c is null||!await CanAccessProject(c.ProjectId,ct))return NotFound();if(!c.AllocationApproved)return Conflict(new {error="Approve annual allocation before creating tasks."});if(r.Equipment.Count==0||string.IsNullOrWhiteSpace(r.Reference))return BadRequest();
        var ids=r.Equipment.Select(x=>x.EquipmentId).ToArray();if(ids.Distinct().Count()!=ids.Length)return BadRequest(new {error="Duplicate equipment in request."});
        var equipment=await db.AreaEquipment.Where(x=>ids.Contains(x.Id)&&x.ProjectId==c.ProjectId).ToListAsync(ct);if(equipment.Count!=ids.Length)return BadRequest(new {error="Equipment must belong to the campaign project."});
        var membership=await db.CampaignEquipment.Where(x=>x.CampaignId==campaignId&&ids.Contains(x.AreaEquipmentId)).ToListAsync(ct);if(membership.Count!=ids.Length)return BadRequest(new {error="Tasks can only be created for equipment in this campaign's annual lists."});
        foreach(var annualGroup in membership.GroupBy(x=>x.AnnualListId)){
            var annual=await db.CampaignAnnualLists.SingleAsync(x=>x.Id==annualGroup.Key,ct);var gradeCounts=r.Equipment.Join(annualGroup,x=>x.EquipmentId,x=>x.AreaEquipmentId,(x,_)=>x).GroupBy(x=>x.Grade).ToDictionary(x=>x.Key,x=>x.Count());
            var current=await (from task in db.InspectionTasks join planRow in db.InspectionPlans on task.InspectionPlanId equals planRow.Id join ce in db.CampaignEquipment on task.AreaEquipmentId equals ce.AreaEquipmentId where planRow.ProjectId==c.ProjectId&&task.ScheduleReference==c.Reference&&ce.AnnualListId==annual.Id&&task.StatusCode!="Cancelled" select task.PlannedGrade).ToListAsync(ct);
            var closePlanned=(current.Count(x=>x==InspectionType.Close)+gradeCounts.GetValueOrDefault(InspectionType.Close));var detailedPlanned=(current.Count(x=>x==InspectionType.Detailed)+gradeCounts.GetValueOrDefault(InspectionType.Detailed));var visualPlanned=(current.Count(x=>x==InspectionType.Visual)+gradeCounts.GetValueOrDefault(InspectionType.Visual));
            var visualCapacity=annual.EquipmentCount-(annual.PlannedCloseCount??0)-(annual.PlannedDetailedCount??0);
            if(closePlanned>(annual.PlannedCloseCount??0)||detailedPlanned>(annual.PlannedDetailedCount??0)||visualPlanned>visualCapacity)return Conflict(new {error=$"Work package exceeds approved annual grade allocation for {annual.Year}."});
            var detailedIds=r.Equipment.Where(x=>x.Grade==InspectionType.Detailed).Select(x=>x.EquipmentId).Intersect(annualGroup.Select(x=>x.AreaEquipmentId)).ToArray();
            if(detailedIds.Length>0){var frozenSamples=await db.SamplingApprovals.AsNoTracking().Where(x=>x.AnnualListId==annual.Id&&x.CampaignRevision==c.Revision&&x.IsFrozen).Select(x=>x.SelectedEquipmentIdsJson).ToListAsync(ct);var approvedIds=frozenSamples.SelectMany(x=>JsonSerializer.Deserialize<Guid[]>(x)??[]).ToHashSet();if(detailedIds.Any(x=>!approvedIds.Contains(x)))return Conflict(new {error=$"Detailed tasks must be selected in an approved frozen sample for {annual.Year} planning revision {c.Revision}."});}
        }
        foreach(var item in r.Equipment){if(!Enum.IsDefined(item.Grade))return BadRequest(new {error="Invalid inspection grade."});if(await db.InspectionTasks.AnyAsync(t=>t.AreaEquipmentId==item.EquipmentId&&t.StatusCode!="Completed"&&t.StatusCode!="Cancelled",ct))return Conflict(new {error=$"Equipment {item.EquipmentId} already has an active task."});if(item.AssignedUserId.HasValue&&!await db.Users.AnyAsync(u=>u.Id==item.AssignedUserId&&u.Role==UserRole.Surveyor&&u.IsActive&&u.ApprovalStatus==ApprovalStatus.Approved,ct))return BadRequest(new {error="Assigned inspectors must be active approved Surveyors."});}
        var defaultSurveyor=await db.Projects.Where(x=>x.Id==c.ProjectId).Select(x=>x.AssignedSurveyorId).SingleAsync(ct);
        var package=new InspectionWorkPackage{CampaignId=campaignId,ProjectId=c.ProjectId,Reference=r.Reference.Trim(),Site=r.Site,Area=r.Area,Rotation=r.Rotation,PlannedStart=r.PlannedStart,PlannedEnd=r.PlannedEnd,CreatedByUserId=Actor};db.InspectionWorkPackages.Add(package);
        var plan=await db.InspectionPlans.FirstOrDefaultAsync(x=>x.ProjectId==c.ProjectId&&x.SourceReference==c.Reference,ct);if(plan is null){plan=new InspectionPlan{ProjectId=c.ProjectId,Name=c.Name,SourceReference=c.Reference,StartsAt=new DateTimeOffset(c.StartsOn.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero),EndsAt=new DateTimeOffset(c.EndsOn.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero)};db.InspectionPlans.Add(plan);}
        foreach(var item in r.Equipment){var assignee=item.AssignedUserId??defaultSurveyor;var task=new InspectionTask{InspectionPlanId=plan.Id,AreaEquipmentId=item.EquipmentId,AssignedUserId=assignee,WorkPackageId=package.Id,PlannedGrade=item.Grade,RouteOrder=item.RouteOrder,Rotation=r.Rotation,PriorityCode=item.PriorityCode,RequiredPermits=item.RequiredPermits,IsolationPrerequisites=item.IsolationPrerequisites,ChecklistVersionId=item.ChecklistVersionId,ScheduledAt=item.ScheduledAt,DueAt=item.DueAt,ScheduleReference=c.Reference,WorkPackageReference=r.Reference};db.InspectionTasks.Add(task);db.InspectionTaskAssignmentEvents.Add(new InspectionTaskAssignmentEvent{InspectionTaskId=task.Id,NewUserId=assignee,ActorUserId=Actor,Reason=item.AssignedUserId.HasValue?"Initial package assignment":"Project assigned Surveyor"});}
        c.Status=CampaignStatus.Planned;await db.SaveChangesAsync(ct);return CreatedAtAction(nameof(WorkPackages),new {campaignId},new {package.Id,package.Reference,TaskCount=ids.Length});
    }

    [HttpGet("campaigns/{campaignId:guid}/work-packages")]
    [Authorize(Roles = "Admin,Manager,Qc,Surveyor")]
    public async Task<IActionResult> WorkPackages(Guid campaignId,CancellationToken ct)
    {if(!await CanReadCampaign(campaignId,ct))return NotFound();var isSurveyor=User.IsInRole(nameof(UserRole.Surveyor));var q=db.InspectionWorkPackages.AsNoTracking().Where(x=>x.CampaignId==campaignId);if(isSurveyor)q=q.Where(x=>db.InspectionTasks.Any(t=>t.WorkPackageId==x.Id&&t.AssignedUserId==Actor));var rows=await q.OrderBy(x=>x.PlannedStart).Select(x=>new{x.Id,x.Reference,x.Site,x.Area,x.Rotation,x.Status,x.PlannedStart,x.PlannedEnd}).ToListAsync(ct);var packageIds=rows.Select(x=>x.Id).ToArray();var tasks=await db.InspectionTasks.AsNoTracking().Where(x=>packageIds.Contains(x.WorkPackageId!.Value)&&(!isSurveyor||x.AssignedUserId==Actor)).Select(x=>new{x.Id,x.WorkPackageId,x.AreaEquipmentId,x.PlannedGrade,x.AssignedUserId,x.RouteOrder,x.PriorityCode,x.ScheduledAt,x.DueAt,x.StatusCode,x.IsInaccessible,x.CarryForwardReason,x.RequiredPermits,x.IsolationPrerequisites}).ToListAsync(ct);return Ok(rows.Select(x=>new{x.Id,x.Reference,x.Site,x.Area,x.Rotation,x.Status,x.PlannedStart,x.PlannedEnd,Tasks=tasks.Where(t=>t.WorkPackageId==x.Id).OrderBy(t=>t.RouteOrder).ToArray(),Completed=tasks.Count(t=>t.WorkPackageId==x.Id&&t.StatusCode=="Completed"),Assigned=tasks.Count(t=>t.WorkPackageId==x.Id&&t.AssignedUserId!=null)}));}

    [HttpPost("tasks/{taskId:guid}/reassign")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Reassign(Guid taskId,ReassignRequest r,CancellationToken ct)
    {var t=await db.InspectionTasks.SingleOrDefaultAsync(x=>x.Id==taskId,ct);if(t is null)return NotFound();var project=await db.InspectionPlans.Where(x=>x.Id==t.InspectionPlanId).Select(x=>x.ProjectId).SingleAsync(ct);if(!project.HasValue||!await CanAccessProject(project.Value,ct))return NotFound();if(!await db.Users.AnyAsync(u=>u.Id==r.UserId&&u.Role==UserRole.Surveyor&&u.IsActive&&u.ApprovalStatus==ApprovalStatus.Approved,ct))return BadRequest(new {error="Assignee must be an active approved Surveyor."});var old=t.AssignedUserId;t.AssignedUserId=r.UserId;db.InspectionTaskAssignmentEvents.Add(new InspectionTaskAssignmentEvent{InspectionTaskId=t.Id,PreviousUserId=old,NewUserId=r.UserId,ActorUserId=Actor,Reason=r.Reason});await db.SaveChangesAsync(ct);return Ok(new{t.Id,t.AssignedUserId});}

    [HttpPost("tasks/{taskId:guid}/inaccessible")]
    [Authorize(Roles = "Surveyor,Manager,Admin")]
    public async Task<IActionResult> Inaccessible(Guid taskId,InaccessibleRequest r,CancellationToken ct)
    {var t=await db.InspectionTasks.SingleOrDefaultAsync(x=>x.Id==taskId,ct);if(t is null)return NotFound();var project=await db.InspectionPlans.Where(x=>x.Id==t.InspectionPlanId).Select(x=>x.ProjectId).SingleAsync(ct);if(!project.HasValue||!await CanAccessProject(project.Value,ct))return NotFound();if(User.IsInRole(nameof(UserRole.Surveyor))&&t.AssignedUserId!=Actor)return Forbid();if(string.IsNullOrWhiteSpace(r.Reason))return BadRequest(new{error="Carry-forward reason is required."});t.IsInaccessible=true;t.CarryForwardReason=r.Reason.Trim();t.StatusCode="CarryForward";await db.SaveChangesAsync(ct);return Ok(new{t.Id,t.StatusCode,t.CarryForwardReason});}

    [HttpGet("campaigns/{id:guid}/progress")]
    [Authorize(Roles = "Admin,Manager,Qc,Surveyor")]
    public async Task<IActionResult> Progress(Guid id,CancellationToken ct)
    {
        if(!await CanReadCampaign(id,ct))return NotFound();var c=await db.InspectionCampaigns.AsNoTracking().SingleAsync(x=>x.Id==id,ct);var annual=await db.CampaignAnnualLists.AsNoTracking().Where(x=>x.CampaignId==id).Select(x=>new{x.Id,x.Year,x.EquipmentCount,x.PlannedCloseCount,x.PlannedDetailedCount}).ToListAsync(ct);var tasks=await (from t in db.InspectionTasks.AsNoTracking() join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id where p.ProjectId==c.ProjectId && t.ScheduleReference==c.Reference select new{t.Id,t.AreaEquipmentId,t.AssignedUserId,t.StatusCode,t.PlannedGrade,t.DueAt,t.IsInaccessible,t.Rotation}).ToListAsync(ct);var ids=tasks.Where(x=>x.AreaEquipmentId.HasValue).Select(x=>x.AreaEquipmentId!.Value).ToArray();var equipmentLookup=await db.CampaignEquipment.AsNoTracking().Where(x=>x.CampaignId==id).Select(x=>new{x.AreaEquipmentId,x.AnnualListId}).ToDictionaryAsync(x=>x.AreaEquipmentId,x=>x.AnnualListId,ct);
        var activityRows=await (from a in db.InspectionActivities.AsNoTracking() join t in db.InspectionTasks.AsNoTracking() on a.InspectionTaskId equals t.Id join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id where p.ProjectId==c.ProjectId&&t.ScheduleReference==c.Reference select new{TaskId=t.Id,EquipmentId=t.AreaEquipmentId,a.Grade,a.ReviewStatus,a.CompletedAt}).ToListAsync(ct);
        var summaries=annual.Select(a=>{var listTasks=tasks.Where(t=>t.AreaEquipmentId.HasValue&&equipmentLookup.GetValueOrDefault(t.AreaEquipmentId.Value)==a.Id).ToArray();var taskIds=listTasks.Select(t=>t.Id).ToArray();var acts=activityRows.Where(x=>taskIds.Contains(x.TaskId)).ToArray();var completedTasks=listTasks.Where(t=>t.StatusCode=="Completed"||acts.Any(x=>x.TaskId==t.Id&&x.CompletedAt!=null)).Select(t=>t.Id).Distinct().ToArray();var closeEquipment=acts.Where(x=>x.Grade==InspectionType.Close&&x.CompletedAt!=null).Select(x=>x.EquipmentId).Where(x=>x.HasValue).Distinct().Count();var detailedEquipment=acts.Where(x=>x.Grade==InspectionType.Detailed&&x.CompletedAt!=null).Select(x=>x.EquipmentId).Where(x=>x.HasValue).Distinct().Count();return new{a.Year,a.EquipmentCount,a.PlannedCloseCount,a.PlannedDetailedCount,Planned=listTasks.Length,Completed=completedTasks.Length,CloseCompleted=closeEquipment,DetailedCompleted=detailedEquipment,DetailedCreditAchievedPercent=a.EquipmentCount==0?0:Math.Round(detailedEquipment*100m/a.EquipmentCount,2),Submitted=acts.Count(x=>x.ReviewStatus==InspectionActivityReviewStatus.Submitted),QcApproved=acts.Count(x=>x.ReviewStatus==InspectionActivityReviewStatus.Accepted),Compliant=(int?)null,Pending=listTasks.Count(t=>t.StatusCode is "Planned" or "InProgress"),Overdue=listTasks.Count(t=>t.DueAt.HasValue&&t.DueAt.Value<DateTimeOffset.UtcNow&&t.StatusCode!="Completed"),Inaccessible=listTasks.Count(t=>t.IsInaccessible),RotationProgress=listTasks.GroupBy(t=>t.Rotation??"Unassigned").Select(g=>new{Rotation=g.Key,Total=g.Count(),Completed=g.Count(t=>t.StatusCode=="Completed")})};}).ToArray();
        var defects=await (from f in db.ActivityFindings.AsNoTracking() join a in db.InspectionActivities.AsNoTracking() on f.InspectionActivityId equals a.Id join t in db.InspectionTasks.AsNoTracking() on a.InspectionTaskId equals t.Id join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id where p.ProjectId==c.ProjectId&&t.ScheduleReference==c.Reference select new{f.StatusCode,f.SeverityCode}).ToListAsync(ct);
        return Ok(new{Campaign=new{c.Id,c.Name,c.Reference,c.Status,c.Revision},TotalEquipment=annual.Sum(x=>x.EquipmentCount),Planned=tasks.Count,Completed=tasks.Count(x=>x.StatusCode=="Completed"||activityRows.Any(a=>a.TaskId==x.Id&&a.CompletedAt!=null)),Pending=tasks.Count(x=>x.StatusCode is "Planned" or "InProgress"),Overdue=tasks.Count(x=>x.DueAt.HasValue&&x.DueAt.Value<DateTimeOffset.UtcNow&&x.StatusCode!="Completed"),Inaccessible=tasks.Count(x=>x.IsInaccessible),Submitted=activityRows.Count(x=>x.ReviewStatus==InspectionActivityReviewStatus.Submitted),QcApproved=activityRows.Count(x=>x.ReviewStatus==InspectionActivityReviewStatus.Accepted),Compliant=(int?)null,OpenDefectsByCategory=defects.Where(x=>x.StatusCode is not "Closed" and not "ClientClosed").GroupBy(x=>x.SeverityCode??"Unclassified").Select(g=>new{Category=g.Key,Count=g.Count()}),Annual=summaries,RotationProgress=tasks.GroupBy(x=>x.Rotation??"Unassigned").Select(g=>new{Rotation=g.Key,Total=g.Count(),Completed=g.Count(x=>x.StatusCode=="Completed")}),InspectorProductivity=tasks.Where(x=>x.AssignedUserId.HasValue).GroupBy(x=>x.AssignedUserId).Select(g=>new{UserId=g.Key,Assigned=g.Count(),Completed=g.Count(x=>x.StatusCode=="Completed")})});
    }

    [HttpGet("campaigns/{id:guid}/recurrences")]
    [Authorize(Roles = "Admin,Manager,Qc,Surveyor")]
    public async Task<IActionResult> Recurrences(Guid id,CancellationToken ct){if(!await CanReadCampaign(id,ct))return NotFound();return Ok(await db.InspectionRecurrences.AsNoTracking().Where(x=>x.CampaignId==id).OrderBy(x=>x.NextOccurrenceOn).ToListAsync(ct));}

    [HttpPost("campaigns/{id:guid}/recurrences")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateRecurrence(Guid id,CreateRecurrenceRequest r,CancellationToken ct)
    {var campaign=await db.InspectionCampaigns.SingleOrDefaultAsync(x=>x.Id==id,ct);if(campaign is null||!await CanAccessProject(campaign.ProjectId,ct))return NotFound();if(r.EveryMonths<1||r.EveryMonths>120||!Enum.IsDefined(r.Grade))return BadRequest(new{error="Recurrence must be 1–120 months with a supported inspection grade."});if(!await db.CampaignAnnualLists.AnyAsync(x=>x.Id==r.AnnualListId&&x.CampaignId==id,ct))return BadRequest(new{error="Select an annual equipment list in this campaign."});var rule=new InspectionRecurrence{CampaignId=id,AnnualListId=r.AnnualListId,Grade=r.Grade,EveryMonths=r.EveryMonths,NextOccurrenceOn=r.NextOccurrenceOn,CreatedByUserId=Actor};db.InspectionRecurrences.Add(rule);await db.SaveChangesAsync(ct);return Ok(rule);}

    [HttpPut("recurrences/{id:guid}/enabled")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> SetRecurrenceEnabled(Guid id,SetRecurrenceEnabledRequest r,CancellationToken ct){var rule=await db.InspectionRecurrences.SingleOrDefaultAsync(x=>x.Id==id,ct);if(rule is null)return NotFound();var project=await db.InspectionCampaigns.Where(x=>x.Id==rule.CampaignId).Select(x=>x.ProjectId).SingleAsync(ct);if(!await CanAccessProject(project,ct))return NotFound();rule.Enabled=r.Enabled;await db.SaveChangesAsync(ct);return Ok(rule);}

    private async Task<bool> CanReadCampaign(Guid id,CancellationToken ct){var project=await db.InspectionCampaigns.Where(x=>x.Id==id).Select(x=>x.ProjectId).SingleOrDefaultAsync(ct);return await CanAccessProject(project,ct);}
    private bool IsCampaignApprover(InspectionCampaign campaign)=>User.IsInRole(nameof(UserRole.Admin))||(User.IsInRole(nameof(UserRole.Qc))&&campaign.InspectionLeadUserId==Actor);
    private Task<bool> IsApprovedQc(Guid? userId,CancellationToken ct)=>userId.HasValue?db.Users.AnyAsync(u=>u.Id==userId.Value&&u.Role==UserRole.Qc&&u.IsActive&&u.ApprovalStatus==ApprovalStatus.Approved,ct):Task.FromResult(false);
    private async Task<bool> CanAccessProject(Guid? projectId,CancellationToken ct){if(!projectId.HasValue)return false;if(User.IsInRole(nameof(UserRole.Admin)))return await db.Projects.AnyAsync(x=>x.Id==projectId,ct);if(User.IsInRole(nameof(UserRole.Manager)))return await db.Projects.AnyAsync(x=>x.Id==projectId&&(x.CreatedByUserId==Actor||db.Users.Any(q=>q.Id==x.AssignedQcId&&q.ManagerId==Actor)),ct);if(User.IsInRole(nameof(UserRole.Qc)))return await db.Projects.AnyAsync(x=>x.Id==projectId&&(x.AssignedQcId==Actor||db.InspectionCampaigns.Any(c=>c.ProjectId==x.Id&&c.InspectionLeadUserId==Actor)),ct);if(User.IsInRole(nameof(UserRole.Surveyor)))return await db.Projects.AnyAsync(x=>x.Id==projectId&&(x.AssignedSurveyorId==Actor||db.InspectionTasks.Any(t=>t.AssignedUserId==Actor&&db.InspectionPlans.Any(p=>p.Id==t.InspectionPlanId&&p.ProjectId==x.Id))),ct);return false;}
    private void ApplyAllocation(InspectionCampaign c,CampaignAnnualList list){if(c.ClosePercent is null||c.DetailedPercent is null||c.MinimumDetailedCreditPercent is null||c.RoundingRule is null)return;var allocation=InspectionPlanningCalculator.Allocate(list.EquipmentCount,c.ClosePercent.Value,c.DetailedPercent.Value,c.MinimumDetailedCreditPercent.Value,c.RoundingRule.Value);list.PlannedCloseCount=allocation.Close;list.PlannedDetailedCount=allocation.Detailed;}
    private async Task<object> AllocationView(Guid id,CancellationToken ct){var c=await db.InspectionCampaigns.AsNoTracking().SingleAsync(x=>x.Id==id,ct);return new{c.ClosePercent,c.DetailedPercent,c.MinimumDetailedCreditPercent,c.RoundingRule,c.AllocationApproved,c.AllocationApprovedAt,c.AllocationApprovedByUserId,Annual=await db.CampaignAnnualLists.AsNoTracking().Where(x=>x.CampaignId==id).Select(x=>new{x.Id,x.Year,x.EquipmentCount,x.PlannedCloseCount,x.PlannedDetailedCount,x.ActualCloseCount,x.ActualDetailedCount}).ToListAsync(ct)};}
    private static Guid[] RoundRobinTypes(List<CampaignEquipment> population,int count){var groups=population.GroupBy(x=>x.EquipmentTypeSnapshot??"Unspecified").OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>new Queue<Guid>(x.OrderBy(y=>y.AreaEquipmentId).Select(y=>y.AreaEquipmentId))).ToArray();var output=new List<Guid>();while(output.Count<count){var progressed=false;foreach(var q in groups)if(q.Count>0&&output.Count<count){output.Add(q.Dequeue());progressed=true;}if(!progressed)break;}return output.ToArray();}
}

public sealed record CreateCampaignRequest(Guid ProjectId,string Name,string Reference,DateOnly StartsOn,DateOnly EndsOn,Guid? InspectionLeadUserId=null);
public sealed record EditCampaignRequest(string Name,string Reference,DateOnly StartsOn,DateOnly EndsOn,string? ChangeReason,Guid? InspectionLeadUserId=null);
public sealed record CampaignStatusRequest(CampaignStatus Status,string? Reason);
public sealed record AllocationRequest(decimal ClosePercent,decimal DetailedPercent,decimal MinimumDetailedCreditPercent,AllocationRoundingRule RoundingRule);
public sealed record AnnualListRequest(int Year,List<Guid> EquipmentIds,Dictionary<string,decimal>? RiskScores=null);
public sealed record ApprovalRequest(string? Comments);
public sealed record SamplingRequest(SamplingMethod Method,int RuleVersion,int? RandomSeed,string? Justification,List<Guid>? SelectedEquipmentIds,List<string>? Overrides);
public sealed record WorkPackageRequest(string Reference,string? Site,string? Area,string? Rotation,DateOnly? PlannedStart,DateOnly? PlannedEnd,List<PlannedEquipmentTask> Equipment);
public sealed record PlannedEquipmentTask(Guid EquipmentId,InspectionType Grade,Guid? AssignedUserId,int? RouteOrder,string? PriorityCode,string? RequiredPermits,string? IsolationPrerequisites,Guid? ChecklistVersionId,DateTimeOffset? ScheduledAt,DateTimeOffset? DueAt);
public sealed record ReassignRequest(Guid UserId,string? Reason);
public sealed record InaccessibleRequest(string Reason);
public sealed record CreateRecurrenceRequest(Guid AnnualListId,InspectionType Grade,int EveryMonths,DateOnly NextOccurrenceOn);
public sealed record SetRecurrenceEnabledRequest(bool Enabled);
