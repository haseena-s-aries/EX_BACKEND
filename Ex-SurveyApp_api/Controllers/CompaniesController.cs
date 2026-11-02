using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ex_SurveyApp_api.Controllers;
[ApiController, Authorize, EndpointScopeInventory(EndpointScopeKind.SharedReferenceData), Route("api/companies")]
public class CompaniesController(SurveyDbContext db) : ControllerBase {
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, CancellationToken ct) {
        var query = db.CompanyMasters.AsNoTracking().Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) {var term = search.Trim().ToLowerInvariant(); query = query.Where(c => c.CompanyName.ToLower().Contains(term) || c.DisplayName.ToLower().Contains(term));}
        var companies = await query.OrderBy(c => c.DisplayName).ToListAsync(ct);
        return Ok(companies.Select(CompanyAssets.Response));
    }
    [HttpGet("{id:guid}/logo")]
    public async Task<IActionResult> Logo(Guid id, CancellationToken ct) {
        var company = await db.CompanyMasters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        var stream = CompanyAssets.Logo(company?.LogoPath);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, "image/png");
    }
}
