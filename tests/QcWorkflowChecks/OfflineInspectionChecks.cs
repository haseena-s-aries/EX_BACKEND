using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class OfflineInspectionChecks
{
    public static async Task Run()
    {
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var surveyor = new User { Email="offline-surveyor@test.invalid",DisplayName="Offline Surveyor",PasswordHash="test",Role=UserRole.Surveyor,ApprovalStatus=ApprovalStatus.Approved };
        var other = new User { Email="offline-other@test.invalid",DisplayName="Other Surveyor",PasswordHash="test",Role=UserRole.Surveyor,ApprovalStatus=ApprovalStatus.Approved };
        var qc = new User { Email="offline-qc@test.invalid",DisplayName="Offline QC",PasswordHash="test",Role=UserRole.Qc,ApprovalStatus=ApprovalStatus.Approved };
        db.Users.AddRange(surveyor,other,qc);
        var project=new Project{ProjectName="Offline test",JobNo="OFFLINE-1",Sector="Test",Location="Test",VesselOrSiteName="Test",CreatedByUserId=surveyor.Id,AssignedSurveyorId=surveyor.Id,AssignedQcId=qc.Id,HazardousAreaPlanPath="synthetic-plan.png",HazardousAreaPlanFileName="plan.png",Revision="R1"};
        var area=new ProjectArea{ProjectId=project.Id,AreaName="Offline Area"};var equipment=new AreaEquipment{ProjectId=project.Id,AreaId=area.Id,TagNumber="OFF-1",Type="Motor",CreatedByUserId=surveyor.Id};
        var plan=new InspectionPlan{ProjectId=project.Id,Name="Offline plan"};
        var campaign=new InspectionCampaign{ProjectId=project.Id,Name="Offline campaign",Reference="OFF-CAM",StartsOn=new DateOnly(2026,1,1),EndsOn=new DateOnly(2026,12,31),CreatedByUserId=surveyor.Id};
        var package=new InspectionWorkPackage{CampaignId=campaign.Id,ProjectId=project.Id,Reference="OFF-WP",CreatedByUserId=surveyor.Id};
        var task=new InspectionTask{InspectionPlanId=plan.Id,AreaEquipmentId=equipment.Id,AssignedUserId=surveyor.Id,WorkPackageId=package.Id,WorkPackageReference=package.Reference,PlannedGrade=InspectionType.Visual,StatusCode="Assigned"};
        var template=new ChecklistTemplate{Name="Published Visual",InspectionType=InspectionType.Visual,Version=1,IsActive=true,LifecycleStatus=ChecklistLifecycleStatus.Published,ApprovedAt=DateTimeOffset.UtcNow,ApprovedByUserId=qc.Id};
        var question=new ChecklistQuestion{ChecklistTemplateId=template.Id,Text="Inspect fixture",SortOrder=1,IsRequired=true};
        db.Projects.Add(project);db.ProjectAreas.Add(area);db.AreaEquipment.Add(equipment);db.InspectionPlans.Add(plan);db.InspectionCampaigns.Add(campaign);db.InspectionWorkPackages.Add(package);db.InspectionTasks.Add(task);db.ChecklistTemplates.Add(template);db.ChecklistQuestions.Add(question);await db.SaveChangesAsync();

        var actor=new TestActor(surveyor.Id);var ownership=new InspectionTenantOwnershipResolver(db);
        var authorizer=new InspectionActivityAuthorizer(db,ownership,new TenantScopeResolver(db),new NoTenantContext(),actor,Options.Create(new TenantFoundationOptions{Enabled=false}));
        var activityOptions=Options.Create(new InspectionActivityOptions{Enabled=true,DocumentUsageEnabled=true});var service=new InspectionActivityApplicationService(db,authorizer,actor,ownership,new CorrectiveActionPolicy(),activityOptions,TimeProvider.System);
        var controller=new OfflineInspectionsController(db,service,Options.Create(new OfflineInspectionOptions{Enabled=true}),new FakePhotos(),activityOptions,Options.Create(new EquipmentDocumentOptions{Enabled=false}));
        SetActor(controller,surveyor.Id);controller.Request.Headers["Idempotency-Key"]="client-activity-1";
        var manifest=(OkObjectResult)await controller.Packages(CancellationToken.None);var packageArray=(System.Collections.IEnumerable)manifest.Value!.GetType().GetProperty("packages")!.GetValue(manifest.Value)!;var packageItem=packageArray.Cast<object>().Single();var taskArray=(System.Collections.IEnumerable)packageItem.GetType().GetProperty("tasks")!.GetValue(packageItem)!;var taskItem=taskArray.Cast<object>().Single();var documents=(System.Collections.IEnumerable)taskItem.GetType().GetProperty("documents")!.GetValue(taskItem)!;var doc=documents.Cast<object>().Single();Ensure((bool)doc.GetType().GetProperty("required")!.GetValue(doc)!&&doc.GetType().GetProperty("sha256")!.GetValue(doc)!.ToString()!.Length==64,"assigned task manifest includes the existing project plan with SHA-256 integrity metadata");
        actor.UserId=other.Id;SetActor(controller,other.Id);Ensure(await controller.DownloadTaskDocument(task.Id,"ProjectPlan",project.Id,CancellationToken.None) is NotFoundResult,"unassigned surveyor cannot download a task reference document");actor.UserId=surveyor.Id;SetActor(controller,surveyor.Id);Ensure(await controller.DownloadTaskDocument(task.Id,"ProjectPlan",project.Id,CancellationToken.None) is FileStreamResult,"assigned surveyor can retrieve the authorized project reference document");
        controller.Request.Headers["Idempotency-Key"]="client-activity-1";
        var openedAt=DateTimeOffset.UtcNow;var request=new OfflineActivitySubmission(Guid.NewGuid(),task.Id,InspectionType.Visual,template.Id,template.Version,openedAt,[new(Guid.NewGuid(),question.Id,null,"Pass",null,"Checked",null)],[],[new($"project-plan:{project.Id:D}:R1",project.Id,"R1",new string('A',64),"plan.png","Project",project.Id,openedAt)]);
        var first=await controller.Sync(request,CancellationToken.None);
        Ensure(first is OkObjectResult && db.OfflineSyncReceipts.Count()==1 && db.InspectionActivities.Count()==1&&db.InspectionActivityDocumentUsages.Count()==1,"offline sync persists an activity, idempotency receipt and the document revision opened during inspection");
        var duplicate=await controller.Sync(request,CancellationToken.None);
        Ensure(duplicate is OkObjectResult duplicateOk && (bool)duplicateOk.Value!.GetType().GetProperty("duplicate")!.GetValue(duplicateOk.Value)! && db.InspectionActivities.Count()==1,"same idempotency payload returns the prior acknowledgment without duplication");
        var conflictRequest=request with { Checkpoints=[new(request.Checkpoints[0].ClientResponseId,question.Id,null,"Fail",null,"Changed",null)] };
        Ensure(await controller.Sync(conflictRequest,CancellationToken.None) is ConflictObjectResult,"reused idempotency key with changed content conflicts");
        var invalidDocumentRequest=request with {ClientActivityId=Guid.NewGuid(),ReferenceDocumentsUsed=[request.ReferenceDocumentsUsed![0] with {ScopeId=Guid.NewGuid()}]};controller.Request.Headers["Idempotency-Key"]="invalid-doc-scope";Ensure(await controller.Sync(invalidDocumentRequest,CancellationToken.None) is BadRequestObjectResult&&db.InspectionActivities.Count()==1,"offline sync rejects a document reference outside the assigned project before creating the activity");
        actor.UserId=other.Id;SetActor(controller,other.Id);controller.Request.Headers["Idempotency-Key"]="other-user-key";
        Ensure(await controller.Sync(request with {ClientActivityId=Guid.NewGuid()},CancellationToken.None) is NotFoundResult,"another surveyor cannot synchronize an unassigned task");
        Console.WriteLine("Offline inspection API checks passed: synthetic EF InMemory only; no persisted SQL isolation established.");
    }
    private static void SetActor(ControllerBase controller,Guid id)=>controller.ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim(ClaimTypes.Role,"Surveyor")],"test"))}};
    private static void Ensure(bool passed,string message){if(!passed)throw new InvalidOperationException("Offline inspection check failed: "+message);Console.WriteLine("PASS: "+message);}
    private sealed class TestActor(Guid id):IInspectionActivityActorContext{public Guid? UserId{get;set;}=id;}
    private sealed class NoTenantContext:ITenantContextAccessor{public AuthorizedTenantContext? Current=>null;}
}
