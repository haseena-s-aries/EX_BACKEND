using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Manager,Qc,Surveyor,Client")]
[Route("api/reports/projects")]
public class ReportsController(SurveyDbContext db, IProjectPhotoStorage storage, ILogger<ReportsController> logger) : ControllerBase
{
    private ProjectReportData Data => new(db);
    [HttpGet]
    public async Task<IActionResult> Projects(CancellationToken ct) => Ok(await Data.AccessibleProjects(User).OrderByDescending(p => p.UpdatedAt)
        .Select(p => new {p.Id, p.ProjectName, JobNumber = p.JobNo, p.Location, p.Status}).ToListAsync(ct));

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectReport>> Report(Guid projectId, CancellationToken ct)
    {
        var report = await Data.Load(projectId, User, ct);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("{projectId:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid projectId, CancellationToken ct)
    {
        var report = await Data.Load(projectId, User, ct);
        if (report is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        try {
            var bytes = await new ProjectReportPdf(storage, logger).Generate(report, ct);
            var safeName = string.Concat(report.JobNumber.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
            return File(bytes, "application/pdf", (safeName.Length > 0 ? safeName : "project") + "_Explosion_Proof_Survey_Report.pdf");
        }
        catch (OperationCanceledException) {throw;}
        catch (Exception ex) {
            logger.LogError(ex, "Report generation failed for project {ProjectId}", projectId);
            return Problem("Could not generate the report. Please try again or contact the administrator.", statusCode: 500);
        }
    }

    [HttpGet("{projectId:guid}/assets/{key}")]
    public async Task<IActionResult> Asset(Guid projectId, string key, CancellationToken ct)
    {
        var report = await Data.Load(projectId, User, ct);
        var asset = report?.Assets.SingleOrDefault(a => a.Key == key);
        if (asset is null) return NotFound();
        var file = await storage.OpenReadAsync(asset.StorageKey, ct);
        if (file is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Stream, file.ContentType, enableRangeProcessing: true);
    }
}
