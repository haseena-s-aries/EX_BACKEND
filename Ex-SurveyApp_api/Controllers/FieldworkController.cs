using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController, Authorize(Roles = "Surveyor")]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
[Route("api/fieldwork")]
public sealed class FieldworkController(SurveyDbContext db) : ControllerBase
{
    [HttpGet("session")]
    public IActionResult Session() => Ok(new { serverTime = DateTimeOffset.UtcNow });

    [HttpGet("projects/{projectId:guid}/records/{kind}/{id:guid}")]
    public async Task<IActionResult> Record(Guid projectId, string kind, Guid id, CancellationToken ct)
    {
        if (!await FieldworkAccess.Projects(db, User).AnyAsync(p => p.Id == projectId, ct)) return Forbid();
        // "checklist" ids are equipment ids: the equipment-specific checklist selection is the record.
        if (kind is not ("equipment" or "defect" or "activity" or "checklist")) return BadRequest();
        var entity = await FieldworkAccess.Entity(db, kind, id, projectId, ct);
        return entity is null ? NotFound() : Ok(new {server = entity, version = FieldworkAccess.Version(entity)});
    }

    /// <summary>Pre-sync revalidation: the caller is still an approved Surveyor assigned to this project.</summary>
    [HttpGet("projects/{projectId:guid}/access")]
    public async Task<IActionResult> Access(Guid projectId, CancellationToken ct) =>
        await FieldworkAccess.Projects(db, User).AnyAsync(p => p.Id == projectId, ct)
            ? Ok(new {projectId, assigned = true, serverTime = DateTimeOffset.UtcNow})
            : StatusCode(StatusCodes.Status403Forbidden, new {error = "Project assignment has changed. Local work is retained for authorized resolution."});

    [HttpGet("projects/{projectId:guid}/versions")]
    public async Task<IActionResult> Versions(Guid projectId, CancellationToken ct)
    {
        if (!await FieldworkAccess.Projects(db, User).AnyAsync(p => p.Id == projectId, ct)) return Forbid();
        var equipment = await db.AreaEquipment.AsNoTracking().Where(e => e.ProjectId == projectId).ToListAsync(ct);
        var defects = await (from f in db.Findings.AsNoTracking() join e in db.AreaEquipment on f.RegisterEquipmentId equals e.Id
            where e.ProjectId == projectId select f).ToListAsync(ct);
        return Ok(new {equipment = equipment.ToDictionary(e => e.Id, e => FieldworkAccess.Version(e)),
            defects = defects.ToDictionary(f => f.Id, f => FieldworkAccess.Version(f))});
    }
}
