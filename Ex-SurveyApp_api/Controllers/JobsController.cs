using System.ComponentModel.DataAnnotations;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Manager,Qc")]
[Route("api/jobs")]
public class JobsController(SurveyDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Job>>> List([FromQuery] Guid? clientId, CancellationToken cancellationToken)
    {
        var query = db.Jobs.AsNoTracking();
        if (clientId is not null)
            query = query.Where(x => x.ClientId == clientId);

        return Ok(await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Job>> Get(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<Job>> Create(CreateJobRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientId == Guid.Empty || string.IsNullOrWhiteSpace(request.JobNumber) || string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "ClientId, JobNumber and Title are required." });

        if (!await db.Clients.AnyAsync(x => x.Id == request.ClientId, cancellationToken))
            return BadRequest(new { error = "ClientId does not refer to an existing client." });

        var number = request.JobNumber.Trim();
        if (await db.Jobs.AnyAsync(x => x.JobNumber == number, cancellationToken))
            return Conflict(new { error = "JobNumber already exists." });

        var job = new Job
        {
            ClientId = request.ClientId,
            JobNumber = number,
            Title = request.Title.Trim(),
            Uid = request.Uid?.Trim()
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = job.Id }, job);
    }
}

public record CreateJobRequest(
    [Required] Guid ClientId,
    [Required, StringLength(100, MinimumLength = 1)] string JobNumber,
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [StringLength(100)] string? Uid);
