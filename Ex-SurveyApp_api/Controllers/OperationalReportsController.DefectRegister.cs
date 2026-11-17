using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

public sealed partial class OperationalReportsController
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> DefectArchiveLocks = new();
    [HttpGet("projects/{projectId:guid}/defect-register")]
    public async Task<IActionResult> DefectRegister(Guid projectId, CancellationToken ct)
    {
        var report = await new DefectRegisterReportData(db).Load(projectId, User, ct);
        if (report is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        return Ok(report);
    }

    [HttpGet("projects/{projectId:guid}/defect-register/photos/{fileId:guid}")]
    public async Task<IActionResult> DefectRegisterPhoto(Guid projectId, Guid fileId, CancellationToken ct)
    {
        var report = await new DefectRegisterReportData(db).Load(projectId, User, ct);
        var photo = report?.Items.SelectMany(r => r.Photos).FirstOrDefault(p => p.Id == fileId);
        if (photo is null) return NotFound();
        var stored = await storage.OpenReadAsync(photo.StorageKey, ct);
        if (stored is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stored.Stream, photo.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("projects/{projectId:guid}/defect-register.csv")]
    public Task<IActionResult> DefectCsv(Guid projectId, CancellationToken ct) => ExportDefects(projectId, "csv", ct);

    [HttpGet("projects/{projectId:guid}/defect-register.pdf")]
    public Task<IActionResult> DefectPdf(Guid projectId, CancellationToken ct) => ExportDefects(projectId, "pdf", ct);

    [HttpGet("projects/{projectId:guid}/defect-register/excel")]
    public Task<IActionResult> DefectExcel(Guid projectId, CancellationToken ct) => ExportDefects(projectId, "xlsx", ct);

    private async Task<IActionResult> ExportDefects(Guid projectId, string format, CancellationToken ct)
    {
        var report = await new DefectRegisterReportData(db).Load(projectId, User, ct);
        if (report is null) return NotFound();
        byte[] bytes; string contentType;
        if (format == "csv") { bytes = DefectReportExports.Csv(report); contentType = "text/csv; charset=utf-8"; }
        else
        {
            var media = await DefectReportMedia.Load(report, storage, ct);
            bytes = format == "pdf" ? DefectReportExports.Pdf(report, media) : DefectWorkbook.Export(report, media);
            contentType = format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }
        var fileName = $"{Safe(report.JobNumber)}_Defect_Register.{format}";
        if (format != "csv")
        {
            var archiveLock = DefectArchiveLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
            await archiveLock.WaitAsync(ct);
            try
            {
                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
                var type = format == "pdf" ? "DefectRegisterPdf" : "DefectRegisterExcel";
                var revision = (await db.GeneratedReportArtifacts.Where(a => a.ProjectId == projectId && a.ReportType == type).Select(a => (int?)a.Revision).MaxAsync(ct) ?? 0) + 1;
                // Store a new immutable artifact for each download, using the existing archive mechanism.
                var artifact = await Archive(projectId, projectId, type, $"{Safe(report.JobNumber)}-DEFECT", revision,
                    $"{Safe(report.JobNumber)}_Defect_Register_r{revision}.{format}", contentType, bytes, ct);
                fileName = artifact.FileName;
                if (transaction is not null) await transaction.CommitAsync(ct);
            }
            finally { archiveLock.Release(); }
        }
        await RecordExport($"Defect {format.ToUpperInvariant()} Report Generated", projectId, $"rows={report.Items.Count}", ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(bytes, contentType, fileName);
    }
}
