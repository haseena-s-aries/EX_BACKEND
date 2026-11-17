using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ReportWorkflowChecks
{
    static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static JsonElement Json(IActionResult result) => JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    public static async Task Run()
    {
        var options = new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new SurveyDbContext(options);
        var manager = new User { Email = "report-manager@example.invalid", DisplayName = "Report Manager", PasswordHash = "test", Role = UserRole.Manager };
        var surveyor = new User { Email = "report-surveyor@example.invalid", DisplayName = "Report Surveyor", PasswordHash = "test", Role = UserRole.Surveyor };
        var project = new Project { ProjectName = "Report lifecycle fixture", JobNo = "WORKFLOW", Sector = "Test", Location = "Test yard", VesselOrSiteName = "Test vessel", CreatedByUserId = manager.Id, AssignedSurveyorId = surveyor.Id, AssignedQcId = manager.Id, AssignedClientId = manager.Id, Status = ProjectStatus.InProgress };
        var area = new ProjectArea { ProjectId = project.Id, AreaName = "Deck" };
        var equipment = new AreaEquipment { ProjectId = project.Id, AreaId = area.Id, SequenceNo = 1, TagNumber = "EQ-1", Type = "Motor", CreatedByUserId = surveyor.Id };
        var session = new SurveySession { AreaEquipmentId = equipment.Id, SurveyorId = surveyor.Id, Status = SurveyStatus.Submitted, SubmittedAt = DateTimeOffset.UtcNow, InspectionTimestamp = DateTimeOffset.UtcNow };
        db.AddRange(manager, surveyor, project, area, equipment, session); await db.SaveChangesAsync();
        ClaimsPrincipal Principal(string role, Guid id) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role)], "synthetic"));
        var photos = new Photos(); var archive = new Archive();
        ReportWorkflowController Controller(SurveyDbContext context, string role = "Surveyor", Guid? id = null) => new(context, photos, archive, NullLogger<ReportWorkflowController>.Instance) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(role, id ?? surveyor.Id) } } };
        ReportsController Reports(string role = "Surveyor", Guid? id = null) => new(db, photos, NullLogger<ReportsController>.Instance, archive) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(role, id ?? surveyor.Id) } } };
        var controller = Controller(db);
        var initial = Json(await controller.Get(project.Id, default));
        Check(initial.GetProperty("status").GetString() == "Ongoing" && initial.GetProperty("version").GetInt64() == 0 && !await db.ProjectReportWorkflows.AnyAsync(), "New and legacy projects read as Ongoing without changing records on GET");
        await Reports("Manager", manager.Id).Pdf(project.Id, archive, default);
        Check((await db.ProjectReportWorkflows.SingleAsync()).Status == "Ongoing" && (await db.ProjectReportWorkflows.SingleAsync()).Version == 0, "First generated report persists Ongoing without a submission transition");
        Check(await controller.Change(project.Id, new("Hold", 0, "   "), default) is BadRequestObjectResult && !await db.ReportWorkflowEvents.AnyAsync(), "Hold requires a reason and invalid requests leave no audit or status change");
        Check(await controller.Change(project.Id, new("Closed", 0), default) is BadRequestObjectResult, "Only the three report statuses are accepted");
        var held = Json(await controller.Change(project.Id, new("Hold", 0, "Waiting for drawings"), default));
        Check(held.GetProperty("status").GetString() == "Hold" && held.GetProperty("holdReason").GetString() == "Waiting for drawings", "Hold reason and actor are saved independently of Project Status");
        Check(await controller.Change(project.Id, new("Submitted", 1, ConfirmSubmission: true), default) is BadRequestObjectResult, "A held report must be resumed before submission");
        Check(await controller.Change(project.Id, new("Ongoing", 0), default) is ConflictObjectResult, "Stale status changes cannot overwrite a newer version");
        var resumed = Json(await controller.Change(project.Id, new("Ongoing", 1), default));
        Check(resumed.GetProperty("status").GetString() == "Ongoing" && resumed.GetProperty("holdReason").ValueKind == JsonValueKind.Null && resumed.GetProperty("history").GetArrayLength() == 2, "Resume clears the current hold while preserving its historical reason");
        Check(await controller.Change(project.Id, new("Submitted", 2), default) is BadRequestObjectResult, "Final submission requires explicit confirmation");
        Check(await controller.Change(project.Id, new("Submitted", 2, ConfirmSubmission: true), default) is BadRequestObjectResult, "Submitting without a selected draft project PDF is rejected");
        await Reports("Manager", manager.Id).Pdf(project.Id, archive, default);
        var missingDetails = await db.GeneratedReportArtifacts.OrderByDescending(a => a.Revision).FirstAsync();
        Check(await controller.Change(project.Id, new("Submitted", 2, ConfirmSubmission: true, DraftArtifactId: missingDetails.Id), default) is BadRequestObjectResult, "Report document details must be present before submission");
        await Reports().GenerateSurveyorPdf(project.Id, new("UID-1", "DOC-1", "A"), archive, default);
        var draft = await db.GeneratedReportArtifacts.OrderByDescending(a => a.Revision).FirstAsync();
        Check((await db.ProjectReportWorkflows.SingleAsync()).Status == "Ongoing", "Generating the PDF never automatically submits the report");
        Check(await controller.Change(project.Id, new("Submitted", 2, ConfirmSubmission: true, DraftArtifactId: draft.Id), default) is BadRequestObjectResult, "Pending QC prevents submission without changing QC or inspection data");
        db.QcReviews.Add(new QcReview { SurveySessionId = session.Id, ReviewedByUserId = manager.Id, Status = QcReviewStatus.Approved, ReviewedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync();
        var submitted = Json(await controller.Change(project.Id, new("Submitted", 2, ConfirmSubmission: true, DraftArtifactId: draft.Id), default));
        var finalId = submitted.GetProperty("submittedArtifactId").GetGuid();
        var final = await db.GeneratedReportArtifacts.SingleAsync(a => a.Id == finalId);
        var originalBytes = archive.Files[final.StorageKey].ToArray();
        Check(submitted.GetProperty("status").GetString() == "Submitted" && submitted.GetProperty("submittedByUserId").GetGuid() == surveyor.Id && submitted.GetProperty("submittedAt").ValueKind == JsonValueKind.String && final.ApprovalState == "Submitted", "Explicit submission stores its date, user and immutable final artifact");
        Check(draft.ApprovalState == "Draft" && draft.Id != final.Id && project.Status == ProjectStatus.InProgress && session.Status == SurveyStatus.Submitted && (await db.QcReviews.SingleAsync()).Status == QcReviewStatus.Approved, "Final snapshot preserves earlier drafts and all independent status fields");
        Check(await controller.Change(project.Id, new("Ongoing", 3), default) is ConflictObjectResult && await controller.Reopen(project.Id, new(3, "Change requested"), default) is ForbidResult, "Submitted reports cannot be silently changed or reopened by Surveyors");
        Check(await Reports().GenerateSurveyorPdf(project.Id, new("CHANGED", "CHANGED", "B"), archive, default) is ConflictObjectResult, "Backend blocks document metadata changes while submitted");
        project.ProjectName = "Underlying project changed later"; await db.SaveChangesAsync();
        var preview = (FileContentResult)await Reports().PreviewPdf(project.Id, default);
        Check(preview.FileContents.SequenceEqual(originalBytes) && final.Sha256 == Convert.ToHexString(SHA256.HashData(originalBytes)), "Submitted previews use frozen bytes, even if underlying project data later changes");
        var archiveCount = await db.GeneratedReportArtifacts.CountAsync();
        var download = (FileContentResult)await Reports("Manager", manager.Id).Pdf(project.Id, archive, default);
        Check(download.FileContents.SequenceEqual(originalBytes) && await db.GeneratedReportArtifacts.CountAsync() == archiveCount, "Downloading the submitted report returns its preserved version without generating another report");
        Check(await Controller(db, "Qc", manager.Id).Change(project.Id, new("Hold", 3, "Test"), default) is ForbidResult && await Controller(db, "Client", manager.Id).Change(project.Id, new("Hold", 3, "Test"), default) is ForbidResult, "QC and Client have read-only Report Status");
        Check(await Controller(db, "Surveyor", Guid.NewGuid()).Get(project.Id, default) is NotFoundResult && await Controller(db, "Manager", Guid.NewGuid()).Change(project.Id, new("Hold", 3, "Test"), default) is NotFoundResult, "Project assignment scope protects workflow reads and writes");
        var managerController = Controller(db, "Manager", manager.Id);
        Check(await managerController.Reopen(project.Id, new(3, " "), default) is BadRequestObjectResult, "Reopening requires an explicit revision reason");
        var reopened = Json(await managerController.Reopen(project.Id, new(3, "Client requested a document update"), default));
        Check(reopened.GetProperty("status").GetString() == "Ongoing" && reopened.GetProperty("revision").GetInt32() == 2 && reopened.GetProperty("submittedArtifactId").GetGuid() == finalId && archive.Files[final.StorageKey].SequenceEqual(originalBytes), "Authorized reopening starts a new revision and preserves the submitted version");
        Check(await controller.Change(project.Id, new("Submitted", 4, ConfirmSubmission: true, DraftArtifactId: draft.Id), default) is BadRequestObjectResult, "A reopened revision cannot reuse a pre-submission draft");
        await using var otherContext = new SurveyDbContext(options);
        var concurrent = await Task.WhenAll(controller.Change(project.Id, new("Hold", 4, "First request"), default), Controller(otherContext).Change(project.Id, new("Hold", 4, "Second request"), default));
        Check(concurrent.Count(r => r is OkObjectResult) == 1 && concurrent.Count(r => r is ConflictObjectResult) == 1, "Concurrent status changes accept one version and reject stale updates");
        Check(await db.ReportWorkflowEvents.CountAsync() == 5 && await db.AuditLogs.CountAsync(a => a.Action == "Report Status Changed") == 5, "Every transition retains reason, timestamp, actor, revision and an audit entry");
        Console.WriteLine("All Report Status workflow checks passed using synthetic in-memory data.");
    }
    sealed class Photos : IProjectPhotoStorage
    {
        public int MaxFilesPerRequest => 20;
        public Task<StoredProjectPhoto> SaveAsync(IFormFile file, Guid projectId, Guid areaId, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveHazardousAreaPlanAsync(IFormFile file, Guid projectId, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveInspectionAsync(IFormFile file, Guid projectId, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string key, CancellationToken ct = default) => Task.FromResult<StoredProjectPhotoContent?>(null);
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }
    sealed class Archive : IGeneratedReportArtifactStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public Task<ArchivedReportFile> SaveAsync(Guid projectId, Guid id, string name, byte[] bytes, CancellationToken ct) { var key = $"{projectId}/{id}/{name}"; Files.Add(key, bytes.ToArray()); return Task.FromResult(new ArchivedReportFile(key, Convert.ToHexString(SHA256.HashData(bytes)))); }
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult(Files.TryGetValue(key, out var bytes) ? new StoredProjectPhotoContent(new MemoryStream(bytes), "application/pdf") : null);
    }
}
