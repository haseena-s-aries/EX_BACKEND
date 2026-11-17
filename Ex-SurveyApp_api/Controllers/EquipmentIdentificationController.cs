using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController, Ex_SurveyApp_api.Attributes.EndpointScopeInventory(Ex_SurveyApp_api.Attributes.EndpointScopeKind.TenantResource), Route("api/v1/equipment-identification")]
public sealed partial class EquipmentIdentificationController(SurveyDbContext db) : ControllerBase
{
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [Authorize(Roles="Surveyor,Admin,Manager,Qc")]
    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve([FromQuery]string identifier,[FromQuery]string technology,CancellationToken ct)
    {
        var normalized=Normalize(identifier,technology);if(normalized is null)return BadRequest(new{state="invalid",message="Enter a valid identifier."});
        var technologies=technology=="QR"?new[]{"QR"}:new[]{"HF_RFID","NFC"};
        var matches=await db.EquipmentTagAssignments.AsNoTracking().Where(x=>x.IsActive&&technologies.Contains(x.Technology)&&x.NormalizedIdentifier==normalized)
            .Select(x=>x.EquipmentId).ToListAsync(ct);
        if(technology is "HF_RFID" or "NFC")
        {
            var legacy=await db.AreaEquipment.AsNoTracking().Where(x=>x.RfidTagUid!=null&&x.RfidTagUid.Trim().Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper()==normalized).Select(x=>x.Id).ToListAsync(ct);
            matches.AddRange(legacy);
        }
        var ids=matches.Distinct().ToArray();
        if(ids.Length==0)
        {
            var inactive=await db.EquipmentTagAssignments.AsNoTracking().AnyAsync(x=>!x.IsActive&&technologies.Contains(x.Technology)&&x.NormalizedIdentifier==normalized,ct);
            if(inactive)return StatusCode(StatusCodes.Status410Gone,new{state="inactive",message="This tag is retired or replaced. Do not identify equipment from it."});
            return NotFound(new{state="unknown",message="No equipment is registered with this identifier."});
        }
        if(ids.Length>1)return Conflict(new{state="duplicate",message="This active identifier matches more than one equipment item. Do not choose automatically."});
        var equipment=await (from x in AccessibleEquipment() join p in db.Projects.AsNoTracking() on x.ProjectId equals p.Id join a in db.ProjectAreas.AsNoTracking() on x.AreaId equals a.Id where x.Id==ids[0] select new{equipmentId=x.Id,x.ProjectId,projectName=p.ProjectName,site=p.VesselOrSiteName,areaId=x.AreaId,areaName=a.AreaName,x.TagNumber,equipmentCode=x.EquipmentCode,x.Type,x.Location,x.Manufacturer,x.Model,x.SerialNumber,x.ExProtectionType,x.RfidTagUid}).SingleOrDefaultAsync(ct);
        if(equipment is null)return NotFound(new{state="unknown",message="No authorized equipment matched this identifier."});
        var restrictTasksToActor=User.IsInRole("Surveyor");var actorId=Actor;
        var tasks=await (from task in db.InspectionTasks.AsNoTracking() join plan in db.InspectionPlans.AsNoTracking() on task.InspectionPlanId equals plan.Id
                         where task.AreaEquipmentId==equipment.equipmentId&&task.StatusCode!="Completed"&&task.StatusCode!="Cancelled"&&!task.IsInaccessible&&
                           (!restrictTasksToActor||task.AssignedUserId==actorId)
                         orderby task.DueAt select new{taskId=task.Id,grade=task.PlannedGrade,status=task.StatusCode,dueAt=task.DueAt,plan.ProjectId,plan.Name}).ToListAsync(ct);
        return Ok(new{state="matched",technology,normalizedIdentifier=normalized,equipment,activeTasks=tasks});
    }

    [Authorize(Roles="Surveyor,Admin,Manager,Qc")]
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery]string q,CancellationToken ct)
    {
        var term=q?.Trim();if(string.IsNullOrWhiteSpace(term)||term.Length<2)return BadRequest(new{error="Enter at least two characters."});
        var rows=await (from x in AccessibleEquipment() join p in db.Projects.AsNoTracking() on x.ProjectId equals p.Id join a in db.ProjectAreas.AsNoTracking() on x.AreaId equals a.Id where x.TagNumber.Contains(term)||x.EquipmentCode!=null&&x.EquipmentCode.Contains(term) orderby x.TagNumber select new{equipmentId=x.Id,x.ProjectId,projectName=p.ProjectName,areaName=a.AreaName,x.TagNumber,x.EquipmentCode,x.Type,x.Location,x.RfidTagUid}).Take(25).ToListAsync(ct);
        return Ok(new{items=rows});
    }

    [Authorize(Roles="Admin,Manager")]
    [HttpGet("equipment/{equipmentId:guid}/tags")]
    public async Task<IActionResult> History(Guid equipmentId,CancellationToken ct)
    {
        if(!User.IsInRole("Admin")&&!User.IsInRole("Manager"))return Forbid();
        var equipment=await AccessibleEquipment().Where(x=>x.Id==equipmentId).Select(x=>new{x.Id,x.ProjectId,x.TagNumber,x.RfidTagUid}).SingleOrDefaultAsync(ct);
        if(equipment is null)return NotFound();
        var current=await db.EquipmentTagAssignments.AsNoTracking().Where(x=>x.EquipmentId==equipmentId&&x.IsActive).OrderBy(x=>x.Technology).Select(x=>new{x.Id,x.Technology,x.NormalizedIdentifier,x.AssignedAt,x.AssignmentReason}).ToListAsync(ct);
        var history=await db.EquipmentTagHistory.AsNoTracking().Where(x=>x.EquipmentId==equipmentId).OrderByDescending(x=>x.OccurredAt).Select(x=>new{x.Id,x.AssignmentId,x.Technology,x.NormalizedIdentifier,x.ActionCode,x.Reason,x.ActorUserId,x.OccurredAt}).ToListAsync(ct);
        return Ok(new{equipment,current,history});
    }

    [Authorize(Roles="Admin,Manager")]
    [HttpPost("equipment/{equipmentId:guid}/registration-review")]
    public async Task<IActionResult> ReviewRegistration(Guid equipmentId,ReviewEquipmentRegistrationRequest request,CancellationToken ct)
    {
        if(!User.IsInRole("Admin")&&!User.IsInRole("Manager"))return Forbid();
        var decision=request.Decision?.Trim();
        if(decision is not ("Verified" or "ChangesRequested"))return BadRequest(new{error="Choose Verified or ChangesRequested."});
        var equipment=await AccessibleEquipment(true).SingleOrDefaultAsync(x=>x.Id==equipmentId,ct);
        if(equipment is null)return NotFound();
        if(equipment.CreatedByUserId==Actor)return Conflict(new{error="The equipment registrant cannot verify their own registration."});
        if(equipment.RegistrationStatus is not ("Submitted" or "ChangesRequested"))return Conflict(new{error="Only submitted or returned equipment registrations can be reviewed."});
        if(decision=="ChangesRequested"&&string.IsNullOrWhiteSpace(request.Comments))return BadRequest(new{error="Add a reason when returning an equipment registration."});
        equipment.RegistrationStatus=decision;
        equipment.UpdatedAt=DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog{EntityType="AreaEquipment",EntityId=equipment.Id,ActorId=Actor,Action="Equipment Registration Reviewed",Comments=$"Decision: {decision}. {request.Comments?.Trim()}".Trim()});
        await db.SaveChangesAsync(ct);
        return Ok(new{equipment.Id,equipment.ProjectId,equipment.RegistrationStatus,reviewedBy=Actor,reviewedAt=equipment.UpdatedAt,comments=request.Comments?.Trim()});
    }

    [Authorize(Roles="Admin,Manager")]
    [HttpPost("equipment/{equipmentId:guid}/tags")]
    public async Task<IActionResult> Assign(Guid equipmentId,AssignEquipmentTagRequest request,CancellationToken ct)
    {
        if(!User.IsInRole("Admin")&&!User.IsInRole("Manager"))return Forbid();
        var technology=request.Technology?.Trim().ToUpperInvariant()??"";var value=Normalize(request.Identifier,technology);
        if(value is null||!ValidTechnology(technology)||string.IsNullOrWhiteSpace(request.Reason)||request.Reason.Trim().Length>1000)return BadRequest(new{error="Choose HF_RFID, NFC, or QR and provide a valid identifier and reason."});
        await using var transaction=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct):null;
        var equipment=await AccessibleEquipment(true).SingleOrDefaultAsync(x=>x.Id==equipmentId,ct);if(equipment is null)return NotFound();
        var duplicate=await db.EquipmentTagAssignments.AnyAsync(x=>x.IsActive&&x.NormalizedIdentifier==value&&x.EquipmentId!=equipmentId&&
            (technology=="QR"?x.Technology=="QR":x.Technology=="HF_RFID"||x.Technology=="NFC"),ct);
        if(technology is "HF_RFID" or "NFC")duplicate|=await db.AreaEquipment.AnyAsync(x=>x.Id!=equipmentId&&x.RfidTagUid!=null&&x.RfidTagUid.Trim().Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper()==value,ct);
        if(duplicate)return Conflict(new{error="This identifier is already active on another equipment item."});
        var old=await db.EquipmentTagAssignments.SingleOrDefaultAsync(x=>x.EquipmentId==equipmentId&&x.Technology==technology&&x.IsActive,ct);
        var legacy=technology=="HF_RFID"?equipment.RfidTagUid?.Trim().ToUpperInvariant():null;
        if(old is not null&&old.NormalizedIdentifier==value)return Conflict(new{error="This identifier is already assigned to this equipment."});
        if(old is not null||legacy is not null)
        {
            var expected=old?.Id.ToString("D")??legacy;
            if(!request.ConfirmReplacement||!string.Equals(request.ExpectedCurrentIdentifier,expected,StringComparison.OrdinalIgnoreCase))return Conflict(new{state="confirmation_required",currentIdentifier=expected,message="Confirm the current tag identity before replacement."});
            if(old is not null){old.IsActive=false;old.DeactivatedAt=DateTimeOffset.UtcNow;old.DeactivatedByUserId=Actor;old.DeactivationReason=request.Reason.Trim();db.EquipmentTagHistory.Add(HistoryRow(equipment.Id,equipment.ProjectId,old.Id,technology,old.NormalizedIdentifier,"Replaced",request.Reason.Trim()));await db.SaveChangesAsync(ct);}
            else db.EquipmentTagHistory.Add(HistoryRow(equipment.Id,equipment.ProjectId,null,technology,legacy!,"LegacyAssignmentClosed",request.Reason.Trim()));
        }
        var assignment=new EquipmentTagAssignment{EquipmentId=equipment.Id,ProjectId=equipment.ProjectId,Technology=technology,NormalizedIdentifier=value,AssignedByUserId=Actor,AssignmentReason=request.Reason.Trim()};db.EquipmentTagAssignments.Add(assignment);
        if(technology=="HF_RFID"){equipment.RfidTagUid=value;equipment.RfidIdentificationMethod="HF_RFID";equipment.UpdatedAt=DateTimeOffset.UtcNow;}
        db.EquipmentTagHistory.Add(HistoryRow(equipment.Id,equipment.ProjectId,assignment.Id,technology,value,old is null&&legacy is null?"Assigned":"ReplacementAssigned",request.Reason.Trim()));
        db.AuditLogs.Add(new AuditLog{EntityType="AreaEquipment",EntityId=equipment.Id,ActorId=Actor,Action="Equipment Tag Assigned",Comments=$"{technology} identifier assigned. Reason: {request.Reason.Trim()}"});
        try{await db.SaveChangesAsync(ct);if(transaction is not null)await transaction.CommitAsync(ct);}catch(DbUpdateException){return Conflict(new{error="An active assignment with this identifier or technology already exists."});}finally{if(transaction is not null)await transaction.DisposeAsync();}
        return Ok(new{assignment.Id,assignment.EquipmentId,assignment.ProjectId,assignment.Technology,assignment.NormalizedIdentifier,assignment.AssignedAt,assignment.AssignmentReason});
    }

    [Authorize(Roles="Admin,Manager")]
    [HttpPost("equipment/{equipmentId:guid}/tags/{assignmentId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid equipmentId,Guid assignmentId,DeactivateEquipmentTagRequest request,CancellationToken ct)
    {
        if(!User.IsInRole("Admin")&&!User.IsInRole("Manager"))return Forbid();
        if(string.IsNullOrWhiteSpace(request.Reason)||request.Reason.Trim().Length>1000)return BadRequest(new{error="A deactivation reason is required."});
        var equipment=await AccessibleEquipment(true).SingleOrDefaultAsync(x=>x.Id==equipmentId,ct);if(equipment is null)return NotFound();
        var assignment=await db.EquipmentTagAssignments.SingleOrDefaultAsync(x=>x.Id==assignmentId&&x.EquipmentId==equipmentId&&x.IsActive,ct);if(assignment is null)return NotFound();
        assignment.IsActive=false;assignment.DeactivatedAt=DateTimeOffset.UtcNow;assignment.DeactivatedByUserId=Actor;assignment.DeactivationReason=request.Reason.Trim();
        if(assignment.Technology=="HF_RFID"&&string.Equals(equipment.RfidTagUid,assignment.NormalizedIdentifier,StringComparison.OrdinalIgnoreCase)){equipment.RfidTagUid=null;equipment.RfidIdentificationMethod=null;equipment.UpdatedAt=DateTimeOffset.UtcNow;}
        db.EquipmentTagHistory.Add(HistoryRow(equipment.Id,equipment.ProjectId,assignment.Id,assignment.Technology,assignment.NormalizedIdentifier,"Deactivated",request.Reason.Trim()));
        db.AuditLogs.Add(new AuditLog{EntityType="AreaEquipment",EntityId=equipment.Id,ActorId=Actor,Action="Equipment Tag Deactivated",Comments=$"{assignment.Technology} identifier deactivated. Reason: {request.Reason.Trim()}"});await db.SaveChangesAsync(ct);
        return Ok(new{assignment.Id,assignment.IsActive,assignment.DeactivatedAt});
    }

    private IQueryable<AreaEquipment> AccessibleEquipment(bool tracking=false)
    {
        var query=tracking?db.AreaEquipment.AsQueryable():db.AreaEquipment.AsNoTracking();
        if(User.IsInRole("Admin"))return query;
        if(User.IsInRole("Surveyor"))return query.Where(e=>db.Projects.Any(p=>p.Id==e.ProjectId&&(p.AssignedSurveyorId == Actor || p.AdditionalSurveyorId == Actor))||db.InspectionTasks.Any(t=>t.AreaEquipmentId==e.Id&&t.AssignedUserId==Actor));
        var projects = new ProjectReportData(db).AccessibleProjects(User);
        return query.Where(e => projects.Any(p => p.Id == e.ProjectId));
    }
    private EquipmentTagHistory HistoryRow(Guid equipmentId,Guid projectId,Guid? assignmentId,string technology,string identifier,string action,string reason)=>new(){EquipmentId=equipmentId,ProjectId=projectId,AssignmentId=assignmentId,Technology=technology,NormalizedIdentifier=identifier,ActionCode=action,Reason=reason,ActorUserId=Actor};
    public static bool ValidTechnology(string technology)=>technology is "HF_RFID" or "NFC" or "QR";
    public static string? Normalize(string? value,string technology)
    {
        if(string.IsNullOrWhiteSpace(value))return null;var input=value.Trim().Normalize(NormalizationForm.FormKC);
        if(technology=="QR")return input.Length<=200?input:null;
        if(technology is not ("HF_RFID" or "NFC"))return null;
        var compact=new string(input.Where(c=>!char.IsWhiteSpace(c)&&c!=':'&&c!='-').ToArray()).ToUpperInvariant();
        return compact.Length is >=4 and <=128&&compact.All(Uri.IsHexDigit)?compact:null;
    }
}

public sealed record AssignEquipmentTagRequest(string Technology,string Identifier,string Reason,bool ConfirmReplacement=false,string? ExpectedCurrentIdentifier=null);
public sealed record DeactivateEquipmentTagRequest(string Reason);
