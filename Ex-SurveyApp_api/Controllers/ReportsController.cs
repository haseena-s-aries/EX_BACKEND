using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Ex_SurveyApp_api.Models;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Manager,Qc,Surveyor,Client")]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
[Route("api/reports/projects")]
public class ReportsController(SurveyDbContext db, IProjectPhotoStorage storage, ILogger<ReportsController> logger, IGeneratedReportArtifactStorage artifactStorage) : ControllerBase
{
    private ProjectReportData Data => new(db);
    [HttpGet]
    public async Task<IActionResult> Projects(CancellationToken ct) => Ok(await Data.AccessibleProjects(User).OrderByDescending(p => p.UpdatedAt)
        .Select(p => new {p.Id, p.ProjectName, JobNumber = p.JobNo, p.Location, p.Status, ReportStatus = db.ProjectReportWorkflows.Where(w => w.ProjectId == p.Id).Select(w => w.Status).FirstOrDefault() ?? "Ongoing"}).ToListAsync(ct));

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectReport>> Report(Guid projectId, CancellationToken ct)
    {
        if (User.IsInRole("Client") && !await Data.AccessibleProjects(User).AnyAsync(p => p.Id == projectId, ct)) return Forbid();
        var report = await Data.Load(projectId, User, ct);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("{projectId:guid}/haer")]
    public async Task<IActionResult> Haer(Guid projectId, [FromQuery] Guid? areaId, CancellationToken ct)
    {
        var report = await Data.LoadHaer(projectId, User, ct, areaId);
        if (report is null) return NotFound();
        await LogExport(projectId,"HAER Report Viewed",areaId?.ToString("D"),ct);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(report);
    }

    [HttpGet("{projectId:guid}/haer.csv")]
    public async Task<IActionResult> HaerCsv(Guid projectId, [FromQuery] Guid? areaId, CancellationToken ct)
    {
        var report = await Data.LoadHaer(projectId, User, ct, areaId);
        if (report is null) return NotFound();
        await LogExport(projectId,"HAER CSV Generated",areaId?.ToString("D"),ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(HaerReportExports.Csv(report), "text/csv; charset=utf-8", $"{SafeJob(report.JobNumber)}_HAER.csv");
    }

    [HttpGet("{projectId:guid}/haer.pdf")]
    public async Task<IActionResult> HaerPdf(Guid projectId, [FromQuery] Guid? areaId, CancellationToken ct)
    {
        var report = await Data.LoadHaer(projectId, User, ct, areaId); if (report is null) return NotFound();
        var header = await Data.AccessibleProjects(User).Where(p => p.Id == projectId)
            .Select(p => new { p.Location, p.ClientLogoPath, Logo = p.OurCompany == null ? null : p.OurCompany.LogoPath })
            .SingleOrDefaultAsync(ct);
        if (header is null) return NotFound();
        await LogExport(projectId,"HAER PDF Generated",areaId?.ToString("D"),ct);
        Response.Headers.CacheControl = "private, no-store";
        var clientLogo = string.IsNullOrWhiteSpace(header.ClientLogoPath) ? null : await storage.OpenReadAsync(header.ClientLogoPath, ct);
        await using var clientLogoStream = clientLogo?.Stream;
        return File(HaerReportExports.Pdf(report, header.Location, header.Logo, clientLogoStream), "application/pdf", $"{SafeJob(report.JobNumber)}_HAER.pdf");
    }

    [HttpGet("{projectId:guid}/haer/excel")]
    public async Task<IActionResult> HaerExcel(Guid projectId, [FromQuery] Guid? areaId, CancellationToken ct)
    {
        var report = await Data.LoadHaer(projectId, User, ct, areaId); if (report is null) return NotFound();
        await LogExport(projectId,"HAER Excel Generated",areaId?.ToString("D"),ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(HaerReportExports.Excel(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{SafeJob(report.JobNumber)}_HAER.xlsx");
    }

    private static string SafeJob(string value) {var safe = string.Concat(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')); return safe.Length == 0 ? "project" : safe;}

    private async Task LogExport(Guid projectId,string action,string? detail,CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,out var actor))return;
        db.AuditLogs.Add(new Ex_SurveyApp_api.Models.AuditLog{EntityType="Project",EntityId=projectId,ActorId=actor,Action=action,Comments=detail});
        await db.SaveChangesAsync(ct);
    }

    [HttpGet("{projectId:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid projectId, [FromServices] IGeneratedReportArtifactStorage archive, CancellationToken ct)
    {
        if (User.IsInRole("Surveyor")) return BadRequest(new { message = "Enter report document details before generating the PDF." });
        return await GeneratePdf(projectId, null, User.IsInRole("Client") ? null : archive, ct);
    }

    [HttpGet("{projectId:guid}/pdf/preview")]
    public Task<IActionResult> PreviewPdf(Guid projectId, CancellationToken ct) =>
        GeneratePdf(projectId, null, null, ct, preview: true);

    private const string ProjectReportType = "ProjectInspectionReport";

    [Authorize(Roles = "Surveyor")]
    [HttpGet("{projectId:guid}/document-details")]
    public async Task<IActionResult> DraftDocumentDetails(Guid projectId, CancellationToken ct)
    {
        if (!await Data.AccessibleProjects(User).AnyAsync(p => p.Id == projectId, ct)) return NotFound();
        var actor = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var details = await db.GeneratedReportArtifacts.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.ReportType == ProjectReportType &&
                x.ApprovalState == "Draft" && x.GeneratedByUserId == actor)
            .OrderByDescending(x => x.Revision)
            .Select(x => new ReportDocumentDetails(x.UidNumber!, x.DocumentNumber!, x.DocumentRevision!))
            .FirstOrDefaultAsync(ct);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(details ?? new ReportDocumentDetails("", "", ""));
    }

    [Authorize(Roles = "Surveyor")]
    [HttpPost("{projectId:guid}/pdf")]
    public Task<IActionResult> GenerateSurveyorPdf(Guid projectId, ReportDocumentDetails details,
        [FromServices] IGeneratedReportArtifactStorage archive, CancellationToken ct) =>
        GeneratePdf(projectId, details, archive, ct);

    private async Task<IActionResult> GeneratePdf(Guid projectId, ReportDocumentDetails? details, IGeneratedReportArtifactStorage? archive, CancellationToken ct, bool preview = false)
    {
        var gate = ReportWorkflowLocks.For(projectId);
        await gate.WaitAsync(ct);
        try {
            if (!await Data.AccessibleProjects(User).AnyAsync(p => p.Id == projectId, ct)) return User.IsInRole("Client") ? Forbid() : NotFound();
            var workflow = await db.ProjectReportWorkflows.AsNoTracking().SingleOrDefaultAsync(w => w.ProjectId == projectId, ct);
            if (workflow?.Status == "Submitted")
            {
                if (details is not null) return Conflict(new {message = "The submitted report is locked. An Admin or Manager must open a new revision."});
                var final = await db.GeneratedReportArtifacts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == workflow.SubmittedArtifactId && a.ProjectId == projectId, ct);
                if (final is null) return Problem("The submitted report archive is unavailable.", statusCode: 500);
                var stored = await artifactStorage.OpenReadAsync(final.StorageKey, ct);
                if (stored is null) return Problem("The submitted report file is unavailable.", statusCode: 500);
                await using var stream = stored.Stream; using var output = new MemoryStream(); await stream.CopyToAsync(output, ct);
                var content = output.ToArray();
                if (!string.Equals(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)), final.Sha256, StringComparison.OrdinalIgnoreCase)) return Problem("The submitted report failed its integrity check.", statusCode: 500);
                Response.Headers.CacheControl = "private, no-store";
                return File(content, final.ContentType, final.FileName);
            }
            if (User.IsInRole("Client") && !await Data.AccessibleProjects(User).AnyAsync(p => p.Id == projectId, ct)) return Forbid();
            var report = await Data.Load(projectId, User, ct);
            if (report is null) return NotFound();
            Response.Headers.CacheControl = "private, no-store";
            if (details is not null)
                report = report with { UidNumber = details.UidNumber.Trim(), DocumentNumber = details.DocumentNumber.Trim(), Revision = details.Revision.Trim() };
            var bytes = await new ProjectReportPdf(storage, logger).Generate(report, ct);
            var fileName = SafeJob(report.JobNumber) + "_Explosion_Proof_Survey_Report.pdf";
            if (preview) return File(bytes, "application/pdf", fileName);

            var lastGeneration = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "Project" && a.EntityId == projectId &&
                (a.Action == "Report Generated" || a.Action == "Report Revision Available")).OrderByDescending(a => a.OccurredAt).FirstOrDefaultAsync(ct);
            db.AuditLogs.Add(new Ex_SurveyApp_api.Models.AuditLog {EntityType = "Project", EntityId = projectId,
                ActorId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value),
                Action = lastGeneration is not null && lastGeneration.Comments != report.Revision ? "Report Revision Available" : "Report Generated", Comments = report.Revision});
            if (archive is not null)
            {
                if (workflow is null) db.ProjectReportWorkflows.Add(new ProjectReportWorkflow {
                    ProjectId = projectId, Status = "Ongoing", UpdatedAt = DateTimeOffset.UtcNow,
                    UpdatedByUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) });
                var version = await db.GeneratedReportArtifacts
                    .Where(x => x.ProjectId == projectId && x.ReportType == ProjectReportType && x.LinkedEntityId == projectId)
                    .MaxAsync(x => (int?)x.Revision, ct) ?? 0;
                var id = Guid.NewGuid();
                var stored = await archive.SaveAsync(projectId, id, fileName, bytes, ct);
                db.GeneratedReportArtifacts.Add(new GeneratedReportArtifact
                {
                    Id = id, ProjectId = projectId, LinkedEntityId = projectId,
                    ReportType = ProjectReportType, ReportNumber = $"Project report {projectId:D}", Revision = version + 1,
                    UidNumber = report.UidNumber, DocumentNumber = report.DocumentNumber, DocumentRevision = report.Revision,
                    FileName = fileName, ContentType = "application/pdf", StorageKey = stored.StorageKey, Sha256 = stored.Sha256,
                    GeneratedByUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)
                });
            }
            await db.SaveChangesAsync(ct);
            return File(bytes, "application/pdf", fileName);
        }
        catch (OperationCanceledException) {throw;}
        catch (Exception ex) {
            logger.LogError(ex, "Report PDF {Operation} failed for project {ProjectId}", preview ? "preview" : "generation", projectId);
            return Problem(preview ? "Could not prepare the report preview. Please try again or contact the administrator."
                : "Could not generate the report. Please try again or contact the administrator.", statusCode: 500);
        }
        finally { gate.Release(); }
    }

    [HttpGet("{projectId:guid}/assets/{key}")]
    public async Task<IActionResult> Asset(Guid projectId, string key, CancellationToken ct)
    {
        if (User.IsInRole("Client") && !await Data.AccessibleProjects(User).AnyAsync(p => p.Id == projectId, ct)) return Forbid();
        var report = await Data.Load(projectId, User, ct);
        var asset = report?.Assets.SingleOrDefault(a => a.Key == key);
        if (asset is null) return NotFound();
        await LogExport(projectId,"Report Evidence Downloaded",key,ct);
        var file = await storage.OpenReadAsync(asset.StorageKey, ct);
        if (file is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Stream, file.ContentType, enableRangeProcessing: true);
    }
}

public sealed record ReportDocumentDetails(
    [Required, StringLength(100)] string UidNumber,
    [Required, StringLength(200)] string DocumentNumber,
    [Required, StringLength(100)] string Revision);
