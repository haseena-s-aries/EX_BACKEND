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
        if (kind is not ("equipment" or "defect" or "activity")) return BadRequest();
        var entity = await FieldworkAccess.Entity(db, kind, id, projectId, ct);
        return entity is null ? NotFound() : Ok(new {server = entity, version = FieldworkAccess.Version(entity)});
    }

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
