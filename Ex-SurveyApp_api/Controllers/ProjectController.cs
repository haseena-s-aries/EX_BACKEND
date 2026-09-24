using System.Security.Claims;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectController(
    SurveyDbContext db,
    IProjectPhotoStorage photoStorage,
    ILogger<ProjectController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ProjectResponse>> CreateProject(
        UpsertProjectRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateProjectRequest(request, null, cancellationToken);
        if (validationError is not null)
            return validationError;

        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            ProjectName = request.ProjectName.Trim(),
            Sector = request.Sector.Trim(),
            JobNo = request.JobNo.Trim(),
            Location = request.Location.Trim(),
            VesselOrSiteName = request.VesselOrSiteName.Trim(),
            AssignedSurveyorId = request.AssignedSurveyorId,
            AssignedQcId = request.AssignedQcId,
            AssignedClientId = request.AssignedClientId,
            CreatedByUserId = CurrentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Projects.Add(project);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { error = "JobNo already exists." });
        }

        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, ToResponse(project));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectSummaryResponse>>> GetProjects(CancellationToken cancellationToken)
    {
        var projects = await AccessibleProjects()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new ProjectSummaryResponse(
                x.Id, x.ProjectName, x.Sector, x.JobNo, x.Location, x.VesselOrSiteName,
                x.AssignedSurveyorId, x.AssignedQcId, x.AssignedClientId, x.CreatedByUserId,
                x.CreatedAt, x.UpdatedAt, x.Areas.Count))
            .ToListAsync(cancellationToken);
        return Ok(projects);
    }

    [HttpGet("assignment-options")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<ProjectAssignableUserResponse>>> GetAssignmentOptions(
        CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Where(x => x.IsActive && x.ApprovalStatus == ApprovalStatus.Approved &&
                        (x.Role == UserRole.Surveyor || x.Role == UserRole.Qc || x.Role == UserRole.Client))
            .OrderBy(x => x.Role)
            .ThenBy(x => x.DisplayName)
            .Select(x => new ProjectAssignableUserResponse(x.Id, x.DisplayName, x.Email, x.Role))
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> GetProject(Guid id, CancellationToken cancellationToken)
    {
        var project = await AccessibleProjects()
            .Include(x => x.Areas)
            .ThenInclude(x => x.Photos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return project is null ? NotFound(new { error = "Project was not found." }) : Ok(ToResponse(project));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ProjectResponse>> UpdateProject(
        Guid id, UpsertProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (project is null)
            return NotFound(new { error = "Project was not found." });

        var validationError = await ValidateProjectRequest(request, id, cancellationToken);
        if (validationError is not null)
            return validationError;

        project.ProjectName = request.ProjectName.Trim();
        project.Sector = request.Sector.Trim();
        project.JobNo = request.JobNo.Trim();
        project.Location = request.Location.Trim();
        project.VesselOrSiteName = request.VesselOrSiteName.Trim();
        project.AssignedSurveyorId = request.AssignedSurveyorId;
        project.AssignedQcId = request.AssignedQcId;
        project.AssignedClientId = request.AssignedClientId;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { error = "JobNo already exists." });
        }

        return Ok(ToResponse(project));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .Include(x => x.Areas)
            .ThenInclude(x => x.Photos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (project is null)
            return NotFound(new { error = "Project was not found." });

        var photoPaths = project.Areas.SelectMany(x => x.Photos).Select(x => x.FilePath).ToArray();
        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);
        await DeleteStoredPhotos(photoPaths);
        return NoContent();
    }

    [HttpPost("{projectId:guid}/areas")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ProjectAreaResponse>> CreateArea(
        Guid projectId, UpsertProjectAreaRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AreaName))
            return BadRequest(new { error = "AreaName is required." });
        var project = await db.Projects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null)
            return NotFound(new { error = "Project was not found." });

        var areaName = request.AreaName.Trim();
        if (await db.ProjectAreas.AnyAsync(x => x.ProjectId == projectId && x.AreaName == areaName, cancellationToken))
            return Conflict(new { error = "An area with this name already exists in the project." });

        var area = new ProjectArea { ProjectId = projectId, AreaName = areaName };
        db.ProjectAreas.Add(area);
        project.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { error = "An area with this name already exists in the project." });
        }
        return StatusCode(StatusCodes.Status201Created, ToResponse(area));
    }

    [HttpPut("{projectId:guid}/areas/{areaId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ProjectAreaResponse>> UpdateArea(
        Guid projectId, Guid areaId, UpsertProjectAreaRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AreaName))
            return BadRequest(new { error = "AreaName is required." });

        var area = await db.ProjectAreas.FirstOrDefaultAsync(
            x => x.Id == areaId && x.ProjectId == projectId, cancellationToken);
        if (area is null)
            return NotFound(new { error = "Project area was not found." });

        var areaName = request.AreaName.Trim();
        if (await db.ProjectAreas.AnyAsync(
                x => x.ProjectId == projectId && x.Id != areaId && x.AreaName == areaName, cancellationToken))
            return Conflict(new { error = "An area with this name already exists in the project." });

        area.AreaName = areaName;
        await TouchProject(projectId, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { error = "An area with this name already exists in the project." });
        }
        return Ok(ToResponse(area));
    }

    [HttpDelete("{projectId:guid}/areas/{areaId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteArea(Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        var area = await db.ProjectAreas.Include(x => x.Photos).FirstOrDefaultAsync(
            x => x.Id == areaId && x.ProjectId == projectId, cancellationToken);
        if (area is null)
            return NotFound(new { error = "Project area was not found." });

        var photoPaths = area.Photos.Select(x => x.FilePath).ToArray();
        db.ProjectAreas.Remove(area);
        await TouchProject(projectId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await DeleteStoredPhotos(photoPaths);
        return NoContent();
    }

    [HttpPost("{projectId:guid}/areas/{areaId:guid}/photos")]
    [Authorize(Roles = "Admin,Manager,Surveyor")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(220_000_000)]
    public async Task<ActionResult<IEnumerable<ProjectAreaPhotoResponse>>> UploadAreaPhotos(
        Guid projectId, Guid areaId, [FromForm] UploadProjectAreaPhotosRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Files.Count == 0)
            return BadRequest(new { error = "At least one image is required." });
        if (request.Files.Count > photoStorage.MaxFilesPerRequest)
            return BadRequest(new { error = $"A maximum of {photoStorage.MaxFilesPerRequest} images can be uploaded at once." });
        if (!await CanAccessProject(projectId, allowSurveyorWrite: true, cancellationToken))
            return NotFound(new { error = "Project was not found or is not assigned to you." });
        if (!await db.ProjectAreas.AnyAsync(x => x.Id == areaId && x.ProjectId == projectId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var storedFiles = new List<StoredProjectPhoto>();
        try
        {
            foreach (var file in request.Files)
                storedFiles.Add(await photoStorage.SaveAsync(file, projectId, areaId, cancellationToken));
        }
        catch (ProjectPhotoValidationException ex)
        {
            await DeleteStoredPhotos(storedFiles.Select(x => x.RelativePath));
            return BadRequest(new { error = ex.Message });
        }
        catch
        {
            await DeleteStoredPhotos(storedFiles.Select(x => x.RelativePath));
            throw;
        }

        var now = DateTimeOffset.UtcNow;
        var photos = storedFiles.Select(x => new ProjectAreaPhoto
        {
            ProjectAreaId = areaId,
            FileName = x.OriginalFileName,
            FilePath = x.RelativePath,
            UploadedByUserId = CurrentUserId,
            UploadedAt = now
        }).ToList();
        db.ProjectAreaPhotos.AddRange(photos);
        await TouchProject(projectId, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteStoredPhotos(storedFiles.Select(x => x.RelativePath));
            throw;
        }

        return StatusCode(StatusCodes.Status201Created, photos.Select(x => ToResponse(x, projectId)).ToArray());
    }

    [HttpGet("{projectId:guid}/areas/{areaId:guid}/photos")]
    public async Task<ActionResult<IEnumerable<ProjectAreaPhotoResponse>>> GetAreaPhotos(
        Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        if (!await CanAccessProject(projectId, allowSurveyorWrite: false, cancellationToken))
            return NotFound(new { error = "Project was not found or is not assigned to you." });
        if (!await db.ProjectAreas.AnyAsync(x => x.Id == areaId && x.ProjectId == projectId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var photos = await db.ProjectAreaPhotos.AsNoTracking()
            .Where(x => x.ProjectAreaId == areaId)
            .OrderByDescending(x => x.UploadedAt)
            .ToListAsync(cancellationToken);
        return Ok(photos.Select(x => ToResponse(x, projectId)));
    }

    [HttpGet("{projectId:guid}/areas/{areaId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> DownloadAreaPhoto(
        Guid projectId, Guid areaId, Guid photoId, CancellationToken cancellationToken)
    {
        if (!await CanAccessProject(projectId, allowSurveyorWrite: false, cancellationToken))
            return NotFound(new { error = "Project was not found or is not assigned to you." });
        if (!await db.ProjectAreas.AnyAsync(x => x.Id == areaId && x.ProjectId == projectId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var photo = await db.ProjectAreaPhotos.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == photoId && x.ProjectAreaId == areaId, cancellationToken);
        if (photo is null)
            return NotFound(new { error = "Project area photo was not found." });

        var content = await photoStorage.OpenReadAsync(photo.FilePath, cancellationToken);
        if (content is null)
            return NotFound(new { error = "Photo file was not found in storage." });

        return File(content.Stream, content.ContentType, enableRangeProcessing: true);
    }

    [HttpDelete("{projectId:guid}/areas/{areaId:guid}/photos/{photoId:guid}")]
    [Authorize(Roles = "Admin,Manager,Surveyor")]
    public async Task<IActionResult> DeleteAreaPhoto(
        Guid projectId, Guid areaId, Guid photoId, CancellationToken cancellationToken)
    {
        if (!await CanAccessProject(projectId, allowSurveyorWrite: true, cancellationToken))
            return NotFound(new { error = "Project was not found or is not assigned to you." });
        if (!await db.ProjectAreas.AnyAsync(x => x.Id == areaId && x.ProjectId == projectId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var photo = await db.ProjectAreaPhotos.FirstOrDefaultAsync(
            x => x.Id == photoId && x.ProjectAreaId == areaId, cancellationToken);
        if (photo is null)
            return NotFound(new { error = "Project area photo was not found." });

        db.ProjectAreaPhotos.Remove(photo);
        await TouchProject(projectId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await DeleteStoredPhotos([photo.FilePath]);
        return NoContent();
    }

    private IQueryable<Project> AccessibleProjects()
    {
        var query = db.Projects.AsNoTracking();
        return CurrentRole switch
        {
            nameof(UserRole.Admin) or nameof(UserRole.Manager) => query,
            nameof(UserRole.Surveyor) => query.Where(x => x.AssignedSurveyorId == CurrentUserId),
            nameof(UserRole.Qc) => query.Where(x => x.AssignedQcId == CurrentUserId),
            nameof(UserRole.Client) => query.Where(x => x.AssignedClientId == CurrentUserId),
            _ => query.Where(_ => false)
        };
    }

    private async Task<bool> CanAccessProject(
        Guid projectId, bool allowSurveyorWrite, CancellationToken cancellationToken)
    {
        if (CurrentRole is nameof(UserRole.Admin) or nameof(UserRole.Manager))
            return await db.Projects.AnyAsync(x => x.Id == projectId, cancellationToken);
        if (allowSurveyorWrite && CurrentRole != nameof(UserRole.Surveyor))
            return false;
        return await AccessibleProjects().AnyAsync(x => x.Id == projectId, cancellationToken);
    }

    private async Task<ObjectResult?> ValidateProjectRequest(
        UpsertProjectRequest request, Guid? projectId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectName) || string.IsNullOrWhiteSpace(request.Sector) ||
            string.IsNullOrWhiteSpace(request.JobNo) || string.IsNullOrWhiteSpace(request.Location) ||
            string.IsNullOrWhiteSpace(request.VesselOrSiteName))
            return BadRequest(new { error = "ProjectName, Sector, JobNo, Location, and VesselOrSiteName are required." });
        if (request.AssignedSurveyorId == Guid.Empty || request.AssignedQcId == Guid.Empty ||
            request.AssignedClientId == Guid.Empty)
            return BadRequest(new { error = "Surveyor, QC, and Client assignments are required." });

        var jobNo = request.JobNo.Trim();
        if (await db.Projects.AnyAsync(x => x.JobNo == jobNo && x.Id != projectId, cancellationToken))
            return Conflict(new { error = "JobNo already exists." });

        var ids = new[] { request.AssignedSurveyorId, request.AssignedQcId, request.AssignedClientId };
        var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var assignmentError = ValidateAssignment(users, request.AssignedSurveyorId, UserRole.Surveyor, "AssignedSurveyorId")
                              ?? ValidateAssignment(users, request.AssignedQcId, UserRole.Qc, "AssignedQcId")
                              ?? ValidateAssignment(users, request.AssignedClientId, UserRole.Client, "AssignedClientId");
        return assignmentError is null ? null : BadRequest(new { error = assignmentError });
    }

    private static string? ValidateAssignment(
        IReadOnlyDictionary<Guid, User> users, Guid id, UserRole expectedRole, string fieldName)
    {
        if (!users.TryGetValue(id, out var user))
            return $"{fieldName} does not refer to an existing user.";
        if (user.Role != expectedRole)
            return $"{fieldName} must refer to a {expectedRole} user.";
        if (!user.IsActive || user.ApprovalStatus != ApprovalStatus.Approved)
            return $"{fieldName} must refer to an active, approved user.";
        return null;
    }

    private async Task DeleteStoredPhotos(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                await photoStorage.DeleteAsync(path);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to delete project photo {PhotoPath}", path);
            }
        }
    }

    private async Task TouchProject(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects.FirstAsync(x => x.Id == projectId, cancellationToken);
        project.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static ProjectResponse ToResponse(Project project) => new(
        project.Id, project.ProjectName, project.Sector, project.JobNo, project.Location,
        project.VesselOrSiteName, project.AssignedSurveyorId, project.AssignedQcId,
        project.AssignedClientId, project.CreatedByUserId, project.CreatedAt, project.UpdatedAt,
        project.Areas.OrderBy(x => x.CreatedAt).Select(ToResponse).ToArray());

    private static ProjectAreaResponse ToResponse(ProjectArea area) => new(
        area.Id, area.ProjectId, area.AreaName, area.CreatedAt,
        area.Photos.OrderByDescending(x => x.UploadedAt).Select(x => ToResponse(x, area.ProjectId)).ToArray());

    private static ProjectAreaPhotoResponse ToResponse(ProjectAreaPhoto photo, Guid projectId) => new(
        photo.Id, photo.ProjectAreaId, photo.FileName, photo.FilePath,
        $"/api/projects/{projectId}/areas/{photo.ProjectAreaId}/photos/{photo.Id}",
        photo.UploadedByUserId, photo.UploadedAt);
}
