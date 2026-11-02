using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Manager,Qc,Surveyor,Client")]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
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
        static string Cell(string? value)
        {
            var safe = value?.Trim() ?? "";
            if (safe.Length > 0 && safe[0] is '=' or '+' or '-' or '@' or '\t' or '\r') safe = "'" + safe;
            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }
        static string Metadata(HaerRow equipment, string key, string? value) => equipment.MetadataAvailability.GetValueOrDefault(key) switch {"NotApplicable" => "Not Applicable", "Unknown" => "Unknown / not recorded", _ => value ?? "Unknown / not recorded"};
        var lines = new List<string> { string.Join(",", new[] {"Site", "Item Number", "Equipment ID", "Tag Number", "Area", "Description", "Type", "Location", "Manufacturer", "Model", "Serial Number", "Ex Protection Method", "Gas Group", "Temperature Class", "Zone", "Ambient Range", "IP Rating", "Certificate", "Notified Body", "Special Conditions", "Circuit", "RFID UID", "RFID Method", "Latest Grade", "Latest Date (UTC)", "Latest Result", "Review Status", "Outstanding Defects", "Outstanding Defect Statuses", "Inspection History", "Reconciliation Status"}.Select(Cell)) };
        lines.AddRange(report.Equipment.Select(e => string.Join(",", new[] {report.SiteName, e.SequenceNo.ToString(), e.EquipmentId, e.TagNumber, e.Area, Metadata(e,"description",e.Description), e.EquipmentType, Metadata(e,"location",e.Location), Metadata(e,"manufacturer",e.Manufacturer), Metadata(e,"model",e.Model), Metadata(e,"serialNumber",e.SerialNumber), Metadata(e,"exProtectionType",e.ExProtectionType), Metadata(e,"gasGroup",e.GasGroup), Metadata(e,"temperatureClass",e.TemperatureClass), Metadata(e,"hazardousAreaZone",e.HazardousAreaZone), Metadata(e,"ambientTemperatureRange",e.AmbientTemperatureRange), Metadata(e,"ipRating",e.IpRating), Metadata(e,"certificateNumber",e.CertificateNumber), Metadata(e,"notifiedBody",e.NotifiedBody), Metadata(e,"specialConditionsOfUse",e.SpecialConditionsOfUse), Metadata(e,"circuitId",e.CircuitId), Metadata(e,"rfidTagUid",e.RfidTagUid), Metadata(e,"rfidIdentificationMethod",e.RfidIdentificationMethod), e.LastInspectionGrade ?? "Unknown / not recorded", e.LastInspectionAt?.ToUniversalTime().ToString("O") ?? "Unknown / not recorded", e.LastInspectionResult ?? "Unknown / not recorded", e.QcStatus ?? e.InspectionStatus, e.OutstandingDefects.ToString(), string.Join("; ",e.OutstandingDefectStatuses), string.Join("; ",e.InspectionHistory.Select(h=>$"{h.Grade} {h.InspectedAt?.ToUniversalTime():yyyy-MM-dd} {h.Status} {h.Result}")), e.ReconciliationStatus}.Select(Cell))));
        Response.Headers.CacheControl = "private, no-store";
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join("\r\n", lines))).ToArray(), "text/csv; charset=utf-8", $"{report.JobNumber}_HAER.csv");
    }

    [HttpGet("{projectId:guid}/haer.pdf")]
    public async Task<IActionResult> HaerPdf(Guid projectId, [FromQuery] Guid? areaId, CancellationToken ct)
    {
        var report = await Data.LoadHaer(projectId, User, ct, areaId); if (report is null) return NotFound();
        await LogExport(projectId,"HAER PDF Generated",areaId?.ToString("D"),ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(HaerReportExports.Pdf(report), "application/pdf", $"{SafeJob(report.JobNumber)}_HAER.pdf");
    }

    [HttpGet("{projectId:guid}/haer/excel")]
    public async Task<IActionResult> HaerExcel(Guid projectId, [FromQuery] Guid? areaId, CancellationToken ct)
    {
        var report = await Data.LoadHaer(projectId, User, ct, areaId); if (report is null) return NotFound();
        await LogExport(projectId,"HAER Excel Generated",areaId?.ToString("D"),ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(HaerReportExports.Excel(report), "application/vnd.ms-excel", $"{SafeJob(report.JobNumber)}_HAER.xls");
    }

    private static string SafeJob(string value) {var safe = string.Concat(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')); return safe.Length == 0 ? "project" : safe;}

    private async Task LogExport(Guid projectId,string action,string? detail,CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,out var actor))return;
        db.AuditLogs.Add(new Ex_SurveyApp_api.Models.AuditLog{EntityType="Project",EntityId=projectId,ActorId=actor,Action=action,Comments=detail});
        await db.SaveChangesAsync(ct);
    }

    [HttpGet("{projectId:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid projectId, CancellationToken ct)
    {
        if (User.IsInRole("Client") && !await Data.AccessibleProjects(User).AnyAsync(p => p.Id == projectId, ct)) return Forbid();
        var report = await Data.Load(projectId, User, ct);
        if (report is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        try {
            var bytes = await new ProjectReportPdf(storage, logger).Generate(report, ct);
            var lastGeneration = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "Project" && a.EntityId == projectId &&
                (a.Action == "Report Generated" || a.Action == "Report Revision Available")).OrderByDescending(a => a.OccurredAt).FirstOrDefaultAsync(ct);
            db.AuditLogs.Add(new Ex_SurveyApp_api.Models.AuditLog {EntityType = "Project", EntityId = projectId,
                ActorId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value),
                Action = lastGeneration is not null && lastGeneration.Comments != report.Revision ? "Report Revision Available" : "Report Generated", Comments = report.Revision});
            await db.SaveChangesAsync(ct);
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
