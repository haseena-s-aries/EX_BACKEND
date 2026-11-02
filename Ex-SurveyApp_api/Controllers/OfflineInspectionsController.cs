using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ex_SurveyApp_api.Contracts.V1;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ex_SurveyApp_api.Controllers;

public sealed class OfflineInspectionOptions { public bool Enabled { get; set; } }

[ApiController, Authorize(Roles = "Surveyor"), Ex_SurveyApp_api.Attributes.EndpointScopeInventory(Ex_SurveyApp_api.Attributes.EndpointScopeKind.TenantResource), Route("api/v1/offline-inspections")]
public sealed class OfflineInspectionsController(
    SurveyDbContext db,
    IInspectionActivityApplicationService activities,
    IOptions<OfflineInspectionOptions> options,
    IProjectPhotoStorage photoStorage,
    IOptions<InspectionActivityOptions> activityOptions,
    IOptions<EquipmentDocumentOptions> documentOptions) : ControllerBase
{
    private Guid ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private bool Enabled => options.Value.Enabled;
    private bool DocumentUsageEnabled => activityOptions.Value.DocumentUsageEnabled;

    [HttpGet("packages")]
    public async Task<IActionResult> Packages(CancellationToken ct)
    {
        if (!Enabled) return Conflict(new {error="Offline inspection is not enabled on this installation."});
        var actor=await db.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==ActorId&&x.IsActive&&x.ApprovalStatus==ApprovalStatus.Approved&&x.Role==UserRole.Surveyor,ct);
        if(actor is null)return Forbid();
        var rows=await (from task in db.InspectionTasks.AsNoTracking()
                        join plan in db.InspectionPlans.AsNoTracking() on task.InspectionPlanId equals plan.Id
                        join equipment in db.AreaEquipment.AsNoTracking() on task.AreaEquipmentId equals equipment.Id
                        join project in db.Projects.AsNoTracking() on plan.ProjectId equals project.Id
                        join area in db.ProjectAreas.AsNoTracking() on equipment.AreaId equals area.Id
                        join package in db.InspectionWorkPackages.AsNoTracking() on task.WorkPackageId equals package.Id
                        where task.AssignedUserId==ActorId&&task.StatusCode!="Completed"&&task.StatusCode!="Cancelled"&&!task.IsInaccessible
                        orderby package.PlannedStart,task.RouteOrder
                        select new OfflineTaskRow(task.Id,package.Id,package.Reference,package.Site,package.Area,package.Rotation,package.PlannedStart,package.PlannedEnd,
                          project.Id,project.ProjectName,project.VesselOrSiteName,area.Id,area.AreaName,
                          equipment.Id,equipment.TagNumber,equipment.SequenceNo,equipment.Type,equipment.Location,equipment.Manufacturer,equipment.Model,equipment.SerialNumber,equipment.Description,equipment.ExProtectionType,equipment.GasGroup,equipment.TemperatureClass,equipment.HazardousAreaZone,equipment.IpRating,equipment.CertificateNumber,equipment.CircuitId,equipment.RfidTagUid,
                          task.PlannedGrade,task.RouteOrder,task.PriorityCode,task.ScheduledAt,task.DueAt,task.RequiredPermits,task.IsolationPrerequisites,task.StatusCode)).ToListAsync(ct);
        var packages=new List<object>();
        foreach(var group in rows.GroupBy(x=>x.WorkPackageId))
        {
            var tasks=new List<object>();
            foreach(var row in group)
            {
                var documents=await BuildDocumentManifestAsync(row,ct);
                var identifiers=(await db.EquipmentTagAssignments.AsNoTracking().Where(x=>x.EquipmentId==row.EquipmentId&&x.IsActive).Select(x=>new{technology=x.Technology,identifier=x.NormalizedIdentifier}).ToListAsync(ct)).Cast<object>().ToList();
                if(!string.IsNullOrWhiteSpace(row.RfidTagUid)&&!identifiers.Any(x=>x.GetType().GetProperty("technology")!.GetValue(x)!.ToString()=="HF_RFID"))identifiers.Add(new{technology="HF_RFID",identifier=row.RfidTagUid.Trim().ToUpperInvariant()});
                var grade=row.Grade??InspectionType.Visual;
                var templates=await db.ChecklistTemplates.AsNoTracking().Where(x=>x.IsActive&&x.LifecycleStatus==ChecklistLifecycleStatus.Published&&x.InspectionType==grade).OrderByDescending(x=>x.Version).ToListAsync(ct);
                var template=templates.FirstOrDefault(x=>ChecklistApplicability.TemplateMatches(x,grade,row.ExProtectionType));
                if(template is null){tasks.Add(new{taskId=row.TaskId,project=new{id=row.ProjectId,name=row.ProjectName,site=row.ProjectSite},area=new{id=row.AreaId,name=row.AreaName},equipment=new{row.EquipmentId,row.TagNumber,row.SequenceNo,row.Type,row.Location,row.Manufacturer,row.Model,row.SerialNumber,row.Description,row.ExProtectionType,row.GasGroup,row.TemperatureClass,row.HazardousAreaZone,row.IpRating,row.CertificateNumber,row.CircuitId,identifiers},grade,routeOrder=row.RouteOrder,dueAt=row.DueAt,scheduledAt=row.ScheduledAt,documents,documentWarnings=documents.Count==0?new[]{"No project reference documents are currently available for this task."}:Array.Empty<string>(),checklist=(object?)null,ready=false,missing="No published applicable checklist"});continue;}
                var questions=await db.ChecklistQuestions.AsNoTracking().Where(x=>x.ChecklistTemplateId==template.Id).OrderBy(x=>x.SortOrder).ToListAsync(ct);
                var applicable=questions.Where(x=>ChecklistApplicability.QuestionMatches(x,row.ExProtectionType)).Select(q=>new{questionId=q.Id,questionCode=q.Id.ToString("N"),questionVersion=template.Version,q.Text,q.SortOrder,q.SectionCode,q.IsRequired,q.ResponseScaleCode,q.RequireRemarkOnFail,q.NumericReadingEnabled,q.NumericReadingRequired,q.MeasurementUnit,q.MinimumReading,q.MaximumReading}).ToArray();
                var ready=applicable.Length>0;
                tasks.Add(new{taskId=row.TaskId,project=new{id=row.ProjectId,name=row.ProjectName,site=row.ProjectSite},area=new{id=row.AreaId,name=row.AreaName},equipment=new{row.EquipmentId,row.TagNumber,row.SequenceNo,row.Type,row.Location,row.Manufacturer,row.Model,row.SerialNumber,row.Description,row.ExProtectionType,row.GasGroup,row.TemperatureClass,row.HazardousAreaZone,row.IpRating,row.CertificateNumber,row.CircuitId,identifiers},grade,routeOrder=row.RouteOrder,row.PriorityCode,row.ScheduledAt,row.DueAt,row.RequiredPermits,row.IsolationPrerequisites,row.StatusCode,checklist=new{id=template.Id,template.Name,template.Version,template.StandardReference,template.StandardEdition,template.ControlledSourceReference,template.ApplicableProtectionTypes,template.ApprovedAt,questions=applicable},documents,documentWarnings=documents.Count==0?new[]{"No project reference documents are currently available for this task."}:Array.Empty<string>(),ready,missing=ready?null:"Published checklist contains no applicable checkpoints"});
            }
            var taskArray=tasks.ToArray();var packageReady=taskArray.All(x=>(bool)x.GetType().GetProperty("ready")!.GetValue(x)!);
            packages.Add(new{id=group.Key,reference=group.First().PackageReference,site=group.First().PackageSite,area=group.First().PackageArea,rotation=group.First().Rotation,plannedStart=group.First().PlannedStart,plannedEnd=group.First().PlannedEnd,equipmentCount=taskArray.Length,ready=packageReady,downloadedAt=DateTimeOffset.UtcNow,tasks=taskArray,missingCount=taskArray.Count(x=>!(bool)x.GetType().GetProperty("ready")!.GetValue(x)!)});
        }
        return Ok(new{serverTime=DateTimeOffset.UtcNow,packages});
    }

    [HttpGet("tasks/{taskId:guid}/documents/{kind}/{documentId:guid}")]
    public async Task<IActionResult> DownloadTaskDocument(Guid taskId,string kind,Guid documentId,CancellationToken ct)
    {
        if(!Enabled)return Conflict(new{error="Offline inspection is not enabled on this installation."});
        var actor=await db.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==ActorId&&x.IsActive&&x.ApprovalStatus==ApprovalStatus.Approved&&x.Role==UserRole.Surveyor,ct);
        if(actor is null)return Forbid();
        var task=await (from t in db.InspectionTasks.AsNoTracking() join p in db.InspectionPlans.AsNoTracking() on t.InspectionPlanId equals p.Id join e in db.AreaEquipment.AsNoTracking() on t.AreaEquipmentId equals e.Id where t.Id==taskId&&t.AssignedUserId==ActorId select new{t.Id,ProjectId=p.ProjectId,AreaId=e.AreaId,EquipmentId=e.Id}).SingleOrDefaultAsync(ct);
        if(task is null)return NotFound();
        string? path=null;string? fileName=null;string? expectedHash=null;
        if(kind.Equals("ProjectPlan",StringComparison.OrdinalIgnoreCase)&&documentId==task.ProjectId)
        {
            var project=await db.Projects.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==task.ProjectId,ct);
            path=project?.HazardousAreaPlanPath;fileName=project?.HazardousAreaPlanFileName;
        }
        else if(kind.Equals("AreaPhoto",StringComparison.OrdinalIgnoreCase))
        {
            var photo=await db.ProjectAreaPhotos.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==documentId&&x.ProjectAreaId==task.AreaId,ct);
            path=photo?.FilePath;fileName=photo?.FileName;
        }
        else if(kind.Equals("EquipmentReference",StringComparison.OrdinalIgnoreCase))
        {
            var doc=documentOptions.Value.Enabled?await db.EquipmentReferenceDocuments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==documentId&&x.ProjectId==task.ProjectId&&x.Status==ReferenceDocumentStatus.Approved&&(!x.ExpiryDate.HasValue||x.ExpiryDate>=DateOnly.FromDateTime(DateTime.UtcNow))&&(x.EquipmentId==null||x.EquipmentId==task.EquipmentId)&&(x.AreaId==null||x.AreaId==task.AreaId),ct):null;
            path=doc?.StoragePath;fileName=doc?.OriginalFileName;expectedHash=doc?.Sha256;
        }
        if(string.IsNullOrWhiteSpace(path))return NotFound();
        var content=await photoStorage.OpenReadAsync(path,ct);if(content is null)return NotFound();
        if(expectedHash is not null){var measured=await MeasureAsync(content.Stream,ct);if(!measured.Sha256.Equals(expectedHash,StringComparison.OrdinalIgnoreCase)){await content.Stream.DisposeAsync();return Conflict(new{error="Stored document checksum does not match approved metadata."});}if(content.Stream.CanSeek)content.Stream.Position=0;else{await content.Stream.DisposeAsync();return Conflict(new{error="Stored document cannot be integrity checked."});}}
        Response.Headers["X-Content-Type-Options"]="nosniff";Response.Headers.CacheControl="private, no-store";
        return File(content.Stream,content.ContentType,Path.GetFileName(fileName??"reference-document"),enableRangeProcessing:true);
    }

    private async Task<List<object>> BuildDocumentManifestAsync(OfflineTaskRow row,CancellationToken ct)
    {
        var result=new List<object>();
        var project=await db.Projects.AsNoTracking().Where(x=>x.Id==row.ProjectId).Select(x=>new{x.HazardousAreaPlanPath,x.HazardousAreaPlanFileName,x.Revision,x.UpdatedAt}).SingleAsync(ct);
        if(!string.IsNullOrWhiteSpace(project.HazardousAreaPlanPath))
        {
            var content=await photoStorage.OpenReadAsync(project.HazardousAreaPlanPath,ct);
            if(content is not null)
            {
            await using(content.Stream){var measured=await MeasureAsync(content.Stream,ct);
                    result.Add(new{id=$"project-plan:{row.ProjectId:D}:{project.Revision??"unversioned"}:{measured.Sha256}",identity=$"project-plan:{row.ProjectId:D}",kind="ProjectPlan",documentId=row.ProjectId,scope="Project",scopeId=row.ProjectId,title=project.HazardousAreaPlanFileName??"Hazardous area plan",category="HazardousAreaPlan",revision=project.Revision??"Unversioned",issueDate=(DateTimeOffset?)null,fileName=project.HazardousAreaPlanFileName??"hazardous-area-plan.pdf",contentType=content.ContentType,sizeBytes=measured.Size,sha256=measured.Sha256,required=true,downloadUrl=$"/v1/offline-inspections/tasks/{row.TaskId:D}/documents/ProjectPlan/{row.ProjectId:D}",lastModified=project.UpdatedAt});}
            }
        }
        var areaFiles=await db.ProjectAreaPhotos.AsNoTracking().Where(x=>x.ProjectAreaId==row.AreaId).OrderByDescending(x=>x.UploadedAt).ToListAsync(ct);
        foreach(var photo in areaFiles)
        {
            var content=await photoStorage.OpenReadAsync(photo.FilePath,ct);if(content is null)continue;
            await using(content.Stream){var measured=await MeasureAsync(content.Stream,ct);
                result.Add(new{id=$"area-photo:{photo.Id:D}:{photo.UploadedAt:O}",identity=$"area-photo:{photo.Id:D}",kind="AreaPhoto",documentId=photo.Id,scope="Area",scopeId=row.AreaId,title=photo.FileName,category="AreaReferenceImage",revision=photo.UploadedAt.ToString("O"),issueDate=photo.UploadedAt,fileName=photo.FileName,contentType=content.ContentType,sizeBytes=measured.Size,sha256=measured.Sha256,required=false,downloadUrl=$"/v1/offline-inspections/tasks/{row.TaskId:D}/documents/AreaPhoto/{photo.Id:D}",lastModified=photo.UploadedAt});}
        }
        if(!documentOptions.Value.Enabled)return result;
        var approvedDocs=await db.EquipmentReferenceDocuments.AsNoTracking().Where(x=>x.ProjectId==row.ProjectId&&x.Status==ReferenceDocumentStatus.Approved&&(x.EquipmentId==null||x.EquipmentId==row.EquipmentId)&&(x.AreaId==null||x.AreaId==row.AreaId)).OrderBy(x=>x.CategoryCode).ThenByDescending(x=>x.ApprovedAt).ToListAsync(ct);
        var currentDocs=approvedDocs.OrderByDescending(x=>x.EquipmentId==row.EquipmentId).ThenByDescending(x=>x.AreaId==row.AreaId).ThenByDescending(x=>x.ApprovedAt).ToList();
        var approvedPolicies=await db.EquipmentDocumentRequirementPolicies.AsNoTracking().Where(x=>x.IsApproved&&(!x.ProjectId.HasValue||x.ProjectId==row.ProjectId)&&(x.EquipmentType==null||x.EquipmentType==row.Type)&&(x.WorkPackageId==null||x.WorkPackageId==row.WorkPackageId)).ToListAsync(ct);
        foreach(var doc in currentDocs)
        {
            var expired=doc.ExpiryDate.HasValue&&doc.ExpiryDate.Value<DateOnly.FromDateTime(DateTime.UtcNow);
            var required=approvedPolicies.Any(x=>x.CategoryCode==doc.CategoryCode&&x.IsRequired);
            var content=await photoStorage.OpenReadAsync(doc.StoragePath,ct);if(content is null)continue;
            await using(content.Stream){var measured=await MeasureAsync(content.Stream,ct);
                var integrityMismatch=!measured.Sha256.Equals(doc.Sha256,StringComparison.OrdinalIgnoreCase);
                result.Add(new{id=$"equipment-document:{doc.Id:D}:{doc.Revision??"unversioned"}:{doc.Sha256}",identity=$"equipment-document:{doc.Id:D}",kind="EquipmentReference",documentId=doc.Id,scope=doc.EquipmentId.HasValue?"Equipment":doc.AreaId.HasValue?"Area":"Project",scopeId=doc.EquipmentId??doc.AreaId??doc.ProjectId,title=doc.Title,category=doc.CategoryCode,revision=doc.Revision??"Unversioned",issueDate=doc.IssueDate,expiryDate=doc.ExpiryDate,expired,integrityMismatch,fileName=doc.OriginalFileName,contentType=doc.ContentType,sizeBytes=measured.Size,sha256=doc.Sha256,required,approvalStatus=doc.Status.ToString(),approvedAt=doc.ApprovedAt,downloadUrl=$"/v1/offline-inspections/tasks/{row.TaskId:D}/documents/EquipmentReference/{doc.Id:D}",lastModified=doc.ApprovedAt});}
        }
        // An approved required rule with no current approved, unexpired file is explicit missing work.
        foreach(var policy in approvedPolicies.Where(x=>x.IsRequired).GroupBy(x=>x.CategoryCode,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()))
            if(!currentDocs.Any(x=>x.CategoryCode.Equals(policy.CategoryCode,StringComparison.OrdinalIgnoreCase)&&(!x.ExpiryDate.HasValue||x.ExpiryDate>=DateOnly.FromDateTime(DateTime.UtcNow))))
                result.Add(new{id=$"missing-required:{row.TaskId:D}:{policy.CategoryCode}",identity=$"missing-required:{row.TaskId:D}:{policy.CategoryCode}",kind="EquipmentReference",documentId=Guid.Empty,scope="Equipment",scopeId=row.EquipmentId,title=$"Required document missing: {policy.CategoryCode}",category=policy.CategoryCode,revision="",fileName="",contentType="",sizeBytes=0L,sha256="",required=true,expired=false,missing=true,downloadUrl="",lastModified=(DateTimeOffset?)null});
        return result;
    }

    private static async Task<(string Sha256,long Size)> MeasureAsync(Stream stream,CancellationToken ct)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[81920];long size=0;
        while(true){var read=await stream.ReadAsync(buffer.AsMemory(),ct);if(read==0)break;hash.AppendData(buffer,0,read);size+=read;}
        return(Convert.ToHexString(hash.GetHashAndReset()),size);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(OfflineActivitySubmission request,CancellationToken ct)
    {
        if(!Enabled)return Conflict(new{error="Offline inspection is not enabled on this installation."});
        if(request.ClientActivityId==Guid.Empty||!Request.Headers.TryGetValue("Idempotency-Key",out var keys)||string.IsNullOrWhiteSpace(keys.FirstOrDefault())||keys[0]!.Length>100)return BadRequest(new{error="A stable client activity ID and Idempotency-Key header are required."});
        if(request.Checkpoints.Count==0||request.Checkpoints.Select(x=>x.ClientResponseId).Distinct().Count()!=request.Checkpoints.Count)return BadRequest(new{error="Submit unique checkpoint response IDs."});
        var idempotencyKey=keys[0]!;var payload=JsonSerializer.Serialize(request);var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var existing=await db.OfflineSyncReceipts.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==ActorId&&(x.IdempotencyKey==idempotencyKey||x.ClientActivityId==request.ClientActivityId),ct);
        if(existing is not null)return existing.PayloadHash==hash?Ok(new{accepted=true,duplicate=true,documentUsageRecorded=DocumentUsageEnabled||request.ReferenceDocumentsUsed is not {Count:>0},serverActivityId=existing.ServerActivityId,responseMap=JsonSerializer.Deserialize<Dictionary<Guid,Guid>>(existing.ResponseMapJson),acceptedAt=existing.AcceptedAt}):Conflict(new{error="The idempotency key or client activity ID was already used with different content."});

        var actor=await db.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==ActorId&&x.IsActive&&x.ApprovalStatus==ApprovalStatus.Approved&&x.Role==UserRole.Surveyor,ct);if(actor is null)return Forbid();
        var task=await db.InspectionTasks.SingleOrDefaultAsync(x=>x.Id==request.TaskId&&x.AssignedUserId==ActorId&&x.AreaEquipmentId.HasValue,ct);if(task is null)return NotFound();
        if(task.StatusCode is "Completed" or "Cancelled"||task.IsInaccessible)return Conflict(new{error="Task is no longer available for offline submission."});
        if(task.PlannedGrade.HasValue&&task.PlannedGrade.Value!=request.Grade)return Conflict(new{error="Submitted grade differs from the assigned work package."});
        var equipment=await db.AreaEquipment.AsNoTracking().SingleAsync(x=>x.Id==task.AreaEquipmentId,ct);
        var template=await db.ChecklistTemplates.SingleOrDefaultAsync(x=>x.Id==request.ChecklistTemplateId&&x.Version==request.ChecklistVersion&&x.InspectionType==request.Grade&&x.ApprovedAt!=null,ct);
        if(template is null||!ChecklistApplicability.TemplateMatches(template,request.Grade,equipment.ExProtectionType))return Conflict(new{error="The downloaded checklist version is missing, unapproved, or no longer matches equipment applicability."});
        var questions=await db.ChecklistQuestions.AsNoTracking().Where(x=>x.ChecklistTemplateId==template.Id).OrderBy(x=>x.SortOrder).ToListAsync(ct);var applicable=questions.Where(x=>ChecklistApplicability.QuestionMatches(x,equipment.ExProtectionType)).ToList();
        if(applicable.Count==0||request.Checkpoints.Any(x=>applicable.All(q=>q.Id!=x.QuestionId)))return Conflict(new{error="Checkpoint selection does not match the downloaded checklist."});
        var documentUses=request.ReferenceDocumentsUsed??[];if(documentUses.Count>100||documentUses.Select(x=>(x.DocumentIdentity,x.DocumentRevision)).Distinct().Count()!=documentUses.Count)return BadRequest(new{error="Submit no more than 100 unique document revision references."});
        foreach(var used in documentUses)
        {
            if(used.OpenedAt>DateTimeOffset.UtcNow.AddMinutes(5)||used.OpenedAt<request.StartedAt.AddMinutes(-5)||used.DocumentIdentity.Length>200||used.DocumentRevision.Length>100||used.Title.Length>255||used.Sha256.Length!=64||!used.Sha256.All(Uri.IsHexDigit))return BadRequest(new{error="Offline document usage metadata is invalid."});
            var belongs=used.Scope switch{"Project"=>used.ScopeId==equipment.ProjectId&&used.DocumentId==equipment.ProjectId&&used.DocumentIdentity.StartsWith($"project-plan:{equipment.ProjectId:D}:",StringComparison.Ordinal),"Area"=>used.ScopeId==equipment.AreaId&&used.DocumentIdentity.StartsWith($"area-photo:{used.DocumentId:D}:",StringComparison.Ordinal),_=>false};
            if(!belongs)return BadRequest(new{error="Reference document does not belong to the assigned task's project or area."});
        }
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction=null;if(db.Database.IsRelational())transaction=await db.Database.BeginTransactionAsync(ct);
        try
        {
            var snapshot=await db.ChecklistVersionSnapshots.SingleOrDefaultAsync(x=>x.SourceChecklistTemplateId==template.Id&&x.VersionNumber==template.Version&&x.ProtectionTypeSnapshot==equipment.ExProtectionType,ct);
            if(snapshot is null)
            {
                var hashInput=$"{template.Id:D}|{template.Version}|{template.StandardReference}|{template.StandardEdition}|{equipment.ExProtectionType}\n"+string.Join("\n",applicable.Select(x=>$"{x.Id:D}|{x.SectionCode}|{x.SortOrder}|{x.Text}|{x.IsRequired}|{x.ResponseScaleCode}|{x.RequireRemarkOnFail}|{x.NumericReadingEnabled}|{x.MeasurementUnit}|{x.MinimumReading}|{x.MaximumReading}"));
                snapshot=new ChecklistVersionSnapshot{SourceChecklistTemplateId=template.Id,VersionNumber=template.Version,InspectionType=template.InspectionType,SnapshotHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))),PublishedAt=template.ApprovedAt??DateTimeOffset.UtcNow,PublishedByUserId=template.ApprovedByUserId??ActorId,StandardReference=template.StandardReference,StandardEdition=template.StandardEdition,ControlledSourceReference=template.ControlledSourceReference,ProtectionTypeSnapshot=equipment.ExProtectionType};db.ChecklistVersionSnapshots.Add(snapshot);
                foreach(var q in applicable)db.CheckpointDefinitionSnapshots.Add(new CheckpointDefinitionSnapshot{ChecklistVersionSnapshotId=snapshot.Id,QuestionCode=q.Id.ToString("N"),QuestionVersion=template.Version,QuestionText=q.Text,SortOrder=q.SortOrder,SectionCode=q.SectionCode,ApplicabilityCode=q.ApplicableProtectionTypes??template.ApplicableProtectionTypes,IsRequired=q.IsRequired,ResponseScaleCode=q.ResponseScaleCode,RequireRemarkOnFail=q.RequireRemarkOnFail,NumericReadingEnabled=q.NumericReadingEnabled,NumericReadingRequired=q.NumericReadingRequired,MeasurementUnit=q.MeasurementUnit,MinimumReading=q.MinimumReading,MaximumReading=q.MaximumReading});
                await db.SaveChangesAsync(ct);
            }
            var created=await activities.CreateActivityAsync(task.Id,new CreateInspectionActivityRequest(request.Grade,snapshot.Id,request.StartedAt,null),ct);var map=new Dictionary<Guid,Guid>();
            // The in-memory provider does not generate SQL Server rowversion values. Seed a deterministic token only for isolated provider tests.
            if(!db.Database.IsRelational())
            {
                var activityRow=await db.InspectionActivities.SingleAsync(x=>x.Id==created.Id,ct);
                if(activityRow.RowVersion.Length==0){activityRow.RowVersion=[1];await db.SaveChangesAsync(ct);}
            }
            foreach(var checkpoint in request.Checkpoints)
            {
                var definition=await db.CheckpointDefinitionSnapshots.AsNoTracking().SingleOrDefaultAsync(x=>x.ChecklistVersionSnapshotId==snapshot.Id&&x.QuestionCode==checkpoint.QuestionId.ToString("N"),ct);if(definition is null)throw new InvalidOperationException("Downloaded checkpoint is no longer in the checklist snapshot.");
                var rowVersion=await db.InspectionActivities.AsNoTracking().Where(x=>x.Id==created.Id).Select(x=>x.RowVersion).SingleAsync(ct);
                var saved=await activities.SaveCheckpointResponseAsync(created.Id,new SaveCheckpointResponseRequest(definition.Id,checkpoint.ApplicabilityResultCode,checkpoint.ResponseCode,checkpoint.TechnicalConditionCode,checkpoint.Observation,Convert.ToBase64String(rowVersion),checkpoint.NumericValue),ct);map.Add(checkpoint.ClientResponseId,saved.Id);
            }
            foreach(var finding in request.Findings)
            {
                if(!map.TryGetValue(finding.ClientResponseId,out var responseId))throw new InvalidOperationException("Finding refers to a checkpoint response outside this submission.");
                await activities.AddFindingAsync(created.Id,new AddActivityFindingRequest(responseId,finding.Observation,finding.ProposedAction,finding.SeverityCode,finding.FaultCode,finding.RequiredMaterialsOrSupport,null,finding.DueAt,finding.ResponsibleParty),ct);
            }
            if(DocumentUsageEnabled)foreach(var used in documentUses.Where(x=>!x.Scope.Equals("Equipment",StringComparison.OrdinalIgnoreCase)))db.InspectionActivityDocumentUsages.Add(new InspectionActivityDocumentUsage{InspectionActivityId=created.Id,InspectionTaskId=task.Id,UserId=ActorId,DocumentIdentity=used.DocumentIdentity,DocumentRevision=used.DocumentRevision,Sha256=used.Sha256.ToUpperInvariant(),Title=used.Title,Scope=used.Scope,ScopeId=used.ScopeId,OpenedAt=used.OpenedAt});
            await activities.SubmitForReviewAsync(created.Id,ct);task.StatusCode="InProgress";
            var receipt=new OfflineSyncReceipt{UserId=ActorId,ClientActivityId=request.ClientActivityId,IdempotencyKey=idempotencyKey,PayloadHash=hash,ServerActivityId=created.Id,ResponseMapJson=JsonSerializer.Serialize(map)};db.OfflineSyncReceipts.Add(receipt);await db.SaveChangesAsync(ct);if(transaction is not null)await transaction.CommitAsync(ct);
            return Ok(new{accepted=true,duplicate=false,documentUsageRecorded=DocumentUsageEnabled||documentUses.Count==0,serverActivityId=created.Id,reviewStatus=InspectionActivityReviewStatus.Submitted,responseMap=map,acceptedAt=receipt.AcceptedAt});
        }
        catch {if(transaction is not null)await transaction.RollbackAsync(ct);throw;}
        finally {if(transaction is not null)await transaction.DisposeAsync();}
    }
}

internal sealed record OfflineTaskRow(Guid TaskId,Guid WorkPackageId,string PackageReference,string? PackageSite,string? PackageArea,string? Rotation,DateOnly? PlannedStart,DateOnly? PlannedEnd,Guid ProjectId,string ProjectName,string ProjectSite,Guid AreaId,string AreaName,Guid EquipmentId,string TagNumber,int SequenceNo,string Type,string? Location,string? Manufacturer,string? Model,string? SerialNumber,string? Description,string? ExProtectionType,string? GasGroup,string? TemperatureClass,string? HazardousAreaZone,string? IpRating,string? CertificateNumber,string? CircuitId,string? RfidTagUid,InspectionType? Grade,int? RouteOrder,string? PriorityCode,DateTimeOffset? ScheduledAt,DateTimeOffset? DueAt,string? RequiredPermits,string? IsolationPrerequisites,string StatusCode);

public sealed record OfflineActivitySubmission(Guid ClientActivityId,Guid TaskId,InspectionType Grade,Guid ChecklistTemplateId,int ChecklistVersion,DateTimeOffset StartedAt,List<OfflineCheckpointSubmission> Checkpoints,List<OfflineFindingSubmission> Findings,List<OfflineDocumentUsageSubmission>? ReferenceDocumentsUsed=null);
public sealed record OfflineCheckpointSubmission(Guid ClientResponseId,Guid QuestionId,string? ApplicabilityResultCode,string? ResponseCode,string? TechnicalConditionCode,string? Observation,decimal? NumericValue);
public sealed record OfflineFindingSubmission(Guid ClientResponseId,string Observation,string? ProposedAction,string? SeverityCode,string? FaultCode,string? RequiredMaterialsOrSupport,string? ResponsibleParty,DateTimeOffset? DueAt);
public sealed record OfflineDocumentUsageSubmission(string DocumentIdentity,Guid DocumentId,string DocumentRevision,string Sha256,string Title,string Scope,Guid ScopeId,DateTimeOffset OpenedAt);
