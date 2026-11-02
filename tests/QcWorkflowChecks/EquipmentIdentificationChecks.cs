using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

internal static class EquipmentIdentificationChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("Equipment identification check failed: "+message);Console.WriteLine("PASS: "+message);}
    public static async Task Run()
    {
        await using var db=new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var manager=new User{Email="tag-manager@check.invalid",DisplayName="Tag Manager",PasswordHash="test",Role=UserRole.Manager,ApprovalStatus=ApprovalStatus.Approved};
        var inspector=new User{Email="tag-inspector@check.invalid",DisplayName="Tag Inspector",PasswordHash="test",Role=UserRole.Surveyor,ApprovalStatus=ApprovalStatus.Approved};
        var otherManager=new User{Email="tag-other-manager@check.invalid",DisplayName="Other Manager",PasswordHash="test",Role=UserRole.Manager,ApprovalStatus=ApprovalStatus.Approved};db.Users.AddRange(manager,inspector,otherManager);
        var project=new Project{ProjectName="Authorized",JobNo="TAG-1",Sector="Test",Location="Test",VesselOrSiteName="Site",CreatedByUserId=manager.Id,AssignedSurveyorId=inspector.Id};
        var otherProject=new Project{ProjectName="Restricted",JobNo="TAG-2",Sector="Test",Location="Test",VesselOrSiteName="Site",CreatedByUserId=otherManager.Id};
        var area=new ProjectArea{ProjectId=project.Id,AreaName="Deck"};var otherArea=new ProjectArea{ProjectId=otherProject.Id,AreaName="Hold"};
        var equipment=new AreaEquipment{ProjectId=project.Id,AreaId=area.Id,SequenceNo=1,TagNumber="PUMP-01",Type="Motor",CreatedByUserId=manager.Id,RfidTagUid="04A1B2C3"};
        var hidden=new AreaEquipment{ProjectId=otherProject.Id,AreaId=otherArea.Id,SequenceNo=1,TagNumber="PUMP-02",Type="Motor",CreatedByUserId=otherManager.Id};
        db.Projects.AddRange(project,otherProject);db.ProjectAreas.AddRange(area,otherArea);db.AreaEquipment.AddRange(equipment,hidden);
        var qr=new EquipmentTagAssignment{EquipmentId=equipment.Id,ProjectId=project.Id,Technology="QR",NormalizedIdentifier="ASSET/PUMP-01",AssignedByUserId=manager.Id,AssignmentReason="Commissioned"};db.EquipmentTagAssignments.Add(qr);
        var hiddenQr=new EquipmentTagAssignment{EquipmentId=hidden.Id,ProjectId=otherProject.Id,Technology="QR",NormalizedIdentifier="ASSET/PUMP-02",AssignedByUserId=otherManager.Id,AssignmentReason="Commissioned"};db.EquipmentTagAssignments.Add(hiddenQr);
        await db.SaveChangesAsync();
        var surveyor=As(inspector);var managerApi=As(manager);var otherManagerApi=As(otherManager);
        Check(EquipmentIdentificationController.Normalize("04:a1-b2 c3","HF_RFID")=="04A1B2C3","RFID UID normalization removes separators and case differences");
        Check(await surveyor.Resolve("ASSET/PUMP-01","QR",CancellationToken.None) is OkObjectResult,"registered QR resolves to assigned equipment");
        Check(await surveyor.Resolve("04:a1:b2:c3","HF_RFID",CancellationToken.None) is OkObjectResult,"legacy RFID UID resolves to equipment");
        Check(await surveyor.Resolve("FFFFFFFF","HF_RFID",CancellationToken.None) is NotFoundObjectResult,"unknown RFID remains unresolved");
        db.EquipmentTagAssignments.Add(new EquipmentTagAssignment{EquipmentId=hidden.Id,ProjectId=otherProject.Id,Technology="QR",NormalizedIdentifier="DUPLICATE",AssignedByUserId=otherManager.Id,AssignmentReason="Fixture"});
        db.EquipmentTagAssignments.Add(new EquipmentTagAssignment{EquipmentId=equipment.Id,ProjectId=project.Id,Technology="QR",NormalizedIdentifier="DUPLICATE",AssignedByUserId=manager.Id,AssignmentReason="Fixture"});await db.SaveChangesAsync();
        Check(await surveyor.Resolve("DUPLICATE","QR",CancellationToken.None) is ConflictObjectResult,"duplicate active identifiers are not auto-selected");
        Check(await surveyor.Resolve("ASSET/PUMP-02","QR",CancellationToken.None) is NotFoundObjectResult,"cross-project equipment is concealed from surveyor");
        var inactive=new EquipmentTagAssignment{EquipmentId=equipment.Id,ProjectId=project.Id,Technology="NFC",NormalizedIdentifier="AABBCCDD",AssignedByUserId=manager.Id,AssignmentReason="Old tag",IsActive=false,DeactivatedAt=DateTimeOffset.UtcNow,DeactivatedByUserId=manager.Id,DeactivationReason="Damaged"};db.EquipmentTagAssignments.Add(inactive);await db.SaveChangesAsync();
        Check(await surveyor.Resolve("AABBCCDD","NFC",CancellationToken.None) is ObjectResult inactiveResult&&inactiveResult.StatusCode==410,"retired tag is identified as inactive");
        Check(await surveyor.Assign(equipment.Id,new("HF_RFID","11223344","test",false),CancellationToken.None) is ForbidResult,"surveyor cannot register or replace a tag");
        Check(await managerApi.Assign(hidden.Id,new("QR","OTHER","test"),CancellationToken.None) is NotFoundResult,"manager cannot register tags outside accessible project scope");
        var assigned=await managerApi.Assign(equipment.Id,new("NFC","A1:B2:C3:D4","New NFC tag"),CancellationToken.None);
        Check(assigned is OkObjectResult,"authorized manager registers a tag without creating equipment");
        var active=db.EquipmentTagAssignments.Single(x=>x.EquipmentId==equipment.Id&&x.Technology=="NFC"&&x.IsActive);
        Check(await managerApi.Assign(equipment.Id,new("NFC","01020304","Replacement"),CancellationToken.None) is ConflictObjectResult,"replacement requires explicit confirmation of current assignment");
        Check(await managerApi.Assign(equipment.Id,new("NFC","01020304","Replacement",true,active.Id.ToString("D")),CancellationToken.None) is OkObjectResult,"explicit confirmed tag replacement succeeds");
        Check(db.EquipmentTagHistory.Any(x=>x.EquipmentId==equipment.Id&&x.ActionCode=="Replaced"&&x.NormalizedIdentifier=="A1B2C3D4")&&db.EquipmentTagAssignments.Any(x=>x.EquipmentId==equipment.Id&&x.Technology=="NFC"&&x.NormalizedIdentifier=="A1B2C3D4"&&!x.IsActive),"replacement preserves prior identifier and audit history");
        var activeReplacement=db.EquipmentTagAssignments.Single(x=>x.EquipmentId==equipment.Id&&x.Technology=="NFC"&&x.IsActive);
        Check(await managerApi.Deactivate(equipment.Id,activeReplacement.Id,new("Retired after test"),CancellationToken.None) is OkObjectResult&&await managerApi.Resolve("01020304","NFC",CancellationToken.None) is ObjectResult retired&&retired.StatusCode==410,"authorized deactivation makes the former tag visibly inactive");
        Check(await otherManagerApi.Assign(equipment.Id,new("QR","ILLEGAL","unauthorized project"),CancellationToken.None) is NotFoundResult,"tag assignment cannot cross project access scope");
        Console.WriteLine("Equipment identification checks passed using synthetic EF InMemory data only.");

        EquipmentIdentificationController As(User user){var c=new EquipmentIdentificationController(db);c.ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Role,user.Role.ToString())],"test"))}};return c;}
    }
}
