

using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
    [Consumes("application/json")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ProjectResponse>> CreateProject(
        UpsertProjectRequest request, CancellationToken cancellationToken)
        => await CreateProjectCore(request, null, cancellationToken);

    [HttpPost]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = "Admin,Manager")]
    [RequestSizeLimit(220_000_000)]
    public Task<ActionResult<ProjectResponse>> CreateProjectWithPlan(
        [FromForm] UpsertProjectRequest request, [FromForm] IFormFile? hazardousAreaPlan,
        CancellationToken cancellationToken)
        => CreateProjectCore(request, hazardousAreaPlan, cancellationToken);

    private async Task<ActionResult<ProjectResponse>> CreateProjectCore(
        UpsertProjectRequest request, IFormFile? plan, CancellationToken cancellationToken)
    {
        var validationError = await ValidateProjectRequest(request, null, cancellationToken);
        if (validationError is not null)
            return validationError;

        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            ProjectName = request.ProjectName.Trim().ToUpperInvariant(),
            Sector = request.Sector.Trim(),
            JobNo = request.JobNo.Trim().ToUpperInvariant(),
            Location = request.Location.Trim().ToUpperInvariant(),
            VesselOrSiteName = request.VesselOrSiteName.Trim().ToUpperInvariant(),
            Status = request.Status,
            AssignedSurveyorId = request.AssignedSurveyorId,
            AssignedQcId = request.AssignedQcId,
            AssignedClientId = request.AssignedClientId,
            CreatedByUserId = CurrentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        if (plan is not null)
        {
            try { await SavePlan(project, plan, cancellationToken); }
            catch (ProjectPhotoValidationException ex) { return BadRequest(new { error = ex.Message }); }
        }
        db.Projects.Add(project);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            if (project.HazardousAreaPlanPath is not null) await DeleteStoredPhotos([project.HazardousAreaPlanPath]);
            return Conflict(new { error = "JobNo already exists." });
        }
        catch
        {
            if (project.HazardousAreaPlanPath is not null) await DeleteStoredPhotos([project.HazardousAreaPlanPath]);
            throw;
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
                x.Status, x.AssignedSurveyorId, x.AssignedQcId, x.AssignedClientId, x.CreatedByUserId,
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
            .Select(x => new ProjectAssignableUserResponse(x.Id, x.DisplayName, x.Email, x.Role,
                x.ManagerId, x.Manager == null ? null : x.Manager.DisplayName))
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> GetProject(Guid id, CancellationToken cancellationToken)
    {
        var project = await AccessibleProjects()
            .Include(x => x.Areas)
            .ThenInclude(x => x.Photos)
            .Include(x => x.Areas)
            .ThenInclude(x => x.EquipmentPins)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (project is null)
            return NotFound(new { error = "Project was not found." });
        var equipmentIds = project.Areas.SelectMany(x => x.EquipmentPins).Select(x => x.Id).ToArray();
        var inspectionRows = await (
            from inspection in db.SurveySessions.AsNoTracking()
            where inspection.AreaEquipmentId.HasValue && equipmentIds.Contains(inspection.AreaEquipmentId.Value)
            join review in db.QcReviews.AsNoTracking() on inspection.Id equals review.SurveySessionId into reviewGroup
            from review in reviewGroup.DefaultIfEmpty()
            select new
            {
                EquipmentId = inspection.AreaEquipmentId!.Value,
                inspection.Status,
                QcStatus = (QcReviewStatus?)review.Status
            }).ToListAsync(cancellationToken);
        var inspectionStatuses = inspectionRows.ToDictionary(
            x => x.EquipmentId,
            x => GetInspectionStatus(x.Status, x.QcStatus));
        return Ok(ToResponse(project, inspectionStatuses));
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ProjectResponse>> UpdateProject(
        Guid id, UpsertProjectRequest request, CancellationToken cancellationToken)
        => await UpdateProjectCore(id, request, null, cancellationToken);

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = "Admin,Manager")]
    [RequestSizeLimit(220_000_000)]
    public Task<ActionResult<ProjectResponse>> UpdateProjectWithPlan(
        Guid id, [FromForm] UpsertProjectRequest request, [FromForm] IFormFile? hazardousAreaPlan,
        CancellationToken cancellationToken)
        => UpdateProjectCore(id, request, hazardousAreaPlan, cancellationToken);

    private async Task<ActionResult<ProjectResponse>> UpdateProjectCore(
        Guid id, UpsertProjectRequest request, IFormFile? plan, CancellationToken cancellationToken)
    {
        var project = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (project is null)
            return NotFound(new { error = "Project was not found." });

        var validationError = await ValidateProjectRequest(request, id, cancellationToken);
        if (validationError is not null)
            return validationError;

        var previousPlan = project.HazardousAreaPlanPath;
        var previousFileName = project.HazardousAreaPlanFileName;
        if (plan is not null)
        {
            try { await SavePlan(project, plan, cancellationToken); }
            catch (ProjectPhotoValidationException ex) { return BadRequest(new { error = ex.Message }); }
        }
        project.ProjectName = request.ProjectName.Trim().ToUpperInvariant();
        project.Sector = request.Sector.Trim();
        project.JobNo = request.JobNo.Trim().ToUpperInvariant();
        project.Location = request.Location.Trim().ToUpperInvariant();
        project.VesselOrSiteName = request.VesselOrSiteName.Trim().ToUpperInvariant();
        project.Status = request.Status;
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
            await RollBackPlan();
            return Conflict(new { error = "JobNo already exists." });
        }
        catch { await RollBackPlan(); throw; }
        if (plan is not null && previousPlan is not null) await DeleteStoredPhotos([previousPlan]);
        return Ok(ToResponse(project));

        async Task RollBackPlan()
        {
            if (plan is null) return;
            if (project.HazardousAreaPlanPath is not null) await DeleteStoredPhotos([project.HazardousAreaPlanPath]);
            project.HazardousAreaPlanPath = previousPlan;
            project.HazardousAreaPlanFileName = previousFileName;
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .Include(x => x.Areas)
            .ThenInclude(x => x.Photos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (project is null)
            return NotFound(new { error = "Project was not found." });

        var equipment = await db.AreaEquipment.Where(x => x.ProjectId == id).ToListAsync(cancellationToken);
        var equipmentIds = equipment.Select(x => x.Id).ToArray();
        var inspections = await db.SurveySessions.Where(x => x.AreaEquipmentId.HasValue &&
            equipmentIds.Contains(x.AreaEquipmentId.Value)).ToListAsync(cancellationToken);
        var inspectionIds = inspections.Select(x => x.Id).ToArray();
        var findings = await db.Findings.Where(x => inspectionIds.Contains(x.SurveySessionId)).ToListAsync(cancellationToken);
        var findingIds = findings.Select(x => x.Id).ToArray();
        var files = await db.Files.Where(x =>
            (x.SurveySessionId.HasValue && inspectionIds.Contains(x.SurveySessionId.Value)) ||
            (x.FindingId.HasValue && findingIds.Contains(x.FindingId.Value))).ToListAsync(cancellationToken);
        var fileIds = files.Select(x => x.Id).ToArray();
        if (await db.ReportRevisions.AnyAsync(x => x.FileId.HasValue && fileIds.Contains(x.FileId.Value), cancellationToken))
            return Conflict(new {error = "This project has files linked to reports. Remove those report links before deleting the project."});

        var photoPaths = project.Areas.SelectMany(x => x.Photos).Select(x => x.FilePath)
            .Concat(files.Select(x => x.StorageKey)).ToArray();
        db.Files.RemoveRange(files);
        db.SurveyAnswers.RemoveRange(await db.SurveyAnswers.Where(x => inspectionIds.Contains(x.SurveySessionId)).ToListAsync(cancellationToken));
        db.QcReviews.RemoveRange(await db.QcReviews.Where(x => inspectionIds.Contains(x.SurveySessionId)).ToListAsync(cancellationToken));
        db.Findings.RemoveRange(findings);
        db.SurveySessions.RemoveRange(inspections);
        db.AreaEquipment.RemoveRange(equipment);
        db.ProjectAreaPhotos.RemoveRange(project.Areas.SelectMany(x => x.Photos));
        db.ProjectAreas.RemoveRange(project.Areas);
        db.Projects.Remove(project);
        db.AuditLogs.Add(new AuditLog {ActorId = CurrentUserId, EntityType = "Project", EntityId = id,
            Action = "Project Deleted", Comments = $"{project.ProjectName}; {project.Areas.Count} areas; {equipment.Count} equipment; {inspections.Count} inspections"});
        await db.SaveChangesAsync(cancellationToken);
        await DeleteStoredPhotos(photoPaths);
        if (project.HazardousAreaPlanPath is not null) await DeleteStoredPhotos([project.HazardousAreaPlanPath]);
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
        var area = await db.ProjectAreas.FirstOrDefaultAsync(
            x => x.Id == areaId && x.ProjectId == projectId, cancellationToken);
        if (area is null)
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
        area.LayoutImagePath ??= await db.ProjectAreaPhotos.AsNoTracking()
            .Where(x => x.ProjectAreaId == areaId)
            .OrderBy(x => x.UploadedAt)
            .ThenBy(x => x.Id)
            .Select(x => x.FilePath)
            .FirstOrDefaultAsync(cancellationToken) ?? photos[0].FilePath;
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
        var area = await db.ProjectAreas.FirstOrDefaultAsync(
            x => x.Id == areaId && x.ProjectId == projectId, cancellationToken);
        if (area is null)
            return NotFound(new { error = "Project area was not found." });

        var photo = await db.ProjectAreaPhotos.FirstOrDefaultAsync(
            x => x.Id == photoId && x.ProjectAreaId == areaId, cancellationToken);
        if (photo is null)
            return NotFound(new { error = "Project area photo was not found." });

        if (area.LayoutImagePath == photo.FilePath)
        {
            area.LayoutImagePath = await db.ProjectAreaPhotos.AsNoTracking()
                .Where(x => x.ProjectAreaId == areaId && x.Id != photoId)
                .OrderBy(x => x.UploadedAt)
                .ThenBy(x => x.Id)
                .Select(x => x.FilePath)
                .FirstOrDefaultAsync(cancellationToken);
        }

        db.ProjectAreaPhotos.Remove(photo);
        await TouchProject(projectId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await DeleteStoredPhotos([photo.FilePath]);
        return NoContent();
    }

    [HttpGet("{id:guid}/hazardous-area-plan")]
    public async Task<IActionResult> DownloadHazardousAreaPlan(Guid id, CancellationToken cancellationToken)
    {
        var project = await AccessibleProjects().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (project?.HazardousAreaPlanPath is null) return NotFound();
        var content = await photoStorage.OpenReadAsync(project.HazardousAreaPlanPath, cancellationToken);
        if (content is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(content.Stream, content.ContentType, enableRangeProcessing: true);
    }

    private async Task SavePlan(Project project, IFormFile plan, CancellationToken cancellationToken)
    {
        var stored = await photoStorage.SaveHazardousAreaPlanAsync(plan, project.Id, cancellationToken);
        project.HazardousAreaPlanPath = stored.RelativePath;
        project.HazardousAreaPlanFileName = stored.OriginalFileName;
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
        if (request.AssignedSurveyorId == Guid.Empty || request.AssignedQcId == Guid.Empty)
            return BadRequest(new { error = "Surveyor and QC assignments are required." });
        if (!Enum.IsDefined(request.Status))
            return BadRequest(new { error = "Status must be Pending, Active, InProgress, Completed, or OnHold." });

        var jobNo = request.JobNo.Trim().ToUpperInvariant();
        if (await db.Projects.AnyAsync(x => x.JobNo == jobNo && x.Id != projectId, cancellationToken))
            return Conflict(new { error = "JobNo already exists." });

        var ids = new Guid?[] { request.AssignedSurveyorId, request.AssignedQcId, request.AssignedClientId };
        var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var assignmentError = ValidateAssignment(users, request.AssignedSurveyorId, UserRole.Surveyor, "AssignedSurveyorId")
                              ?? ValidateAssignment(users, request.AssignedQcId, UserRole.Qc, "AssignedQcId")
                              ?? (request.AssignedClientId.HasValue
                                  ? ValidateAssignment(users, request.AssignedClientId.Value, UserRole.Client, "AssignedClientId")
                                  : null);
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

    private static ProjectResponse ToResponse(
        Project project, IReadOnlyDictionary<Guid, EquipmentInspectionStatus>? inspectionStatuses = null) => new(
        project.Id, project.ProjectName, project.Sector, project.JobNo, project.Location,
        project.VesselOrSiteName, project.Status, project.AssignedSurveyorId, project.AssignedQcId,
        project.AssignedClientId, project.CreatedByUserId, project.CreatedAt, project.UpdatedAt,
        project.Areas.OrderBy(x => x.CreatedAt).Select(area => ToResponse(area, inspectionStatuses)).ToArray(),
        project.HazardousAreaPlanPath is null ? null : $"/api/projects/{project.Id}/hazardous-area-plan",
        project.HazardousAreaPlanFileName,
        project.HazardousAreaPlanPath is null ? null :
            Path.GetExtension(project.HazardousAreaPlanPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf" : "image/jpeg");

    private static ProjectAreaResponse ToResponse(
        ProjectArea area, IReadOnlyDictionary<Guid, EquipmentInspectionStatus>? inspectionStatuses = null) => new(
        area.Id, area.ProjectId, area.AreaName, GetLayoutImagePath(area), area.CreatedAt, area.EquipmentPins.Count,
        area.Photos.OrderByDescending(x => x.UploadedAt).Select(x => ToResponse(x, area.ProjectId)).ToArray(),
        area.EquipmentPins.OrderBy(x => x.SequenceNo).Select(x => new ProjectAreaEquipmentResponse(
            x.Id, x.SequenceNo, x.XPercent, x.YPercent, x.TagNumber, x.EquipmentCode, x.Type, x.CircuitId, x.Location,
            x.Manufacturer, x.ProtectionDetails, x.CertificateNumber, x.CreatedAt,
            inspectionStatuses is not null && inspectionStatuses.TryGetValue(x.Id, out var status)
                ? status.ToString()
                : EquipmentInspectionStatus.NotStarted.ToString())).ToArray());

    private static EquipmentInspectionStatus GetInspectionStatus(SurveyStatus surveyStatus, QcReviewStatus? qcStatus) =>
        QcWorkflowStatus.InspectionStatus(surveyStatus, qcStatus);

    private static string? GetLayoutImagePath(ProjectArea area)
    {
        var layoutPhoto = area.Photos.FirstOrDefault(x => x.FilePath == area.LayoutImagePath)
                          ?? area.Photos.OrderBy(x => x.UploadedAt).ThenBy(x => x.Id).FirstOrDefault();
        return layoutPhoto is null
            ? null
            : $"/api/projects/{area.ProjectId}/areas/{area.Id}/photos/{layoutPhoto.Id}";
    }

    private static ProjectAreaPhotoResponse ToResponse(ProjectAreaPhoto photo, Guid projectId) => new(
        photo.Id, photo.ProjectAreaId, photo.FileName, photo.FilePath,
        $"/api/projects/{projectId}/areas/{photo.ProjectAreaId}/photos/{photo.Id}",
        photo.UploadedByUserId, photo.UploadedAt);
}
