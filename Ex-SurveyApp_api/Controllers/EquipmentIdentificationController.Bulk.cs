using System.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

public sealed partial class EquipmentIdentificationController
{
    private IQueryable<EquipmentTagAssignment> ActiveRadioTags() =>
        db.EquipmentTagAssignments.AsNoTracking().Where(x => x.IsActive && (x.Technology == "HF_RFID" || x.Technology == "NFC"));

    [Authorize(Roles = "Surveyor,Admin,Manager,Qc")]
    [HttpGet("identified")]
    public async Task<IActionResult> Identified(Guid? projectId, string? q, CancellationToken ct, int page = 1, int pageSize = 20)
    {
        if (page < 1 || pageSize is < 1 or > 100 || q?.Length > 250)
            return BadRequest(new { error = "Invalid search or pagination." });

        var radio = ActiveRadioTags();
        var query = AccessibleEquipment().Where(e =>
            radio.Any(t => t.EquipmentId == e.Id) ||
            (e.RfidTagUid != null && e.RfidTagUid.Trim() != "" &&
             !db.EquipmentTagAssignments.Any(t => t.EquipmentId == e.Id && t.NormalizedIdentifier == e.RfidTagUid.Trim().Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper() &&
                 (t.Technology == "HF_RFID" || t.Technology == "NFC"))));
        if (projectId.HasValue) query = query.Where(e => e.ProjectId == projectId.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToUpperInvariant();
            var uid = Normalize(q, "HF_RFID") ?? term;
            query = query.Where(e => e.TagNumber.ToUpper().Contains(term) ||
                (e.EquipmentCode != null && e.EquipmentCode.ToUpper().Contains(term)) ||
                (e.EquipmentName != null && e.EquipmentName.ToUpper().Contains(term)) ||
                (e.Description != null && e.Description.ToUpper().Contains(term)) ||
                (e.RfidTagUid != null && e.RfidTagUid.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper().Contains(uid)) ||
                radio.Any(t => t.EquipmentId == e.Id && t.NormalizedIdentifier.Contains(uid)));
        }
        var total = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (int)(((long)total + pageSize - 1) / pageSize)));
        var rows = await (from equipment in query
                          join project in db.Projects.AsNoTracking() on equipment.ProjectId equals project.Id
                          orderby project.ProjectName, equipment.TagNumber, equipment.Id
                          select new { Equipment = equipment, project.ProjectName })
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = rows.Select(x => x.Equipment.Id).ToArray();
        var tags = (await radio.Where(t => ids.Contains(t.EquipmentId))
            .OrderByDescending(t => t.AssignedAt).ToListAsync(ct)).ToLookup(t => t.EquipmentId);
        return Ok(new { total, page, pageSize, items = rows.Select(row =>
        {
            var e = row.Equipment;
            var links = tags[e.Id].Select(t => new IdentifiedRadioTag(t.NormalizedIdentifier, t.Technology, t.AssignedAt)).ToArray();
            if (links.Length == 0 && e.RfidTagUid is not null)
                links = [new(e.RfidTagUid, e.RfidIdentificationMethod == "NFC" ? "NFC" : "HF_RFID", e.CreatedAt)];
            return new { equipmentId = e.Id, e.ProjectId, row.ProjectName, e.AreaId, e.TagNumber,
                e.EquipmentCode, e.EquipmentName, e.Description, e.Type, status = e.RegistrationStatus, tags = links };
        }) });
    }

    [Authorize(Roles = "Admin,Manager,Surveyor,Qc")]
    [HttpGet("registration-projects")]
    public async Task<IActionResult> RegistrationProjects(CancellationToken ct)
    {
        var projects = await new ProjectReportData(db).AccessibleProjects(User)
            .OrderBy(p => p.ProjectName)
            .Select(p => new { p.Id, p.ProjectName, p.ClientName,
                Areas = p.Areas.OrderBy(a => a.AreaName).Select(a => new { a.Id, a.AreaName }).ToList() })
            .ToListAsync(ct);
        return Ok(projects);
    }

    [Authorize(Roles = "Admin,Manager,Surveyor")]
    [HttpPost("bulk-register")]
    public async Task<IActionResult> BulkRegister(BulkEquipmentRequest request, CancellationToken ct)
    {
        if (request.Rows is null || request.Rows.Count is < 1 or > 100)
            return BadRequest(new { error = "Enter between 1 and 100 equipment rows." });
        if (Actor == Guid.Empty) return Forbid();
        var projects = new ProjectReportData(db).AccessibleProjects(User);
        if (!await projects.AnyAsync(p => p.Id == request.ProjectId, ct) ||
            !await db.ProjectAreas.AnyAsync(a => a.Id == request.AreaId && a.ProjectId == request.ProjectId, ct))
            return NotFound(new { error = "Choose an accessible project and an area within it." });

        var results = new List<BulkEquipmentResult>();
        var ids = new HashSet<Guid>();
        foreach (var row in request.Rows)
        {
            if (!ids.Add(row.ClientId))
            {
                results.Add(new(row.ClientId, null, "Duplicate row ID."));
                continue;
            }
            results.Add(await RegisterRow(request, row, ct));
        }
        return Ok(new { registered = results.Count(x => x.Error is null), failed = results.Count(x => x.Error is not null), results });
    }

    private async Task<BulkEquipmentResult> RegisterRow(BulkEquipmentRequest request, BulkEquipmentRow row, CancellationToken ct)
    {
        var error = ValidateEquipmentFields(row.TagNumber, row.EquipmentCode, row.EquipmentName, row.Description, row.Type);
        var technology = string.IsNullOrWhiteSpace(row.Technology) ? "HF_RFID" : row.Technology.Trim().ToUpperInvariant();
        var uid = Normalize(row.RfidUid, technology);
        if (row.ClientId == Guid.Empty || error is not null)
            return new(row.ClientId, null, error ?? "A row ID is required.");
        if ((request.RequireRfid || !string.IsNullOrWhiteSpace(row.RfidUid)) &&
            (uid is null || technology is not ("HF_RFID" or "NFC")))
            return new(row.ClientId, null, "Enter a valid HF_RFID or NFC UID with 4 to 128 hexadecimal characters.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        try
        {
            var prior = await db.AreaEquipment.AsNoTracking().SingleOrDefaultAsync(x => x.Id == row.ClientId, ct);
            if (prior is not null)
            {
                var sameTag = uid is null
                    ? prior.RfidTagUid is null && !await ActiveRadioTags().AnyAsync(t => t.EquipmentId == prior.Id, ct)
                    : await ActiveRadioTags().AnyAsync(t => t.EquipmentId == prior.Id && t.Technology == technology && t.NormalizedIdentifier == uid, ct);
                return prior.ProjectId == request.ProjectId && prior.AreaId == request.AreaId &&
                    prior.CreatedByUserId == Actor && prior.TagNumber == row.TagNumber!.Trim() &&
                    prior.EquipmentCode == Clean(row.EquipmentCode) && prior.EquipmentName == Clean(row.EquipmentName) &&
                    prior.Type == row.Type!.Trim() && prior.Description == Clean(row.Description) && sameTag && RegisterMetadataMatches(prior, row.HaerFields)
                    ? new(row.ClientId, prior.Id, null)
                    : new(row.ClientId, null, "This row ID already belongs to another registration.");
            }
            var duplicate = await FindDuplicate(request.ProjectId, null, row.TagNumber!, row.EquipmentCode, uid, ct);
            if (duplicate is not null) return new(row.ClientId, null, duplicate);
            var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == request.ProjectId, ct);
            if (project is null || !await db.ProjectAreas.AnyAsync(a => a.Id == request.AreaId && a.ProjectId == request.ProjectId, ct))
                return new(row.ClientId, null, "The project or area is no longer available.");
            var now = DateTimeOffset.UtcNow;
            var item = new AreaEquipment
            {
                Id = row.ClientId, ProjectId = request.ProjectId, AreaId = request.AreaId,
                SequenceNo = 0, // Allocated project-wide by the shared save pipeline.
                TagNumber = row.TagNumber!.Trim(), EquipmentCode = Clean(row.EquipmentCode),
                EquipmentName = Clean(row.EquipmentName), Description = Clean(row.Description), Type = row.Type!.Trim(),
                IsPlacedOnLayout = false, CreatedByUserId = Actor, RegistrationStatus = "Submitted",
                CreatedAt = now, UpdatedAt = now,
                // The legacy column holds 100 characters. The assignment supports the full UID range.
                RfidTagUid = technology == "HF_RFID" && uid?.Length <= 100 ? uid : null,
                RfidIdentificationMethod = uid is null ? null : technology
            };
            var fieldErrors = ApplyRegisterMetadata(item, row.HaerFields, row.Description);
            if (fieldErrors.Count > 0) return new(row.ClientId, null, "Correct the highlighted fields.", fieldErrors);
            db.AreaEquipment.Add(item);
            if (uid is not null)
            {
                const string reason = "Assigned during bulk equipment registration.";
                var tag = new EquipmentTagAssignment { EquipmentId = item.Id, ProjectId = item.ProjectId, Technology = technology,
                    NormalizedIdentifier = uid, AssignedAt = now, AssignedByUserId = Actor, AssignmentReason = reason };
                db.EquipmentTagAssignments.Add(tag);
                db.EquipmentTagHistory.Add(HistoryRow(item.Id, item.ProjectId, tag.Id, technology, uid, "Assigned", reason));
            }
            project.UpdatedAt = now;
            if (project.Status == ProjectStatus.Pending) project.Status = ProjectStatus.InProgress;
            db.AuditLogs.Add(new AuditLog { EntityType = "AreaEquipment", EntityId = item.Id, ActorId = Actor,
                Action = "Equipment Registration Submitted", Comments = "Bulk registration submitted for independent verification." });
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return new(row.ClientId, item.Id, null);
        }
        catch (Exception ex) when ((ex is DbUpdateException or SqlException) && ex.GetBaseException() is SqlException { Number: 2601 or 2627 or 1205 or 547 })
        {
            return new(row.ClientId, null, "A duplicate or concurrent update prevented saving this row. Review it and retry.");
        }
        finally { db.ChangeTracker.Clear(); }
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("equipment/{equipmentId:guid}/identification-details")]
    public async Task<IActionResult> EditIdentification(Guid equipmentId, EquipmentIdentificationEdit request, CancellationToken ct)
    {
        var error = ValidateEquipmentFields(request.TagNumber, request.EquipmentCode, request.EquipmentName, request.Description, request.Type);
        if (error is not null) return BadRequest(new { error });
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var item = await AccessibleEquipment(true).AsTracking().SingleOrDefaultAsync(x => x.Id == equipmentId, ct);
        if (item is null) return NotFound();
        if (!string.Equals(item.TagNumber, request.TagNumber!.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(item.EquipmentCode, Clean(request.EquipmentCode), StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await FindDuplicate(item.ProjectId, item.Id, request.TagNumber!, request.EquipmentCode, null, ct);
            if (duplicate is not null) return Conflict(new { error = duplicate });
        }
        var fieldErrors = ApplyRegisterMetadata(item, request.HaerFields, request.Description);
        if (fieldErrors.Count > 0) return BadRequest(new { error = "Correct the highlighted fields.", errors = fieldErrors });
        item.TagNumber = request.TagNumber!.Trim();
        item.EquipmentCode = Clean(request.EquipmentCode);
        item.EquipmentName = Clean(request.EquipmentName);
        item.Description = Clean(request.Description);
        item.Type = request.Type!.Trim();
        item.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog { EntityType = "AreaEquipment", EntityId = item.Id, ActorId = Actor,
            Action = "Equipment Identification Details Updated", Comments = "Updated equipment details from the identified equipment list." });
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when ((ex is DbUpdateException or SqlException) && ex.GetBaseException() is SqlException { Number: 2601 or 2627 or 1205 })
        {
            return Conflict(new { error = "A concurrent update prevented saving these equipment details." });
        }
        return NoContent();
    }

    private async Task<string?> FindDuplicate(Guid projectId, Guid? exceptId, string tagNumber, string? equipmentCode, string? uid, CancellationToken ct)
    {
        var tag = tagNumber.Trim().ToUpperInvariant();
        var code = Clean(equipmentCode)?.ToUpperInvariant();
        if (await db.AreaEquipment.AnyAsync(e => e.ProjectId == projectId && e.Id != exceptId &&
            (e.TagNumber.Trim().ToUpper() == tag || (e.EquipmentCode != null &&
             (e.EquipmentCode.Trim().ToUpper() == tag || (code != null && e.EquipmentCode.Trim().ToUpper() == code))) ||
             (code != null && e.TagNumber.Trim().ToUpper() == code)), ct))
            return "This equipment tag or item number already exists in the selected project.";
        if (uid is not null && (await db.EquipmentTagAssignments.AnyAsync(t => t.IsActive && t.NormalizedIdentifier == uid, ct) ||
            await db.AreaEquipment.AnyAsync(e => e.RfidTagUid != null &&
                e.RfidTagUid.Trim().Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper() == uid, ct)))
            return "This RFID UID is already linked to equipment.";
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ValidateEquipmentFields(string? tag, string? code, string? name, string? description, string? type)
    {
        if (Clean(tag) is null || Clean(type) is null) return "Equipment tag and type are required.";
        if (tag!.Trim().Length > 100 || Clean(code)?.Length > 100 || Clean(name)?.Length > 250 ||
            Clean(description)?.Length > 1000 || type!.Trim().Length > 100)
            return "Maximum lengths are 100 for tag, item number and type, 250 for name, and 1000 for description.";
        return null;
    }
}

public sealed record IdentifiedRadioTag(string Uid, string Technology, DateTimeOffset IdentifiedAt);
public sealed record EquipmentIdentificationEdit(string? TagNumber, string? EquipmentCode, string? EquipmentName, string? Description, string? Type, IReadOnlyDictionary<string, string?>? HaerFields = null);
public sealed record BulkEquipmentRow(Guid ClientId, string? TagNumber, string? EquipmentCode, string? EquipmentName,
    string? Description, string? Type, string? RfidUid, string? Technology, IReadOnlyDictionary<string, string?>? HaerFields = null);
public sealed record BulkEquipmentRequest(Guid ProjectId, Guid AreaId, bool RequireRfid, IReadOnlyList<BulkEquipmentRow> Rows);
public sealed record BulkEquipmentResult(Guid ClientId, Guid? EquipmentId, string? Error, IReadOnlyDictionary<string, string>? FieldErrors = null);
