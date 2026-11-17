using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

public partial class ProjectController
{
    private IQueryable<SurveyFile> CommonLayouts(Guid projectId) =>
        db.Files.Where(x => x.ProjectId == projectId && x.Category == FileCategory.ProjectCommonLayout);

    [HttpGet("{projectId:guid}/common-layouts")]
    public async Task<IActionResult> ListCommonLayouts(Guid projectId, CancellationToken ct)
    {
        if (!await CanAccessProject(projectId, false, ct)) return NotFound();
        var rows = await CommonLayouts(projectId).AsNoTracking().OrderBy(x => x.UploadedAt).ThenBy(x => x.Id).ToListAsync(ct);
        return Ok(rows.Select(x => CommonLayoutView(projectId, x)));
    }

    [HttpPost("{projectId:guid}/common-layouts")]
    [Authorize(Roles = "Admin,Manager,Surveyor")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(220_000_000)]
    public async Task<IActionResult> UploadCommonLayouts(Guid projectId, [FromForm] UploadProjectAreaPhotosRequest request, CancellationToken ct)
    {
        if (!await CanAccessProject(projectId, true, ct)) return NotFound();
        if (request.Files.Count == 0 || request.Files.Count > photoStorage.MaxFilesPerRequest)
            return BadRequest(new {error = $"Choose between 1 and {photoStorage.MaxFilesPerRequest} layout images."});

        var rows = new List<SurveyFile>();
        try
        {
            foreach (var photo in request.Files)
            {
                var stored = await photoStorage.SaveCommonLayoutAsync(photo, projectId, ct);
                var contentType = Path.GetExtension(stored.OriginalFileName).ToLowerInvariant() switch
                {
                    ".pdf" => "application/pdf",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };
                rows.Add(new SurveyFile {ProjectId = projectId, Category = FileCategory.ProjectCommonLayout,
                    FileName = stored.OriginalFileName, StorageKey = stored.RelativePath,
                    ContentType = contentType, UploadedById = CurrentUserId});
            }
            db.Files.AddRange(rows);
            await db.SaveChangesAsync(ct);
        }
        catch (ProjectPhotoValidationException ex)
        {
            await DeleteStoredPhotos(rows.Select(x => x.StorageKey));
            return BadRequest(new {error = ex.Message});
        }
        catch
        {
            await DeleteStoredPhotos(rows.Select(x => x.StorageKey));
            throw;
        }
        return StatusCode(201, rows.Select(x => CommonLayoutView(projectId, x)));
    }

    [HttpGet("{projectId:guid}/common-layouts/{photoId:guid}")]
    public async Task<IActionResult> DownloadCommonLayout(Guid projectId, Guid photoId, CancellationToken ct)
    {
        if (!await CanAccessProject(projectId, false, ct)) return NotFound();
        var photo = await CommonLayouts(projectId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == photoId, ct);
        if (photo is null) return NotFound();
        var content = await photoStorage.OpenReadAsync(photo.StorageKey, ct);
        return content is null ? NotFound() : File(content.Stream, photo.ContentType);
    }

    [HttpDelete("{projectId:guid}/common-layouts/{photoId:guid}")]
    [Authorize(Roles = "Admin,Manager,Surveyor")]
    public async Task<IActionResult> DeleteCommonLayout(Guid projectId, Guid photoId, CancellationToken ct)
    {
        if (!await CanAccessProject(projectId, true, ct)) return NotFound();
        var photo = await CommonLayouts(projectId).SingleOrDefaultAsync(x => x.Id == photoId, ct);
        if (photo is null) return NotFound();
        if (await db.AreaEquipment.AnyAsync(x => x.LayoutImageKey == $"common:{photoId}", ct))
            return Conflict(new { error = "This image has saved equipment markings. Move those markings to another image before removing it." });
        db.Files.Remove(photo);
        await db.SaveChangesAsync(ct);
        await DeleteStoredPhotos([photo.StorageKey]);
        return NoContent();
    }

    private static object CommonLayoutView(Guid projectId, SurveyFile file) =>
        new {file.Id, file.FileName, file.UploadedAt, DownloadUrl = $"/api/projects/{projectId:D}/common-layouts/{file.Id:D}"};
}
