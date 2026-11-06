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
        var valid = new BulkEquipmentRow(Guid.NewGuid(), "BULK-01", "ITEM-01", "Transfer pump", "Bulk fixture", "Pump", "12:34:56:78", "NFC", new Dictionary<string, string?> {["manufacturer"] = "Field manufacturer", ["model"] = "M-10"});
        var longUid = valid with { ClientId = Guid.NewGuid(), TagNumber = "BULK-02", EquipmentCode = null, RfidUid = new string('A', 128), Technology = "HF_RFID" };
        var mixed = new BulkEquipmentRequest(project.Id, area.Id, true, [
            valid,
            valid with { ClientId = Guid.NewGuid(), TagNumber = "DUPLICATE-UID", EquipmentCode = null, RfidUid = "12345678" },
            valid with { ClientId = Guid.NewGuid(), TagNumber = "bulk-01", EquipmentCode = null, RfidUid = "55667788" },
            valid with { ClientId = Guid.NewGuid(), TagNumber = "LEGACY-UID", EquipmentCode = null, RfidUid = "04-a1-b2-c3" },
            valid with { ClientId = Guid.NewGuid(), TagNumber = "INVALID-UID", EquipmentCode = null, RfidUid = "XYZ" },
            longUid
        ]);
        var batch = Json(await managerApi.BulkRegister(mixed, default));
        Check(batch.GetProperty("registered").GetInt32() == 2 && batch.GetProperty("failed").GetInt32() == 4,
            "bulk registration accepts valid rows and reports duplicate and invalid rows individually");
        Check(await db.AreaEquipment.AnyAsync(x => x.Id == valid.ClientId && x.ProjectId == project.Id && x.AreaId == area.Id &&
            !x.IsPlacedOnLayout && x.EquipmentName == "Transfer pump" && x.Manufacturer == "Field manufacturer" && x.Model == "M-10" && x.RegistrationStatus == "Submitted") &&
            await db.EquipmentTagHistory.AnyAsync(x => x.EquipmentId == valid.ClientId && x.NormalizedIdentifier == "12345678"),
            "bulk equipment keeps the chosen project, area, name, and independent review history");
        Check(await db.EquipmentTagAssignments.AnyAsync(x => x.EquipmentId == longUid.ClientId && x.NormalizedIdentifier.Length == 128),
            "full-length RFID UIDs are stored in the tag assignment");
        var retry = Json(await managerApi.BulkRegister(mixed with { Rows = [valid] }, default));
        Check(retry.GetProperty("registered").GetInt32() == 1 && await db.AreaEquipment.CountAsync(x => x.Id == valid.ClientId) == 1,
            "retrying a successful row returns the same equipment");
        var manual = valid with { ClientId = Guid.NewGuid(), TagNumber = "MANUAL-01", EquipmentCode = null, RfidUid = null };
        Check(Json(await managerApi.BulkRegister(mixed with { RequireRfid = false, Rows = [manual] }, default)).GetProperty("registered").GetInt32() == 1,
            "manual bulk registration accepts equipment without a UID");
        Check(await managerApi.BulkRegister(mixed with { AreaId = otherArea.Id }, default) is NotFoundObjectResult &&
            await otherManagerApi.BulkRegister(mixed, default) is NotFoundObjectResult,
            "bulk registration rejects a mismatched area or inaccessible project");
        var registrationProjects = Json(await managerApi.RegistrationProjects(default));
        Check(registrationProjects.GetArrayLength() == 1 && registrationProjects[0].GetProperty("id").GetGuid() == project.Id &&
            registrationProjects[0].GetProperty("areas")[0].GetProperty("id").GetGuid() == area.Id,
            "bulk project and area choices follow manager access");
        var fieldApi = new SurveyorController(db, null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<SurveyorController>.Instance)
            { ControllerContext = surveyor.ControllerContext };
        Check((await fieldApi.GetEquipmentByRfid("12:34:56:78", default)).Result is OkObjectResult,
            "the existing Surveyor RFID lookup finds bulk NFC registrations");
        var listing = Json(await managerApi.Identified(project.Id, null, default, 2, 2));
        Check(listing.GetProperty("total").GetInt32() == 3 && listing.GetProperty("items").GetArrayLength() == 1,
            "identified list includes only linked equipment, with stable pagination and one row per item");
        var filtered = Json(await surveyor.Identified(project.Id, "12:34:56:78", default));
        Check(filtered.GetProperty("items").GetArrayLength() == 1 && filtered.GetProperty("items")[0].GetProperty("equipmentId").GetGuid() == valid.ClientId &&
            Json(await otherManagerApi.Identified(project.Id, null, default)).GetProperty("total").GetInt32() == 0,
            "UID search and identified list respect equipment scope");
        Check(await managerApi.EditIdentification(valid.ClientId, new("PUMP-01", null, "Duplicate", null, "Pump"), default) is ConflictObjectResult &&
            await managerApi.EditIdentification(valid.ClientId, new("BULK-01", "ITEM-01", "Renamed pump", "Updated", "Pump"), default) is NoContentResult &&
            await db.EquipmentTagAssignments.AnyAsync(x => x.EquipmentId == valid.ClientId && x.IsActive && x.NormalizedIdentifier == "12345678"),
            "equipment edit validates duplicates and preserves the RFID assignment");

        static System.Text.Json.JsonElement Json(IActionResult result) =>
            System.Text.Json.JsonSerializer.SerializeToElement(((OkObjectResult)result).Value,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var invalidMetadata = valid with { ClientId = Guid.NewGuid(), TagNumber = "HAER-INVALID", EquipmentCode = null, RfidUid = null,
            HaerFields = new Dictionary<string, string?> { ["manufacturer"] = new string('X', 201) } };
        var invalidResult = Json(await managerApi.BulkRegister(mixed with { RequireRfid = false, Rows = [invalidMetadata] }, default));
        Check(invalidResult.GetProperty("results")[0].GetProperty("fieldErrors").TryGetProperty("manufacturer", out _) &&
            !await db.AreaEquipment.AnyAsync(x => x.Id == invalidMetadata.ClientId), "HAER bulk validation returns a field error without saving an invalid row");
        var edit = await managerApi.EditIdentification(valid.ClientId,
            new("BULK-01", "ITEM-01", "Renamed pump", "Updated", "Pump", new Dictionary<string, string?> {["manufacturer"] = "Updated manufacturer"}), default);
        Check(edit is NoContentResult &&
            await db.AreaEquipment.AnyAsync(x => x.Id == valid.ClientId && x.Manufacturer == "Updated manufacturer" && x.Model == "M-10" && x.AreaId == area.Id),
            "HAER edits persist technical fields without changing untouched metadata or the area");
        var register = new HaerController(db) {ControllerContext = managerApi.ControllerContext};
        var registerData = Json(await register.Register(project.Id, default));
        Check(registerData.GetProperty("equipment").EnumerateArray().Any(x => x.GetProperty("equipmentRecordId").GetGuid() == valid.ClientId &&
            x.GetProperty("areaId").GetGuid() == area.Id && x.GetProperty("manufacturer").GetString() == "Updated manufacturer"),
            "HAER register returns shared equipment fields and document identifiers");
        Check(await register.Register(otherProject.Id, default) is NotFoundResult, "HAER register preserves project authorization");
        (await db.Projects.SingleAsync(p => p.Id == project.Id)).ClientName = "HAER Client Details";
        await db.SaveChangesAsync();
        var surveyorProjects = Json(await surveyor.RegistrationProjects(default));
        Check(surveyorProjects[0].GetProperty("clientName").GetString() == "HAER Client Details",
            "HAER project choices return the Client Details name from the same project");
        Check(surveyorProjects.GetArrayLength() == 1 && surveyorProjects[0].GetProperty("id").GetGuid() == project.Id,
            "Surveyor HAER project choices contain only assigned projects");
        var surveyorRegister = new HaerController(db) {ControllerContext = surveyor.ControllerContext};
        Check(await surveyorRegister.Register(project.Id, default) is OkObjectResult &&
            await surveyorRegister.Register(otherProject.Id, default) is NotFoundResult,
            "Surveyor HAER read access follows project assignment");
        var projectApi = new ProjectController(db, null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectController>.Instance)
            {ControllerContext = surveyor.ControllerContext};
        Check((await projectApi.CreateArea(otherProject.Id, new("Forbidden area"), default)).Result is NotFoundObjectResult &&
            (await projectApi.CreateArea(project.Id, new("Surveyor area"), default)).Result is ObjectResult {StatusCode: 201},
            "Surveyor area creation is limited to assigned projects");
        var surveyorAreaId = await db.ProjectAreas.Where(a => a.ProjectId == project.Id && a.AreaName == "Surveyor area").Select(a => a.Id).SingleAsync();
        var surveyorRow = manual with {ClientId = Guid.NewGuid(), TagNumber = "SURVEYOR-HAER"};
        var surveyorBatch = new BulkEquipmentRequest(project.Id, surveyorAreaId, false, [surveyorRow]);
        Check(await surveyor.BulkRegister(surveyorBatch with {ProjectId = otherProject.Id, AreaId = otherArea.Id}, default) is NotFoundObjectResult &&
            await surveyor.BulkRegister(surveyorBatch with {AreaId = otherArea.Id}, default) is NotFoundObjectResult &&
            Json(await surveyor.BulkRegister(surveyorBatch, default)).GetProperty("registered").GetInt32() == 1 &&
            await db.AreaEquipment.AnyAsync(e => e.Id == surveyorRow.ClientId && e.AreaId == surveyorAreaId &&
                e.CreatedByUserId == inspector.Id && e.RegistrationStatus == "Submitted"),
            "Surveyor can submit equipment to the new assigned area and cannot cross project boundaries");
        Console.WriteLine("Equipment identification checks passed using synthetic EF InMemory data only.");

        EquipmentIdentificationController As(User user){var c=new EquipmentIdentificationController(db);c.ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Role,user.Role.ToString())],"test"))}};return c;}
    }
}
