using System.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

public partial class DefectsController
{
    private IQueryable<Project> Projects() => new ProjectReportData(db).AccessibleProjects(User);
    private bool CanEdit(Guid? author) => User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Surveyor") && author == UserId;

    private IQueryable<DefectRow> RegisterRows()
    {
        var actor = UserId;
        var manage = User.IsInRole("Admin") || User.IsInRole("Manager");
        return from finding in db.Findings.AsNoTracking()
               join equipment in db.AreaEquipment.AsNoTracking() on finding.RegisterEquipmentId equals equipment.Id
               join project in Projects() on equipment.ProjectId equals project.Id
               join area in db.ProjectAreas.AsNoTracking() on equipment.AreaId equals area.Id
               join author in db.Users.AsNoTracking() on finding.RegisterAuthorId equals author.Id
               select new DefectRow {
                   Id = finding.Id, EquipmentId = equipment.Id, ProjectId = project.Id, ProjectName = project.ProjectName,
                   AreaName = area.AreaName, TagNumber = equipment.TagNumber, ItemNumber = equipment.EquipmentCode,
                   EquipmentDescription = equipment.Description ?? equipment.EquipmentName ?? equipment.Type,
                   RegisterDate = finding.RegisterDate, Grade = finding.RegisterGrade, AosdId = finding.AosdId,
                   FaultCategory = finding.FaultCategory, FaultCode = finding.FaultCode, Remarks = finding.Remarks,
                   AdditionalRemarks = finding.AdditionalRemarks, Observation = finding.Observation,
                   Recommendation = finding.Recommendation, Status = finding.Status, ReportedBy = author.DisplayName,
                   CanEdit = finding.Status == FindingStatus.Open && (manage || User.IsInRole("Surveyor") && finding.RegisterAuthorId == actor)
               };
    }

    [HttpGet("register/projects")]
    public async Task<IActionResult> RegisterProjects(CancellationToken ct) =>
        Ok(await Projects().OrderBy(x => x.ProjectName).Select(x => new { x.Id, Name = x.ProjectName, x.ClientName }).ToListAsync(ct));

    [HttpGet("register/projects/{projectId:guid}/equipment")]
    public async Task<IActionResult> RegisterEquipment(Guid projectId, CancellationToken ct)
    {
        if (!await Projects().AnyAsync(p => p.Id == projectId, ct)) return NotFound();
        return Ok(await (from equipment in db.AreaEquipment.AsNoTracking()
                         join area in db.ProjectAreas.AsNoTracking() on equipment.AreaId equals area.Id
                         where equipment.ProjectId == projectId && area.ProjectId == projectId
                         orderby equipment.TagNumber, equipment.Id
                         select new { equipment.Id, equipment.TagNumber, ItemNumber = equipment.EquipmentCode,
                             Description = equipment.Description ?? equipment.EquipmentName ?? equipment.Type,
                             Area = area.AreaName }).ToListAsync(ct));
    }

    [HttpPut("register/projects/{projectId:guid}/{findingId:guid}")]
    [Authorize(Roles = "Admin,Manager,Surveyor")]
    public async Task<IActionResult> SaveRegisterRow(Guid projectId, Guid findingId, RegisterFindingRequest request, CancellationToken ct)
    {
        if (findingId == Guid.Empty) return BadRequest(new { error = "A defect identifier is required." });
        if (!await Projects().AnyAsync(p => p.Id == projectId, ct)) return NotFound();
        if (!await db.AreaEquipment.AnyAsync(e => e.Id == request.EquipmentId && e.ProjectId == projectId, ct))
            return BadRequest(new { error = "Select equipment from this project." });
        if (request.Date is null || request.Grade is null || !Enum.IsDefined(request.Grade.Value) || string.IsNullOrWhiteSpace(request.Observation))
            return BadRequest(new { error = "Date, inspection grade and observation are required." });
        if (Clean(request.FaultCategory) is not (null or "A" or "B" or "C"))
            return BadRequest(new { error = "Fault category must be A, B or C." });
        if (Clean(request.AosdId)?.Length > 100 || Clean(request.FaultCode)?.Length > 100 ||
            Clean(request.Remarks)?.Length > 2000 || Clean(request.AdditionalRemarks)?.Length > 2000 ||
            request.Observation.Trim().Length > 4000 || Clean(request.Recommendation)?.Length > 4000)
            return BadRequest(new { error = "AOSD ID and fault code allow 100 characters, remarks allow 2,000, and observation and recommendation allow 4,000." });

        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var row = await db.Findings.SingleOrDefaultAsync(x => x.Id == findingId, ct);
        if (row is null)
        {
            row = new Finding { Id = findingId, RegisterEquipmentId = request.EquipmentId, RegisterAuthorId = UserId, Observation = request.Observation.Trim() };
            db.Findings.Add(row);
        }
        else if (row.RegisterEquipmentId != request.EquipmentId || !CanEdit(row.RegisterAuthorId) || row.Status != FindingStatus.Open)
        {
            return NotFound();
        }

        row.RegisterDate = request.Date;
        row.RegisterGrade = request.Grade;
        row.AosdId = Clean(request.AosdId);
        row.FaultCategory = Clean(request.FaultCategory);
        row.FaultCode = Clean(request.FaultCode);
        row.Observation = request.Observation.Trim();
        row.Recommendation = Clean(request.Recommendation);
        row.Remarks = Clean(request.Remarks);
        row.AdditionalRemarks = Clean(request.AdditionalRemarks);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPut("register/{findingId:guid}/photos/{slot:int}")]
    [Authorize(Roles = "Admin,Manager,Surveyor")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> SetRegisterPhoto(Guid findingId, int slot, IFormFile photo, CancellationToken ct) =>
        UpdateRegisterPhoto(findingId, slot, photo, ct);

    [HttpDelete("register/{findingId:guid}/photos/{slot:int}")]
    [Authorize(Roles = "Admin,Manager,Surveyor")]
    public Task<IActionResult> RemoveRegisterPhoto(Guid findingId, int slot, CancellationToken ct) =>
        UpdateRegisterPhoto(findingId, slot, null, ct);

    private async Task<IActionResult> UpdateRegisterPhoto(Guid findingId, int slot, IFormFile? photo, CancellationToken ct)
    {
        if (slot is not (1 or 2)) return BadRequest(new { error = "Choose photo slot 1 or 2." });
        var role = $"DefectPhoto{slot}";
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var row = await RegisterRows().SingleOrDefaultAsync(x => x.Id == findingId && x.CanEdit, ct);
        if (row is null) return NotFound();
        var existing = await db.Files.Where(f => f.FindingId == findingId && f.Category == FileCategory.FindingPhoto && f.EquipmentPhotoRole == role).ToListAsync(ct);
        StoredProjectPhoto? saved = null;
        if (photo is not null)
        {
            try { saved = await storage.SaveInspectionAsync(photo, row.ProjectId, findingId, ct); }
            catch (ProjectPhotoValidationException ex) { return BadRequest(new { error = ex.Message }); }
            var type = Path.GetExtension(saved.OriginalFileName).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
            db.Files.Add(new SurveyFile { FindingId = findingId, ProjectId = row.ProjectId, AreaEquipmentId = row.EquipmentId,
                Category = FileCategory.FindingPhoto, EquipmentPhotoRole = role, UploadedById = UserId,
                FileName = saved.OriginalFileName, StorageKey = saved.RelativePath, ContentType = type });
        }
        db.Files.RemoveRange(existing);
        try { await db.SaveChangesAsync(ct); }
        catch
        {
            if (saved is not null) await storage.DeleteAsync(saved.RelativePath, CancellationToken.None);
            throw;
        }
        var fieldworkFiles = FieldworkFileTransaction.For(HttpContext);
        if (fieldworkFiles is not null)
        {
            // Browser fieldwork: the outer transaction may still roll back, so keep the replaced photo until it commits.
            if (saved is not null) fieldworkFiles.DeleteIfRolledBack(saved.RelativePath);
            foreach (var file in existing) fieldworkFiles.DeleteAfterCommit(file.StorageKey);
            return NoContent();
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        foreach (var file in existing) await storage.DeleteAsync(file.StorageKey, CancellationToken.None);
        return NoContent();
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

public sealed record RegisterFindingRequest(Guid EquipmentId, DateOnly? Date, InspectionType? Grade, string? AosdId,
    string? FaultCategory, string? FaultCode, string? Observation, string? Recommendation, string? Remarks, string? AdditionalRemarks);
