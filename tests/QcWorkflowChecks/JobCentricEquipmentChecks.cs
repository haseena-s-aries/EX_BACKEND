using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class JobCentricEquipmentChecks
{
    public static async Task Run()
    {
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var inspector = new User { Email = "inspector@test.invalid", DisplayName = "Inspector", PasswordHash = "test", Role = UserRole.Surveyor, ApprovalStatus = ApprovalStatus.Approved };
        var otherInspector = new User { Email = "other@test.invalid", DisplayName = "Other", PasswordHash = "test", Role = UserRole.Surveyor, ApprovalStatus = ApprovalStatus.Approved };
        var admin = new User { Email = "admin@test.invalid", DisplayName = "Admin", PasswordHash = "test", Role = UserRole.Admin, ApprovalStatus = ApprovalStatus.Approved };
        db.Users.AddRange(inspector, otherInspector, admin);
        var project = new Project { ProjectName = "Assigned job", JobNo = "JOB-JC-1", Sector = "Marine", Location = "Site", VesselOrSiteName = "Vessel", CreatedByUserId = inspector.Id, AssignedSurveyorId = inspector.Id, AssignedQcId = inspector.Id };
        var area = new ProjectArea { ProjectId = project.Id, AreaName = "Deck" };
        var otherProject = new Project { ProjectName = "Other job", JobNo = "JOB-JC-2", Sector = "Marine", Location = "Site", VesselOrSiteName = "Vessel", CreatedByUserId = otherInspector.Id, AssignedSurveyorId = otherInspector.Id, AssignedQcId = inspector.Id };
        var otherArea = new ProjectArea { ProjectId = otherProject.Id, AreaName = "Deck" };
        var otherEquipment = new AreaEquipment { ProjectId = otherProject.Id, AreaId = otherArea.Id, SequenceNo = 1, TagNumber = "OTHER-001", Type = "Motor", RfidTagUid = "11223344", CreatedByUserId = otherInspector.Id };
        db.Projects.AddRange(project, otherProject); db.ProjectAreas.AddRange(area, otherArea); db.AreaEquipment.Add(otherEquipment);
        await db.SaveChangesAsync();
        var storage = new FakeProjectPhotos();
        var controller = new SurveyorController(db, storage, NullLogger<SurveyorController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, inspector.Id.ToString()), new Claim(ClaimTypes.Role, nameof(UserRole.Surveyor))], "test")) } }
        };

        var unknown = await controller.ResolveEquipmentInArea(project.Id, area.Id, "AABBCCDD", "HF_RFID");
        Check(unknown is NotFoundObjectResult { Value: not null }, "Unknown tag is reported as not found within the selected job");
        var eventId = Guid.NewGuid();
        var scanEvent = new RecordEquipmentScanEventRequest(eventId, "AABBCCDD", "HF_RFID", "unknown");
        var recorded = await controller.RecordEquipmentScanEvent(project.Id, area.Id, scanEvent, CancellationToken.None);
        Check(recorded is ObjectResult { StatusCode: StatusCodes.Status201Created } && db.EquipmentScanEvents.Count() == 1, "Unknown identifier scan is recorded without creating equipment");
        var retry = await controller.RecordEquipmentScanEvent(project.Id, area.Id, scanEvent, CancellationToken.None);
        Check(retry is OkObjectResult { Value: not null } && db.EquipmentScanEvents.Count() == 1, "Retry with the client event ID is idempotent");

        var localEquipmentId = Guid.NewGuid();
        var registrationRequest = new UpsertAreaEquipmentRequest(
            0, 0, "NEW-001", null, "Motor", null, "Deck 1", "Verified maker", null, null,
            "AA:BB:CC:DD", IsPlacedOnLayout: false, Description: "New field item", Model: "M-1",
            SerialNumber: "S-1", ExProtectionType: "Ex d", GasGroup: "IIB", TemperatureClass: "T4",
            HazardousAreaZone: null, AmbientTemperatureRange: null, IpRating: "IP66", NotifiedBody: null,
            SpecialConditionsOfUse: null, RfidIdentificationMethod: "HF_RFID", ClientEquipmentId: localEquipmentId);
        var created = await controller.CreateEquipment(project.Id, area.Id, registrationRequest, CancellationToken.None);
        Check(created.Result is ObjectResult { StatusCode: StatusCodes.Status201Created }, "Inspector can register new equipment without a layout position");
        var equipment = await db.AreaEquipment.SingleAsync(x => x.ProjectId == project.Id);
        Check(equipment.Id == localEquipmentId, "Client-generated equipment ID is preserved by the server for offline reconciliation");
        var registrationRetry = await controller.CreateEquipment(project.Id, area.Id, registrationRequest, CancellationToken.None);
        Check(registrationRetry.Result is OkObjectResult && await db.AreaEquipment.CountAsync(x => x.ProjectId == project.Id) == 1,
            "Retry with the same client equipment ID returns the existing equipment instead of duplicating it");
        Check(!equipment.IsPlacedOnLayout && equipment.XPercent == 0 && equipment.YPercent == 0, "Unplaced registration is represented separately from a real layout pin");
        Check(equipment.Description == "New field item" && equipment.Model == "M-1" && equipment.ExProtectionType == "Ex d", "Supported nameplate and Ex fields persist through the equipment request");
        Check(await db.EquipmentTagAssignments.AnyAsync(x => x.EquipmentId == equipment.Id && x.Technology == "HF_RFID" && x.NormalizedIdentifier == "AABBCCDD"), "Initial RFID assignment is stored in tag history on explicit registration");
        var photo = new FormFile(new MemoryStream([0x89, 0x50, 0x4e, 0x47]), 0, 4, "Files", "plate.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        var clientPhotoId = Guid.NewGuid();
        var uploaded = await controller.UploadEquipmentPhotos(project.Id, area.Id, equipment.Id,
            new UploadEquipmentPhotosRequest { Role = "Nameplate", Files = [photo], ClientPhotoId = clientPhotoId }, CancellationToken.None);
        Check(uploaded is ObjectResult { StatusCode: StatusCodes.Status201Created } &&
              await db.Files.AnyAsync(x => x.ProjectId == project.Id && x.AreaEquipmentId == equipment.Id && x.EquipmentPhotoRole == "Nameplate"),
            "Nameplate photo is associated with the existing project and equipment IDs");
        var photoRetry = new FormFile(new MemoryStream([0x89, 0x50, 0x4e, 0x47]), 0, 4, "Files", "plate.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        Check(await controller.UploadEquipmentPhotos(project.Id, area.Id, equipment.Id,
                  new UploadEquipmentPhotosRequest { Role = "Nameplate", Files = [photoRetry], ClientPhotoId = clientPhotoId }, CancellationToken.None) is OkObjectResult &&
              await db.Files.CountAsync(x => x.AreaEquipmentId == equipment.Id) == 1,
            "Equipment evidence retry with the same client photo ID is idempotent");
        var changedPhoto = new FormFile(new MemoryStream([0x89, 0x50, 0x4e, 0x47, 0x01]), 0, 5, "Files", "plate.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        Check(await controller.UploadEquipmentPhotos(project.Id, area.Id, equipment.Id,
                  new UploadEquipmentPhotosRequest { Role = "Nameplate", Files = [changedPhoto], ClientPhotoId = clientPhotoId }, CancellationToken.None) is ConflictObjectResult,
            "Reusing an equipment photo ID with different bytes is rejected");
        var listed = await controller.GetEquipmentPhotos(project.Id, area.Id, equipment.Id, CancellationToken.None);
        Check(listed is OkObjectResult { Value: not null }, "Authorized inspector can retrieve equipment photo metadata");
        var photoId = await db.Files.Where(x => x.AreaEquipmentId == equipment.Id).Select(x => x.Id).SingleAsync();
        Check(await controller.DownloadEquipmentPhoto(project.Id, area.Id, equipment.Id, photoId, CancellationToken.None) is FileStreamResult,
            "Equipment photograph can be retrieved through its job-scoped route");
        var deniedPhoto = await controller.DownloadEquipmentPhoto(otherProject.Id, otherArea.Id, equipment.Id, photoId, CancellationToken.None);
        Check(deniedPhoto is ObjectResult { StatusCode: StatusCodes.Status403Forbidden }, "Equipment photo access is denied outside the assigned job");
        var conflict = await controller.RecordEquipmentScanEvent(project.Id, area.Id, scanEvent with { Identifier = "11223344" }, CancellationToken.None);
        Check(conflict is ConflictObjectResult, "Reusing a scan event ID for a different identifier is rejected");

        var reviewer = new EquipmentIdentificationController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim(ClaimTypes.Role, nameof(UserRole.Admin))], "test")) } }
        };
        equipment.CreatedByUserId = admin.Id;
        Check(await reviewer.ReviewRegistration(equipment.Id, new ReviewEquipmentRegistrationRequest("Verified"), CancellationToken.None) is ConflictObjectResult,
            "Equipment registrant cannot independently verify their own registration");
        equipment.CreatedByUserId = inspector.Id;
        var registrationReview = await reviewer.ReviewRegistration(equipment.Id,
            new ReviewEquipmentRegistrationRequest("Verified", "Checked against the available nameplate."), CancellationToken.None);
        Check(registrationReview is OkObjectResult && equipment.RegistrationStatus == "Verified" &&
              db.AuditLogs.Any(x => x.EntityId == equipment.Id && x.Action == "Equipment Registration Reviewed"),
            "Independent equipment verification updates its own audited lifecycle");

        var matched = await controller.ResolveEquipmentInArea(project.Id, area.Id, "AA:BB:CC:DD", "HF_RFID");
        Check(matched is OkObjectResult { Value: not null }, "Known equipment resolves inside its assigned job area");
        var crossJob = await controller.ResolveEquipmentInArea(project.Id, area.Id, "11223344", "HF_RFID");
        Check(crossJob is NotFoundObjectResult, "A tag belonging to another job is not disclosed by the current job lookup");
        var denied = await controller.ResolveEquipmentInArea(otherProject.Id, otherArea.Id, "11223344", "HF_RFID");
        Check(denied is ObjectResult { StatusCode: StatusCodes.Status403Forbidden }, "A surveyor cannot query an unassigned job");
        Console.WriteLine("Job-centric equipment checks passed.");
    }

    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }

    private sealed class FakeProjectPhotos : IProjectPhotoStorage
    {
        private readonly Dictionary<string, byte[]> files = new();
        public int MaxFilesPerRequest => 20;
        public Task<StoredProjectPhoto> SaveHazardousAreaPlanAsync(IFormFile file, Guid projectId, CancellationToken ct) => throw new NotSupportedException();
        public async Task<StoredProjectPhoto> SaveAsync(IFormFile file, Guid projectId, Guid areaId, CancellationToken ct)
        {
            var path = $"{projectId:N}/{areaId:N}/{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}";
            using var memory = new MemoryStream(); await file.CopyToAsync(memory, ct); files[path] = memory.ToArray();
            return new StoredProjectPhoto(Path.GetFileName(file.FileName), path);
        }
        public Task<StoredProjectPhoto> SaveInspectionAsync(IFormFile file, Guid projectId, Guid inspectionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string path, CancellationToken ct = default) => Task.FromResult(files.TryGetValue(path, out var data) ? new StoredProjectPhotoContent(new MemoryStream(data), "image/png") : null);
        public Task DeleteAsync(string path, CancellationToken ct = default) {files.Remove(path); return Task.CompletedTask;}
    }
}
