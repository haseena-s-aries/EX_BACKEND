using System.ComponentModel.DataAnnotations;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
[Authorize(Roles = "Admin,Manager,Qc")]
[Route("api/clients")]
public class ClientsController(SurveyDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Client>>> List(CancellationToken cancellationToken) =>
        Ok(await db.Clients.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Client>> Get(Guid id, CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return client is null ? NotFound() : Ok(client);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<Client>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["Name is required."]
            }));

        var client = new Client
        {
            Name = request.Name.Trim(),
            ContactEmail = request.ContactEmail?.Trim()
        };

        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = client.Id }, client);
    }
}

public record CreateClientRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [EmailAddress, StringLength(320)] string? ContactEmail);
