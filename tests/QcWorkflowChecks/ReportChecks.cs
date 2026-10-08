using System.Security.Claims;
using System.Text.Json;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;

internal static class ReportChecks
{
    public static async Task SqlPreview()
    {
        var root = Path.GetFullPath("Ex-SurveyApp_api");
        var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json", true).AddJsonFile("appsettings.Development.json", true).AddEnvironmentVariables().Build();
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer(config.GetConnectionString("SurveyDatabase")).Options);
        var companies = await db.CompanyMasters.AsNoTracking().ToListAsync();
        foreach (var expected in CompanyAssets.Seed())
        {
            var saved = companies.Single(c => c.Id == expected.Id);
            Check(saved.CompanyName == expected.CompanyName && saved.DisplayName == expected.DisplayName
                && saved.Address == expected.Address && saved.Telephone == expected.Telephone && saved.Fax == expected.Fax
                && saved.Website == expected.Website && saved.Email == expected.Email && saved.LogoPath == expected.LogoPath,
                "SQL company row preserves its exact workbook values: " + expected.DisplayName);
        }
        Console.WriteLine($"SQL company master verified: {companies.Count} persisted records.");
        var user = await db.Users.AsNoTracking().Where(u => u.IsActive && u.ApprovalStatus == ApprovalStatus.Approved && (u.Role == UserRole.Admin || u.Role == UserRole.Manager)).OrderBy(u => u.Role).FirstAsync();
        var loader = new ProjectReportData(db);
        var project = await loader.AccessibleProjects(Principal(user)).OrderByDescending(p => db.AreaEquipment.Count(e => e.ProjectId == p.Id)).ThenByDescending(p => p.UpdatedAt).FirstAsync();
        var report = await loader.Load(project.Id, Principal(user), default) ?? throw new Exception("Report unavailable");
        var settings = config.GetSection("ProjectPhotoStorage").Get<ProjectPhotoStorageSettings>() ?? new();
        var storage = new ProjectPhotoStorage(Options.Create(settings), new EnvironmentRoot {ContentRootPath = root});
        var bytes = await new ProjectReportPdf(storage, NullLogger.Instance).Generate(report, default);
        var directory = Path.GetFullPath("../output/pdf"); Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "ex-survey-project-report.pdf"); await File.WriteAllBytesAsync(output, bytes);
        Console.WriteLine($"SQL report verified: {report.ProjectName}; {report.Areas.Count} areas; {report.Areas.Sum(a => a.Equipment.Count)} equipment; {report.Areas.SelectMany(a => a.Equipment).Sum(e => e.Inspections.Count)} inspections.");
        Console.WriteLine("Read-only report preview saved: " + output);
    }
    private sealed class EnvironmentRoot : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Report verification";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    public static async Task Run()
    {
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var manager = User("Reporting Manager", UserRole.Manager);
        var qc = User("Assigned QC", UserRole.Qc); qc.ManagerId = manager.Id;
        var surveyor = User("Recorded Surveyor", UserRole.Surveyor);
        var client = User("Recorded Client", UserRole.Client);
        var other = User("Unrelated Manager", UserRole.Manager);
        db.Users.AddRange(manager, qc, surveyor, client, other);
        var project = new Project {ProjectName = "DEMONSTRATION PROJECT", JobNo = "DYNAMIC-REPORT-TEST", Sector = "Marine", Location = "Recorded Location", VesselOrSiteName = "Recorded Site", AssignedQcId = qc.Id, AssignedSurveyorId = surveyor.Id, AssignedClientId = client.Id, CreatedByUserId = Guid.NewGuid(), HazardousAreaPlanPath = "plan.pdf"};
        var area = new ProjectArea {ProjectId = project.Id, AreaName = "MAIN DECK", LayoutImagePath = "layout.png"};
        var second = new ProjectArea {ProjectId = project.Id, AreaName = "ENGINE ROOM"};
        var layout = new ProjectAreaPhoto {ProjectAreaId = area.Id, FileName = "not-visible-layout.png", FilePath = "layout.png", UploadedByUserId = manager.Id};
        var equipment = new AreaEquipment {ProjectId = project.Id, AreaId = area.Id, SequenceNo = 1, TagNumber = "TAG-ALPHA", EquipmentCode = "EQUIP-ALPHA", Type = "Transformer", XPercent = 25, YPercent = 50, CircuitId = "CIRCUIT-1", Manufacturer = "Recorded Manufacturer", ProtectionDetails = "Recorded protection", CertificateNumber = "Recorded Certificate", Location = "Deck North", CreatedByUserId = surveyor.Id};
        var uninspected = new AreaEquipment {ProjectId = project.Id, AreaId = area.Id, SequenceNo = 2, TagNumber = "TAG-BETA", Type = "Motor", XPercent = 75, YPercent = 25, CreatedByUserId = surveyor.Id};
        var template = new ChecklistTemplate {Name = "Recorded Detailed Checklist", InspectionType = InspectionType.Detailed};
        var questions = new[] {"Enclosure is intact", "Cable connections are secure", "Unused terminals are protected"}.Select((text, n) => new ChecklistQuestion {ChecklistTemplateId = template.Id, SortOrder = n + 1, Text = text}).ToArray();
        var inspection = new SurveySession {AreaEquipmentId = equipment.Id, SurveyorId = surveyor.Id, ChecklistTemplateId = template.Id, Status = SurveyStatus.Returned, SubmittedAt = DateTimeOffset.UtcNow, InspectionTimestamp = DateTimeOffset.UtcNow, InspectorName = "Recorded Inspector", SurveyorRemarks = "Cable needs replacement. Surveyor remark from database."};
        var finding = new Finding {SurveySessionId = inspection.Id, Observation = "Cable insulation damaged", Recommendation = "Replace the damaged cable", Severity = "High"};
        var photo = new SurveyFile {SurveySessionId = inspection.Id, FindingId = finding.Id, FileName = "not-visible-photo.png", StorageKey = "photo.png", ContentType = "image/png", UploadedById = surveyor.Id, Category = FileCategory.FindingPhoto};
        db.Projects.Add(project); db.ProjectAreas.AddRange(area, second); db.ProjectAreaPhotos.Add(layout); db.AreaEquipment.AddRange(equipment, uninspected); db.ChecklistTemplates.Add(template); db.ChecklistQuestions.AddRange(questions); db.SurveySessions.Add(inspection); db.Findings.Add(finding); db.Files.Add(photo);
        db.SurveyAnswers.AddRange(questions.Select((q, n) => new SurveyAnswer {SurveySessionId = inspection.Id, ChecklistQuestionId = q.Id, Value = new[] {"True", "False", "N/A"}[n], Comment = n == 1 ? "Loose cable" : null}));
        db.QcReviews.Add(new QcReview {SurveySessionId = inspection.Id, ReviewedByUserId = manager.Id, Status = QcReviewStatus.ChangesRequested, Severity = QcSeverity.High, Comments = "Please correct cable insulation", StartedAt = DateTimeOffset.UtcNow, ReviewedAt = DateTimeOffset.UtcNow});
        await db.SaveChangesAsync();
        var loader = new ProjectReportData(db);
        var report = await loader.Load(project.Id, Principal(manager), default) ?? throw new Exception("Missing report");
        Check(report.Areas.Count == 2 && report.Areas[0].Equipment.Count + report.Areas[1].Equipment.Count == 2, "Report loads actual project areas and equipment in one aggregate");
        Check(report.Assets.Count() == 3 && report.PreparedBy == surveyor.DisplayName && report.Client == client.DisplayName && report.CheckedBy == manager.DisplayName, "Report resolves actual assignments and asset references");
        var record = report.Areas.SelectMany(a => a.Equipment).Single(e => e.SequenceNo == 1).Inspections.Single();
        Check(record.Answers.Select(a => ProjectReportPdf.Answer(a.Value)).SequenceEqual(new[] {"PASS", "FAIL", "N/A"}), "PDF answer representation uses stored answers, never assumed Pass");
        Check(record.Findings.Single().Recommendation == finding.Recommendation && record.Photos.Single().Key == "photo-" + photo.Id && record.Status == "ChangesRequested", "Report includes findings, recommendations, photos and real QC result");
        inspection.ChecklistTemplateId = null;
        await db.SaveChangesAsync();
        var inferred = await loader.Load(project.Id, Principal(manager), default);
        var inferredInspection = inferred!.Areas.SelectMany(a => a.Equipment).Single(e => e.SequenceNo == 1).Inspections.Single();
        Check(inferredInspection.InspectionType == "Detailed" && inferredInspection.Answers.All(a => a.InspectionType == "Detailed"), "Saved question templates identify inspection grades even when session template is unset");
        Check(await loader.Load(project.Id, Principal(other), default) == null, "Unrelated Manager cannot obtain report data");
        Check(await loader.Load(project.Id, Principal(surveyor), default) != null && await loader.Load(project.Id, Principal(qc), default) != null && await loader.Load(project.Id, Principal(client), default) != null, "Assigned Surveyor, QC and Client retain scoped report access");
        Check(!JsonSerializer.Serialize(report).Contains("StorageKey"), "Public report DTO never exposes physical storage keys");
        project.UidNumber = "226869"; project.DocumentNumber = "DYNAMIC/5001"; project.Revision = "A";
        project.Description = "INITIAL ISSUE"; project.ClientName = "PROJECT CLIENT COMPANY"; project.ClientLogoPath = "logo.png";
        await db.SaveChangesAsync();
        report = (await loader.Load(project.Id, Principal(manager), default))!;
        Check(report.UidNumber == "226869" && report.DocumentNumber == "DYNAMIC/5001" && report.Revision == "A" && report.Description == "INITIAL ISSUE" && report.Client == "PROJECT CLIENT COMPANY" && report.ClientLogo?.Url.EndsWith("/assets/client-logo") == true && report.Assets.Count() == 4, "Report maps optional project metadata and client branding from saved project data");
        var storage = new Images();
        var company = CompanyAssets.Seed().Single(c => c.DisplayName.EndsWith("- DF"));
        db.CompanyMasters.Add(company); project.OurCompanyId = company.Id; await db.SaveChangesAsync();
        report = (await loader.Load(project.Id, Principal(manager), default))!;
        Check(report.OurCompany?.Email == company.Email && report.OurCompanyLogo is not null && report.Assets.Count() == 5, "Report dynamically resolves selected company contacts and logo");
        var result = await new ProjectReportPdf(storage, NullLogger.Instance).Generate(report, default);
        using var pdf = PdfReader.Open(new MemoryStream(result), PdfDocumentOpenMode.Import);
        Check(pdf.PageCount >= 18 && pdf.Pages.Cast<PdfPage>().Any(p => p.Width.Point > p.Height.Point), "Reference-style report includes portrait sections and landscape tables");
        var scratch = Path.GetFullPath("../tmp/pdfs/reports"); Directory.CreateDirectory(scratch);
        await File.WriteAllBytesAsync(Path.Combine(scratch, "reference-template-test.pdf"), result);
        var pngReport = report with {HazardousAreaPlan = report.HazardousAreaPlan! with {ContentType = "image/png", StorageKey = "layout.png"}};
        var pngResult = await new ProjectReportPdf(storage, NullLogger.Instance).Generate(pngReport, default);
        using var pngDocument = PdfReader.Open(new MemoryStream(pngResult), PdfDocumentOpenMode.Import);
        Check(pngDocument.Pages[5].Width.Point > 1000 && pngDocument.Pages[5].Width.Point > pngDocument.Pages[5].Height.Point,
            "PNG Hazardous Area Plan uses the same large A3 landscape page as area layouts");
        await File.WriteAllBytesAsync(Path.Combine(scratch, "hazardous-plan-png-test.pdf"), pngResult);
        var indexReport = report with {HazardousAreaPlan = null, Areas = [new ReportArea("INDEX CONTINUATION AREA", null,
            Enumerable.Range(1, 97).Select(number => report.Areas[0].Equipment[0] with {
                SequenceNo = number, EquipmentId = number % 2 == 0 ? "INDEX-ITEM-" + number : null,
                TagNumber = "INDEX-TAG-" + number, Type = number % 3 == 0 ? "LONG EQUIPMENT TYPE THAT WRAPS CLEANLY INSIDE ITS COLUMN" : "Motor", Inspections = []
            }).ToArray())]};
        var indexResult = await new ProjectReportPdf(storage, NullLogger.Instance).Generate(indexReport, default);
        await File.WriteAllBytesAsync(Path.Combine(scratch, "equipment-index-test.pdf"), indexResult);
        // Exercise multiple TOC pages, table continuation and rows taller than a printable page.
        var longEquipment = report.Areas.SelectMany(a => a.Equipment).First(e => e.Inspections.Count > 0);
        var longRecord = longEquipment.Inspections.Single() with {Comments = string.Join(" ", Enumerable.Repeat("LONG-REMARK", 1800)), Photos = []};
        var stress = report with {HazardousAreaPlan = null, Areas = Enumerable.Range(1, 20).Select(n => new ReportArea("DYNAMIC AREA " + n, null, n == 1 ? Enumerable.Range(1, 90).Select(number => longEquipment with {SequenceNo = number, TagNumber = "STRESS-TAG-" + number, Inspections = number == 1 ? new[] {longRecord} : []}).ToArray() : [])).ToArray()};
        var stressPdf = await new ProjectReportPdf(storage, NullLogger.Instance).Generate(stress, default);
        await File.WriteAllBytesAsync(Path.Combine(scratch, "pagination-test.pdf"), stressPdf);
        using var large = PdfReader.Open(new MemoryStream(stressPdf), PdfDocumentOpenMode.Import);
        Check(large.PageCount > 50, "Large projects paginate without truncating long saved text");
        Console.WriteLine("All report data and PDF checks passed.");
    }
    private static User User(string name, UserRole role) => new() {DisplayName = name, Email = name.Replace(" ", "") + "@test.invalid", PasswordHash = "test", Role = role};
    internal static ClaimsPrincipal Principal(User user) => new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Role, user.Role.ToString())], "Report verification"));
    private static void Check(bool value, string message) {if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message);}
    private sealed class Images : IProjectPhotoStorage
    {
        public int MaxFilesPerRequest => 20;
        public Task DeleteAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveAsync(Microsoft.AspNetCore.Http.IFormFile f, Guid p, Guid a, CancellationToken c) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveInspectionAsync(Microsoft.AspNetCore.Http.IFormFile f, Guid p, Guid i, CancellationToken c) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveHazardousAreaPlanAsync(Microsoft.AspNetCore.Http.IFormFile f, Guid p, CancellationToken c) => throw new NotSupportedException();
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string key, CancellationToken ct = default)
        {
            if (key.StartsWith("company-logo/")) return Task.FromResult<StoredProjectPhotoContent?>(new(CompanyAssets.Logo(key["company-logo/".Length..])!, "image/png"));
            var stream = new MemoryStream();
            if (key == "plan.pdf") {
                using var pdf = new PdfDocument(); for (var n = 1; n <= 2; n++) {var page = pdf.AddPage(); using var gfx = XGraphics.FromPdfPage(page); gfx.DrawString("ORIGINAL VECTOR PLAN PAGE " + n, new XFont("Report Sans", 18), XBrushes.Black, 50, 100); gfx.DrawRectangle(XPens.Blue, 50, 140, 430, 400);} pdf.Save(stream, false);
                stream.Position = 0; return Task.FromResult<StoredProjectPhotoContent?>(new(stream, "application/pdf"));
            }
            using var bitmap = new SKBitmap(1000, 600); using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.White);
            using var paint = new SKPaint {Color = key == "layout.png" ? SKColors.CornflowerBlue : SKColors.OrangeRed, Style = SKPaintStyle.Stroke, StrokeWidth = 8};
            canvas.DrawRect(60, 60, 880, 480, paint); canvas.DrawLine(500, 60, 500, 540, paint); canvas.DrawCircle(250, 300, 60, paint);
            using var image = SKImage.FromBitmap(bitmap); using var bytes = image.Encode(SKEncodedImageFormat.Png, 100); bytes.SaveTo(stream); stream.Position = 0;
            return Task.FromResult<StoredProjectPhotoContent?>(new(stream, "image/png"));
        }
    }
}
