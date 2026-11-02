using System.Security.Claims;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Ex_SurveyApp_api.Attributes.EndpointScopeInventory(Ex_SurveyApp_api.Attributes.EndpointScopeKind.TenantResource)]
[Authorize(Roles = "Admin,Manager")]
[Route("api/v1/haer")]
public sealed class HaerController(SurveyDbContext db)
    : ControllerBase
{
    [HttpPut("equipment/{equipmentId:guid}/metadata")]
    public async Task<IActionResult> UpdateMetadata(Guid equipmentId, HaerMetadataRequest request, CancellationToken ct)
    {
        var equipment = await db.AreaEquipment.SingleOrDefaultAsync(x => x.Id == equipmentId, ct);
        if (equipment is null) return NotFound();
        var actorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var access = new ProjectReportData(db).AccessibleProjects(User);
        if (!await access.AnyAsync(p => p.Id == equipment.ProjectId, ct)) return NotFound();
        var na = request.NotApplicableFields ?? [];
        var validFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {"description","location","manufacturer","model","serialNumber","exProtectionType","gasGroup","temperatureClass","hazardousAreaZone","ambientTemperatureRange","ipRating","certificateNumber","notifiedBody","specialConditionsOfUse","circuitId","rfidTagUid","rfidIdentificationMethod"};
        if (na.Any(x => !validFields.Contains(x)) || na.Distinct(StringComparer.OrdinalIgnoreCase).Count() != na.Count) return BadRequest(new {error = "Unknown or duplicate Not Applicable field."});
        string? Clean(string? value, int max, string key) { if (string.IsNullOrWhiteSpace(value)) return null; var normalized = value.Trim(); if (normalized.Length > max) throw new ArgumentException($"{key} exceeds {max} characters."); if (na.Contains(key, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException($"{key} cannot have a value and be Not Applicable at the same time."); return normalized; }
        if (request.ReconciliationStatus is not ("Unreviewed" or "NeedsReview" or "Reconciled" or "Exception")) return BadRequest(new {error = "Choose a supported reconciliation status."});
        var requestedRfid = request.RfidTagUid?.Trim();
        if(!string.IsNullOrWhiteSpace(requestedRfid)&&!string.Equals(requestedRfid,equipment.RfidTagUid,StringComparison.OrdinalIgnoreCase))return Conflict(new{error="RFID tag changes must use the equipment tag management workflow so replacement history is preserved."});
        if (!string.IsNullOrWhiteSpace(requestedRfid) && await db.AreaEquipment.AnyAsync(x => x.Id != equipmentId && x.RfidTagUid == requestedRfid, ct))
            return Conflict(new {error = "This RFID UID is already assigned to another equipment item."});
        try {
            equipment.Description = Clean(request.Description,1000,"description"); equipment.Location = Clean(request.Location,250,"location"); equipment.Manufacturer = Clean(request.Manufacturer,200,"manufacturer");
            equipment.Model = Clean(request.Model,200,"model"); equipment.SerialNumber = Clean(request.SerialNumber,150,"serialNumber");
            equipment.ExProtectionType = Clean(request.ExProtectionType,150,"exProtectionType"); equipment.GasGroup = Clean(request.GasGroup,100,"gasGroup");
            equipment.TemperatureClass = Clean(request.TemperatureClass,100,"temperatureClass"); equipment.HazardousAreaZone = Clean(request.HazardousAreaZone,100,"hazardousAreaZone");
            equipment.AmbientTemperatureRange = Clean(request.AmbientTemperatureRange,100,"ambientTemperatureRange"); equipment.IpRating = Clean(request.IpRating,50,"ipRating");
            equipment.CertificateNumber = Clean(request.CertificateNumber,150,"certificateNumber"); equipment.NotifiedBody = Clean(request.NotifiedBody,250,"notifiedBody");
            equipment.SpecialConditionsOfUse = Clean(request.SpecialConditionsOfUse,2000,"specialConditionsOfUse"); equipment.CircuitId = Clean(request.CircuitId,100,"circuitId");
            if(!string.IsNullOrWhiteSpace(request.RfidTagUid)){equipment.RfidTagUid = Clean(request.RfidTagUid,100,"rfidTagUid");equipment.RfidIdentificationMethod = Clean(request.RfidIdentificationMethod,100,"rfidIdentificationMethod");}
        } catch (ArgumentException ex) { return BadRequest(new {error = ex.Message}); }
        equipment.NotApplicableFieldsJson = System.Text.Json.JsonSerializer.Serialize(na.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        equipment.ReconciliationStatus = request.ReconciliationStatus;
        equipment.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog {EntityType = "AreaEquipment", EntityId = equipment.Id, ActorId = actorId, Action = "HAER Metadata Updated", Comments = $"Reconciliation status: {equipment.ReconciliationStatus}."});
        await db.SaveChangesAsync(ct);
        return Ok(new {equipment.Id, equipment.UpdatedAt, equipment.ReconciliationStatus, equipment.NotApplicableFieldsJson});
    }
}

public sealed record HaerMetadataRequest(string? Description, string? Location, string? Manufacturer, string? Model, string? SerialNumber, string? ExProtectionType,
    string? GasGroup, string? TemperatureClass, string? HazardousAreaZone, string? AmbientTemperatureRange, string? IpRating,
    string? CertificateNumber, string? NotifiedBody, string? SpecialConditionsOfUse, string? CircuitId, string? RfidTagUid,
    string? RfidIdentificationMethod, IReadOnlyList<string>? NotApplicableFields, string ReconciliationStatus);
