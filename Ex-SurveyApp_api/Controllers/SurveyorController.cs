using System.Security.Claims;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Surveyor))]
[Route("api/surveyor")]
public class SurveyorController(SurveyDbContext db) : ControllerBase
{
    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<SurveyorProjectSummaryResponse>>> GetProjects(
        CancellationToken cancellationToken)
    {
        var projects = await db.Projects.AsNoTracking()
            .Where(x => x.AssignedSurveyorId == CurrentUserId)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new SurveyorProjectSummaryResponse(
                x.Id, x.ProjectName, x.Sector, x.JobNo, x.Location, x.VesselOrSiteName,
                x.CreatedAt, x.Areas.Count))
            .ToListAsync(cancellationToken);

        return Ok(projects);
    }

    [HttpGet("projects/{projectId:guid}")]
    public async Task<ActionResult<SurveyorProjectDetailsResponse>> GetProject(
        Guid projectId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);

        var project = await db.Projects.AsNoTracking()
            .Include(x => x.Areas)
                .ThenInclude(x => x.Photos)
            .Include(x => x.Areas)
                .ThenInclude(x => x.EquipmentPins)
            .AsSplitQuery()
            .SingleAsync(x => x.Id == projectId, cancellationToken);

        var userIds = new[] { project.AssignedSurveyorId, project.AssignedQcId, project.AssignedClientId };
        var names = await db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        return Ok(new SurveyorProjectDetailsResponse(
            project.Id, project.ProjectName, project.Sector, project.JobNo, project.Location,
            project.VesselOrSiteName, project.CreatedAt,
            names.GetValueOrDefault(project.AssignedSurveyorId, string.Empty),
            names.GetValueOrDefault(project.AssignedQcId, string.Empty),
            names.GetValueOrDefault(project.AssignedClientId, string.Empty),
            project.Areas.OrderBy(x => x.CreatedAt).Select(x => ToAreaSummary(projectId, x)).ToArray()));
    }

    [HttpGet("projects/{projectId:guid}/areas")]
    public async Task<ActionResult<IEnumerable<SurveyorAreaSummaryResponse>>> GetAreas(
        Guid projectId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);

        var areas = await db.ProjectAreas.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Include(x => x.Photos)
            .Include(x => x.EquipmentPins)
            .AsSplitQuery()
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(areas.Select(x => ToAreaSummary(projectId, x)));
    }

    [HttpGet("projects/{projectId:guid}/areas/{areaId:guid}")]
    public async Task<ActionResult<SurveyorAreaDetailsResponse>> GetArea(
        Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);

        var area = await db.ProjectAreas.AsNoTracking()
            .Where(x => x.Id == areaId && x.ProjectId == projectId)
            .Include(x => x.Photos)
            .Include(x => x.EquipmentPins)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
        if (area is null)
            return NotFound(new { error = "Project area was not found." });

        return Ok(new SurveyorAreaDetailsResponse(
            area.Id,
            area.AreaName,
            GetLayoutImagePath(projectId, area),
            area.EquipmentPins.OrderBy(x => x.CreatedAt).Select(ToEquipmentResponse).ToArray()));
    }

    [HttpGet("projects/{projectId:guid}/areas/{areaId:guid}/equipment")]
    public async Task<ActionResult<IEnumerable<AreaEquipmentResponse>>> GetEquipment(
        Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);
        if (!await AreaExists(projectId, areaId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var equipment = await db.AreaEquipment.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.AreaId == areaId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(equipment.Select(ToEquipmentResponse));
    }

    [HttpPost("projects/{projectId:guid}/areas/{areaId:guid}/equipment")]
    public async Task<ActionResult<AreaEquipmentResponse>> CreateEquipment(
        Guid projectId, Guid areaId, UpsertAreaEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);
        if (!await AreaExists(projectId, areaId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return validationError;

        var tagNumber = request.TagNumber.Trim();
        if (await db.AreaEquipment.AnyAsync(
                x => x.AreaId == areaId && x.TagNumber == tagNumber, cancellationToken))
            return Conflict(new { error = "TagNumber already exists in this area." });

        var now = DateTimeOffset.UtcNow;
        var equipment = new AreaEquipment
        {
            ProjectId = projectId,
            AreaId = areaId,
            XPercent = request.XPercent,
            YPercent = request.YPercent,
            TagNumber = tagNumber,
            EquipmentCode = TrimOrNull(request.EquipmentCode),
            Type = request.Type.Trim(),
            CircuitId = TrimOrNull(request.CircuitId),
            Location = TrimOrNull(request.Location),
            Manufacturer = TrimOrNull(request.Manufacturer),
            ProtectionDetails = TrimOrNull(request.ProtectionDetails),
            CertificateNumber = TrimOrNull(request.CertificateNumber),
            CreatedByUserId = CurrentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.AreaEquipment.Add(equipment);
        await TouchProject(projectId, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { error = "TagNumber already exists in this area." });
        }

        return StatusCode(StatusCodes.Status201Created, ToEquipmentResponse(equipment));
    }

    [HttpPut("projects/{projectId:guid}/areas/{areaId:guid}/equipment/{equipmentId:guid}")]
    public async Task<ActionResult<AreaEquipmentResponse>> UpdateEquipment(
        Guid projectId, Guid areaId, Guid equipmentId, UpsertAreaEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);
        if (!await AreaExists(projectId, areaId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return validationError;

        var equipment = await db.AreaEquipment.SingleOrDefaultAsync(
            x => x.Id == equipmentId && x.ProjectId == projectId && x.AreaId == areaId,
            cancellationToken);
        if (equipment is null)
            return NotFound(new { error = "Equipment pin was not found." });

        var tagNumber = request.TagNumber.Trim();
        if (await db.AreaEquipment.AnyAsync(
                x => x.AreaId == areaId && x.Id != equipmentId && x.TagNumber == tagNumber,
                cancellationToken))
            return Conflict(new { error = "TagNumber already exists in this area." });

        equipment.XPercent = request.XPercent;
        equipment.YPercent = request.YPercent;
        equipment.TagNumber = tagNumber;
        equipment.EquipmentCode = TrimOrNull(request.EquipmentCode);
        equipment.Type = request.Type.Trim();
        equipment.CircuitId = TrimOrNull(request.CircuitId);
        equipment.Location = TrimOrNull(request.Location);
        equipment.Manufacturer = TrimOrNull(request.Manufacturer);
        equipment.ProtectionDetails = TrimOrNull(request.ProtectionDetails);
        equipment.CertificateNumber = TrimOrNull(request.CertificateNumber);
        equipment.UpdatedAt = DateTimeOffset.UtcNow;
        await TouchProject(projectId, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { error = "TagNumber already exists in this area." });
        }

        return Ok(ToEquipmentResponse(equipment));
    }

    [HttpDelete("projects/{projectId:guid}/areas/{areaId:guid}/equipment/{equipmentId:guid}")]
    public async Task<IActionResult> DeleteEquipment(
        Guid projectId, Guid areaId, Guid equipmentId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);
        if (!await AreaExists(projectId, areaId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var equipment = await db.AreaEquipment.SingleOrDefaultAsync(
            x => x.Id == equipmentId && x.ProjectId == projectId && x.AreaId == areaId,
            cancellationToken);
        if (equipment is null)
            return NotFound(new { error = "Equipment pin was not found." });

        db.AreaEquipment.Remove(equipment);
        await TouchProject(projectId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ProjectAccess> CheckProjectAccess(Guid projectId, CancellationToken cancellationToken)
    {
        var assignedSurveyorId = await db.Projects.AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => (Guid?)x.AssignedSurveyorId)
            .SingleOrDefaultAsync(cancellationToken);

        if (assignedSurveyorId is null)
            return ProjectAccess.NotFound;
        return assignedSurveyorId == CurrentUserId ? ProjectAccess.Allowed : ProjectAccess.Forbidden;
    }

    private ObjectResult ProjectAccessError(ProjectAccess access) => access == ProjectAccess.NotFound
        ? NotFound(new { error = "Project was not found." })
        : StatusCode(StatusCodes.Status403Forbidden,
            new { error = "This project is assigned to another Surveyor." });

    private Task<bool> AreaExists(Guid projectId, Guid areaId, CancellationToken cancellationToken) =>
        db.ProjectAreas.AsNoTracking().AnyAsync(
            x => x.Id == areaId && x.ProjectId == projectId, cancellationToken);

    private async Task TouchProject(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects.SingleAsync(x => x.Id == projectId, cancellationToken);
        project.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private BadRequestObjectResult? ValidateRequest(UpsertAreaEquipmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TagNumber) || string.IsNullOrWhiteSpace(request.Type))
            return BadRequest(new { error = "TagNumber and Type are required." });
        if (request.XPercent is < 0 or > 100 || request.YPercent is < 0 or > 100)
            return BadRequest(new { error = "XPercent and YPercent must be between 0 and 100." });
        return null;
    }

    private static SurveyorAreaSummaryResponse ToAreaSummary(Guid projectId, ProjectArea area) => new(
        area.Id,
        area.AreaName,
        GetLayoutImagePath(projectId, area),
        area.Photos.Count,
        area.EquipmentPins.Count);

    private static string? GetLayoutImagePath(Guid projectId, ProjectArea area)
    {
        var layoutPhoto = area.Photos.FirstOrDefault(x => x.FilePath == area.LayoutImagePath)
                          ?? area.Photos.OrderBy(x => x.UploadedAt).ThenBy(x => x.Id).FirstOrDefault();
        return layoutPhoto is null
            ? null
            : $"/api/projects/{projectId}/areas/{area.Id}/photos/{layoutPhoto.Id}";
    }

    private static AreaEquipmentResponse ToEquipmentResponse(AreaEquipment equipment) => new(
        equipment.Id,
        equipment.ProjectId,
        equipment.AreaId,
        equipment.XPercent,
        equipment.YPercent,
        equipment.TagNumber,
        equipment.EquipmentCode,
        equipment.Type,
        equipment.CircuitId,
        equipment.Location,
        equipment.Manufacturer,
        equipment.ProtectionDetails,
        equipment.CertificateNumber,
        equipment.CreatedByUserId,
        equipment.CreatedAt,
        equipment.UpdatedAt);

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private enum ProjectAccess { Allowed, NotFound, Forbidden }
}
