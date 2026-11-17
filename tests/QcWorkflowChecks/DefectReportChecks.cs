using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PdfSharp.Pdf.IO;
using SkiaSharp;

internal static class DefectReportChecks
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS: " + name); }
    public static async Task Run()
    {
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Email = "defect-export@example.invalid", DisplayName = "Synthetic Inspector", PasswordHash = "synthetic", Role = UserRole.Surveyor };
        var project = new Project { ProjectName = "Synthetic defect exports", JobNo = "QA-DEFECT", Sector = "Test", Location = "Test yard", VesselOrSiteName = "Test vessel", CreatedByUserId = user.Id, AssignedSurveyorId = user.Id, ClientLogoPath = "client.png" };
        var other = new Project { ProjectName = "Other project", JobNo = "OTHER", Sector = "Test", Location = "Other yard", VesselOrSiteName = "Other vessel", AssignedSurveyorId = Guid.NewGuid() };
        var area = new ProjectArea { ProjectId = project.Id, AreaName = "Main deck" };
        var eq = new AreaEquipment { ProjectId = project.Id, AreaId = area.Id, TagNumber = "TAG-001", EquipmentCode = "000123", Type = "Motor", Description = "Saved motor description", CreatedByUserId = user.Id };
        var manual = new Finding { RegisterEquipmentId = eq.Id, RegisterAuthorId = user.Id, RegisterDate = new DateOnly(2026, 11, 11), RegisterGrade = InspectionType.Visual, Observation = "Oil, \"leak\"\nsecond line", Recommendation = "Repair gasket", FaultCategory = "A", AosdId = "0010", FaultCode = "F-1", Remarks = "First remarks", AdditionalRemarks = "Second remarks", Status = FindingStatus.Closed };
        var plan = new InspectionPlan { ProjectId = project.Id, Name = "Test plan" };
        var task = new InspectionTask { InspectionPlanId = plan.Id, AreaEquipmentId = eq.Id };
        var activity = new InspectionActivity { InspectionTaskId = task.Id, Grade = InspectionType.Detailed, InspectorUserId = user.Id, StartedAt = DateTimeOffset.UtcNow };
        var definition = new CheckpointDefinitionSnapshot { QuestionCode = "A-1", QuestionText = "Checkpoint one" };
        var unrelatedDefinition = new CheckpointDefinitionSnapshot { QuestionCode = "A-2", QuestionText = "Checkpoint two" };
        var response = new CheckpointResponseRevision { CheckpointDefinitionSnapshotId = definition.Id, ResponseCode = "Fail" };
        var unrelatedResponse = new CheckpointResponseRevision { CheckpointDefinitionSnapshotId = unrelatedDefinition.Id, ResponseCode = "Pass" };
        var finding = new ActivityFinding { InspectionActivityId = activity.Id, CheckpointResponseRevisionId = response.Id, Observation = "Recorded failed checkpoint", RegisterRemarks = "Inspection remark", FollowUpRemarks = "Follow-up", FailedGrades = "D,V" };
        var action = new CorrectiveAction { ActivityFindingId = finding.Id, ActionReference = "A-100", Status = CorrectiveActionStatus.ClientClosed };
        SurveyFile PhotoFile(string key, Guid? findingId = null, string? slot = null) => new() { ProjectId = project.Id, FindingId = findingId, FileName = key, StorageKey = key, ContentType = "image/png", Category = FileCategory.FindingPhoto, EquipmentPhotoRole = slot, UploadedById = user.Id };
        var photo1 = PhotoFile("red.png", manual.Id, "DefectPhoto1"); var photo2 = PhotoFile("blue.png", manual.Id, "DefectPhoto2");
        var checkpointPhoto = PhotoFile("red.png"); var wrongPhoto = PhotoFile("wrong.png");
        db.AddRange(user, project, other, area, eq, manual, plan, task, activity, definition, unrelatedDefinition, response, unrelatedResponse, finding, action, photo1, photo2, checkpointPhoto, wrongPhoto);
        db.ActivityEvidence.AddRange(new ActivityEvidence { SurveyFileId = checkpointPhoto.Id, InspectionActivityId = activity.Id, CheckpointResponseRevisionId = response.Id }, new ActivityEvidence { SurveyFileId = wrongPhoto.Id, InspectionActivityId = activity.Id, CheckpointResponseRevisionId = unrelatedResponse.Id });
        await db.SaveChangesAsync();
        ClaimsPrincipal Principal(string role, Guid? id = null) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, (id ?? user.Id).ToString()), new Claim(ClaimTypes.Role, role)], "synthetic"));
        var storage = new Photos();
        static byte[] Png(SKColor color, int width, int height) { using var bitmap = new SKBitmap(width, height); bitmap.Erase(color); using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray(); }
        storage.Files["red.png"] = Png(SKColors.OrangeRed, 180, 90); storage.Files["blue.png"] = Png(SKColors.DodgerBlue, 80, 150); storage.Files["client.png"] = Png(SKColors.ForestGreen, 200, 80);
        var archive = new Archive();
        OperationalReportsController Controller(ClaimsPrincipal principal) => new(db, storage, archive, NullLogger<OperationalReportsController>.Instance) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } } };
        var loader = new DefectRegisterReportData(db); var report = (await loader.Load(project.Id, Principal("Surveyor"), default))!;
        Check(report.Items.Count == 2 && report.Items.Any(r => r.ActionNo == "A-100" && r.Status == "ClientClosed" && r.Grade == "Detailed / Visual"), "Manual and failed checkpoint defects share the 16-column report and preserve workflow status");
        Check(report.Items.Single(r => r.Id == manual.Id).Photos.Select(p => p.Slot).SequenceEqual(new[] {1, 2}) && report.Items.Single(r => r.Id == finding.Id).Photos.Single().Id == checkpointPhoto.Id, "Separate photo slots are preserved and another checkpoint's image is excluded");
        var session = new SurveySession { AreaEquipmentId = eq.Id, SurveyorId = user.Id, InspectorName = "Historical Inspector", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var historical = new Finding { SurveySessionId = session.Id, Observation = "Historical inspection defect" };
        db.AddRange(session, historical); await db.SaveChangesAsync();
        var withHistory = (await loader.Load(project.Id, Principal("Surveyor"), default))!;
        Check(withHistory.Items.Count == 3 && withHistory.Items.Single(r => r.Id == historical.Id).Observation == historical.Observation && withHistory.Inspectors.Contains("Historical Inspector"), "Historical inspection defects and recorded inspector names remain available");
        finding.LegacyFindingId = historical.Id; await db.SaveChangesAsync();
        Check((await loader.Load(project.Id, Principal("Surveyor"), default))!.Items.Count == 2, "Bridged legacy and activity findings are not exported twice");
        finding.LegacyFindingId = null; await db.SaveChangesAsync();
        report = withHistory;
        Check(!JsonSerializer.Serialize(report).Contains("StorageKey") && !JsonSerializer.Serialize(report).Contains("client.png"), "Report preview hides private file storage keys");
        Check(await loader.Load(other.Id, Principal("Surveyor"), default) is null && await loader.Load(project.Id, Principal("Manager", Guid.NewGuid()), default) is null, "Unassigned surveyors and managers cannot export another project");
        project.AssignedQcId = user.Id; project.AssignedClientId = user.Id; await db.SaveChangesAsync();
        Check(await loader.Load(project.Id, Principal("Qc"), default) is not null && await loader.Load(project.Id, Principal("Client"), default) is not null, "Assigned QC and Client retain report access");
        var controller = Controller(Principal("Surveyor"));
        var csv = (FileContentResult)await controller.DefectCsv(project.Id, default);
        var csvText = System.Text.Encoding.UTF8.GetString(csv.FileContents);
        Check(csvText.Contains("\"Oil, \"\"leak\"\"\nsecond line\"") && csvText.Contains("\"000123\"") && csvText.Contains("First remarks") && csvText.Contains("Second remarks"), "CSV preserves quoted multiline text, text identifiers and both remarks fields");
        var excel = (FileContentResult)await controller.DefectExcel(project.Id, default);
        using (var zip = new ZipArchive(new MemoryStream(excel.FileContents)))
        {
            XNamespace n = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open(); var sheet = XDocument.Load(stream);
            Check(sheet.Descendants(n + "row").All(r => r.Elements(n + "c").Count() == 16) && sheet.Descendants(n + "pane").Single().Attribute("state")!.Value == "frozen", "XLSX contains 16 columns and a frozen header");
            Check(zip.Entries.Count(e => e.FullName.StartsWith("xl/media/")) == 3 && zip.GetEntry("xl/drawings/drawing1.xml") is not null, "XLSX embeds the correct three photos with drawing relationships");
            using var styleStream = zip.GetEntry("xl/styles.xml")!.Open(); var styles = XDocument.Load(styleStream);
            Check(styles.ToString().Contains("FFFFF299") && styles.ToString().Contains("FFC6EFCE") && styles.ToString().Contains("FFD3E1EE"), "XLSX uses yellow item/category cells, green closed status and grey-blue headers");
        }
        var pdf = (FileContentResult)await controller.DefectPdf(project.Id, default);
        using (var document = PdfReader.Open(new MemoryStream(pdf.FileContents), PdfDocumentOpenMode.Import)) Check(document.PageCount == 1 && document.Pages[0].Width.Point > 1100, "Defect PDF opens as an A3 landscape register");
        await controller.DefectPdf(project.Id, default);
        Check(await db.GeneratedReportArtifacts.CountAsync(a => a.ReportType == "DefectRegisterPdf") == 2 && archive.Files.Count == 3, "PDF and Excel downloads create immutable archive versions for Recent Reports");
        var firstArtifact = await db.GeneratedReportArtifacts.FirstAsync(a => a.ReportType == "DefectRegisterPdf");
        var stored = (FileStreamResult)await controller.DownloadArtifact(project.Id, firstArtifact.Id, default);
        using (var saved = new MemoryStream()) { await stored.FileStream.CopyToAsync(saved); Check(saved.ToArray().SequenceEqual(pdf.FileContents), "Archived PDF retrieval preserves exact generated bytes"); }
        Check(await Controller(Principal("Surveyor", Guid.NewGuid())).DefectRegisterPhoto(project.Id, photo1.Id, default) is NotFoundResult, "Unauthorized project photo download is denied");
        Check(await Controller(Principal("Surveyor")).DefectRegisterPhoto(project.Id, wrongPhoto.Id, default) is NotFoundResult, "Unrelated checkpoint evidence cannot be downloaded via this defect register");
        db.CorrectiveActions.Add(new CorrectiveAction { ActivityFindingId = finding.Id, ActionReference = "A-101", Status = CorrectiveActionStatus.InProgress });
        await db.SaveChangesAsync();
        var multiAction = (await loader.Load(project.Id, Principal("Surveyor"), default))!.Items.Single(r => r.Id == finding.Id);
        Check(multiAction.ActionNo.Contains("A-100") && multiAction.ActionNo.Contains("A-101") && multiAction.Status.Contains("ClientClosed") && multiAction.Status.Contains("InProgress"), "Multiple corrective actions retain all references and statuses without duplicating defect rows");
        var media = await DefectReportMedia.Load(report, storage, default);
        var longReport = report with { Items = Enumerable.Range(1, 24).Select(i => report.Items.Single(r => r.Id == manual.Id) with { Id = Guid.NewGuid(), ActionNo = $"QA-ROW-{i:D3}", Observation = i == 12 ? string.Join(" ", Enumerable.Repeat("Full long observation kept in a continuation layout.", 200)) : "Saved observation" }).ToArray() };
        var longPdf = DefectReportExports.Pdf(longReport, media);
        using (var document = PdfReader.Open(new MemoryStream(longPdf), PdfDocumentOpenMode.Import)) Check(document.PageCount > 3, "Long registers and over-height observations paginate without shrinking fonts");
        Check(DefectReportExports.Pdf(report with { Items = [] }, media).Length > 1000, "Empty registers still produce a valid PDF");
        Check(manual.Observation == "Oil, \"leak\"\nsecond line" && finding.StatusCode == "Open" && action.Status == CorrectiveActionStatus.ClientClosed, "Exporting does not change findings, manual fields or workflow state");
        foreach (var number in Enumerable.Range(1, 4)) {
            var extra = PhotoFile($"overflow-{number}.png", manual.Id, "DefectPhoto1");
            storage.Files[extra.StorageKey] = storage.Files["red.png"]; db.Files.Add(extra);
        }
        var documentPhoto = PhotoFile("document.pdf", manual.Id, "DefectPhoto2"); documentPhoto.ContentType = "application/pdf"; db.Files.Add(documentPhoto);
        await db.SaveChangesAsync();
        var overflowExcel = (FileContentResult)await controller.DefectExcel(project.Id, default);
        using (var zip = new ZipArchive(new MemoryStream(overflowExcel.FileContents))) {
            using var sheetStream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open(); var xml = XDocument.Load(sheetStream).ToString();
            Check(xml.Contains("Attachment (not embedded): overflow-4.png") && xml.Contains("document.pdf") && xml.Contains("/api/reports/projects/") && zip.Entries.Count(e => e.FullName.StartsWith("xl/media/")) == 2,
                "Non-image attachments and photo groups exceeding Excel row height retain explicit authorized references without overlapping cells");
        }
        var output = Path.Combine(Path.GetTempPath(), "ex-survey-defect-export-qa"); Directory.CreateDirectory(output);
        await File.WriteAllBytesAsync(Path.Combine(output, "defect-register.pdf"), pdf.FileContents); await File.WriteAllBytesAsync(Path.Combine(output, "defect-register.xlsx"), excel.FileContents); await File.WriteAllBytesAsync(Path.Combine(output, "defect-register.csv"), csv.FileContents); await File.WriteAllBytesAsync(Path.Combine(output, "defect-register-long.pdf"), longPdf);
        Console.WriteLine("Defect report checks passed. Synthetic samples: " + output);
    }
    sealed class Photos : IProjectPhotoStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public int MaxFilesPerRequest => 20;
        public Task<StoredProjectPhoto> SaveAsync(IFormFile file, Guid projectId, Guid areaId, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveHazardousAreaPlanAsync(IFormFile file, Guid projectId, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveInspectionAsync(IFormFile file, Guid projectId, Guid inspectionId, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string path, CancellationToken ct = default) => Task.FromResult(Files.TryGetValue(path, out var bytes) ? new StoredProjectPhotoContent(new MemoryStream(bytes), "image/png") : null);
    }
    sealed class Archive : IGeneratedReportArtifactStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public Task<ArchivedReportFile> SaveAsync(Guid project, Guid id, string name, byte[] data, CancellationToken ct) { var key = $"{project}/{id}/{name}"; Files.Add(key, data); return Task.FromResult(new ArchivedReportFile(key, Convert.ToHexString(SHA256.HashData(data)))); }
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult(Files.TryGetValue(key, out var bytes) ? new StoredProjectPhotoContent(new MemoryStream(bytes), "application/pdf") : null);
    }
}
