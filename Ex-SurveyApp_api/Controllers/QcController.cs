using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = "Qc,Manager,Admin")]
[Route("api/qc")]
public class QcController(SurveyDbContext db, IProjectPhotoStorage photoStorage) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<QcDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        var rows = await SubmittedInspectionsForCurrentQc().ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        return Ok(new QcDashboardResponse(
            rows.Count(x => x.ReviewStatus == null || x.ReviewStatus == QcReviewStatus.PendingReview),
            rows.Count(x => x.ReviewStatus == QcReviewStatus.ChangesRequested),
            rows.Count(x => x.ReviewStatus == QcReviewStatus.Approved),
            rows.Count(x => x.DueDate < now && x.ReviewStatus != QcReviewStatus.Approved)));
    }

    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<QcProjectSummaryResponse>>> GetProjects(
        CancellationToken cancellationToken, [FromQuery] bool pendingOnly = false)
    {
        var scopedProjects = AccessibleProjects();
        if (pendingOnly)
            scopedProjects = scopedProjects.Where(p => OpenInspectionProjectIds().Contains(p.Id));
        var projects = await (
            from project in scopedProjects
            join surveyor in db.Users.AsNoTracking() on project.AssignedSurveyorId equals surveyor.Id
            join assignedQc in db.Users.AsNoTracking() on project.AssignedQcId equals assignedQc.Id
            orderby project.UpdatedAt descending
            select new
            {
                project.Id,
                project.ProjectName,
                project.JobNo,
                project.Location,
                SurveyorName = surveyor.DisplayName,
                AssignedQcName = assignedQc.DisplayName,
                project.Status
            }).ToListAsync(cancellationToken);

        var projectIds = projects.Select(x => x.Id).ToArray();
        var reviews = await LoadReviewSnapshots(projectIds, cancellationToken);
        var response = projects.Select(project =>
        {
            var projectReviews = reviews.Where(x => x.ProjectId == project.Id).ToArray();
            return new QcProjectSummaryResponse(
                project.Id,
                project.ProjectName,
                project.JobNo,
                project.Location,
                project.SurveyorName,
                project.Status,
                AggregateStatus(projectReviews),
                projectReviews.Select(x => x.SubmittedAt).Where(x => x.HasValue).Max(),
                EarliestOpenDueDate(projectReviews), project.AssignedQcName);
        });

        return Ok(response);
    }

    [HttpGet("projects/{projectId:guid}")]
    public async Task<ActionResult<QcProjectDetailsResponse>> GetProject(
        Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking()
            .Include(x => x.Areas)
                .ThenInclude(x => x.Photos)
            .Include(x => x.Areas)
                .ThenInclude(x => x.EquipmentPins)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null)
            return NotFound(new { error = "Project was not found." });
        if (!await CanAccessProject(project.Id, cancellationToken))
            return ProjectForbidden();

        var userIds = new Guid?[] { project.AssignedSurveyorId, project.AssignedQcId, project.AssignedClientId };
        var names = await db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var reviews = await LoadReviewSnapshots([projectId], cancellationToken);

        var areas = project.Areas.OrderBy(x => x.CreatedAt).Select(area =>
        {
            var areaReviews = reviews.Where(x => x.AreaId == area.Id).ToArray();
            return new QcProjectAreaSummaryResponse(
                area.Id,
                area.AreaName,
                GetLayoutImagePath(projectId, area),
                area.EquipmentPins.Count,
                CountStatus(areaReviews, QcReviewStatus.PendingReview),
                CountStatus(areaReviews, QcReviewStatus.Approved),
                CountStatus(areaReviews, QcReviewStatus.ChangesRequested));
        }).ToArray();

        return Ok(new QcProjectDetailsResponse(
            project.Id,
            project.ProjectName,
            project.Sector,
            project.JobNo,
            project.Location,
            project.VesselOrSiteName,
            project.Status,
            project.CreatedAt,
            names.GetValueOrDefault(project.AssignedSurveyorId, string.Empty),
            names.GetValueOrDefault(project.AssignedQcId, string.Empty),
            project.AssignedClientId.HasValue ? names.GetValueOrDefault(project.AssignedClientId.Value, string.Empty) : "Not assigned",
            areas,
            project.Areas.Sum(x => x.EquipmentPins.Count),
            CountStatus(reviews, QcReviewStatus.PendingReview),
            CountStatus(reviews, QcReviewStatus.Approved),
            CountStatus(reviews, QcReviewStatus.ChangesRequested)));
    }

    [HttpGet("projects/{projectId:guid}/areas/{areaId:guid}")]
    public async Task<ActionResult<QcAreaDetailsResponse>> GetArea(
        Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);

        var area = await db.ProjectAreas.AsNoTracking()
            .Include(x => x.Photos)
            .Include(x => x.EquipmentPins)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == areaId && x.ProjectId == projectId, cancellationToken);
        if (area is null)
            return NotFound(new { error = "Project area was not found." });

        var equipmentIds = area.EquipmentPins.Select(x => x.Id).ToArray();
        var inspections = await (
            from inspection in db.SurveySessions.AsNoTracking()
            where inspection.AreaEquipmentId.HasValue && equipmentIds.Contains(inspection.AreaEquipmentId.Value)
            join review in db.QcReviews.AsNoTracking() on inspection.Id equals review.SurveySessionId into reviewGroup
            from review in reviewGroup.DefaultIfEmpty()
            select new
            {
                EquipmentId = inspection.AreaEquipmentId!.Value,
                InspectionId = inspection.Id,
                ReviewStatus = (QcReviewStatus?)review.Status
            }).ToDictionaryAsync(x => x.EquipmentId, cancellationToken);

        var equipment = area.EquipmentPins.OrderBy(x => x.SequenceNo).Select(pin =>
        {
            inspections.TryGetValue(pin.Id, out var inspection);
            return new QcAreaEquipmentResponse(
                pin.Id,
                inspection?.InspectionId,
                pin.SequenceNo,
                pin.XPercent,
                pin.YPercent,
                pin.TagNumber,
                pin.EquipmentCode,
                pin.Type,
                pin.CircuitId,
                pin.Location,
                pin.Manufacturer,
                pin.ProtectionDetails,
                pin.CertificateNumber,
                pin.RfidTagUid,
                pin.CreatedByUserId,
                pin.CreatedAt,
                pin.UpdatedAt,
                inspection?.ReviewStatus ?? QcReviewStatus.PendingReview);
        }).ToArray();

        return Ok(new QcAreaDetailsResponse(
            area.Id, area.AreaName, GetLayoutImagePath(projectId, area), equipment));
    }

    [HttpGet("equipment/{equipmentId:guid}/inspection")]
    public async Task<ActionResult<QcEquipmentInspectionResponse>> GetEquipmentInspection(
        Guid equipmentId, CancellationToken cancellationToken)
    {
        var context = await GetInspectionByEquipment(equipmentId, cancellationToken);
        if (context is null)
            return NotFound(new { error = "A submitted inspection was not found for this equipment." });
        if (!await CanAccessProject(context.Project.Id, cancellationToken))
            return ProjectForbidden();

        return Ok(await BuildInspectionResponse(context, cancellationToken));
    }

    [HttpGet("inspections/{inspectionId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> DownloadInspectionPhoto(
        Guid inspectionId, Guid photoId, CancellationToken cancellationToken)
    {
        var context = await GetInspection(inspectionId, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Inspection was not found." });
        if (!await CanAccessProject(context.Project.Id, cancellationToken))
            return ProjectForbidden();

        var photo = await db.Files.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == photoId && x.SurveySessionId == inspectionId &&
                 (x.Category == FileCategory.EquipmentPhoto || x.Category == FileCategory.FindingPhoto),
            cancellationToken);
        if (photo is null)
            return NotFound(new { error = "Inspection photo was not found." });

        var content = await photoStorage.OpenReadAsync(photo.StorageKey, cancellationToken);
        return content is null
            ? NotFound(new { error = "Inspection photo file was not found." })
            : File(content.Stream, content.ContentType, enableRangeProcessing: true);
    }

    [HttpPost("inspections/{inspectionId:guid}/start-review")]
    public async Task<ActionResult<QcReviewResponse>> StartReview(
        Guid inspectionId, CancellationToken cancellationToken)
    {
        var context = await GetInspection(inspectionId, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Inspection was not found." });
        if (!await CanAccessProject(context.Project.Id, cancellationToken))
            return ProjectForbidden();
        if (context.Inspection.Status != SurveyStatus.Submitted)
            return Conflict(new { error = "Only a submitted inspection can enter QC review." });

        var review = await db.QcReviews.SingleOrDefaultAsync(
            x => x.SurveySessionId == inspectionId, cancellationToken);
        if (review?.Status == QcReviewStatus.Approved)
            return ReviewLocked();
        if (review is not null && review.Status != QcReviewStatus.PendingReview)
            return Conflict(new { error = $"Inspection review is already {review.Status}." });

        review ??= CreateReview(inspectionId);
        review.Status = QcReviewStatus.InReview;
        review.ReviewedByUserId = CurrentUserId;
        review.StartedAt = DateTimeOffset.UtcNow;
        review.ReviewedAt = null;
        QcWorkflowHistory.Record(db, inspectionId, CurrentUserId, "QC Started Review", review);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToReviewResponse(review));
    }

    [HttpPost("inspections/{inspectionId:guid}/approve")]
    public async Task<ActionResult<QcReviewResponse>> Approve(
        Guid inspectionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApproveQcInspectionRequest? request,
        CancellationToken cancellationToken)
    {
        var context = await GetInspection(inspectionId, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Inspection was not found." });
        if (!await CanAccessProject(context.Project.Id, cancellationToken))
            return ProjectForbidden();

        var review = await db.QcReviews.SingleOrDefaultAsync(
            x => x.SurveySessionId == inspectionId, cancellationToken);
        if (review?.Status == QcReviewStatus.Approved)
            return ReviewLocked();
        if (review?.Status != QcReviewStatus.InReview)
            return Conflict(new { error = "Start the QC review before approving this inspection." });

        review.Status = QcReviewStatus.Approved;
        review.ReviewedByUserId = CurrentUserId;
        review.ReviewedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request?.Comments))
            review.Comments = request.Comments.Trim();
        context.Inspection.Status = SurveyStatus.Approved;
        QcWorkflowHistory.Record(db, inspectionId, CurrentUserId, "QC Approved", review);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToReviewResponse(review));
    }

    [HttpPost("inspections/{inspectionId:guid}/request-changes")]
    public async Task<ActionResult<QcReviewResponse>> RequestChanges(
        Guid inspectionId, RequestQcChangesRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Comments))
            return BadRequest(new { error = "Comments are required when requesting changes." });
        if (!request.Severity.HasValue || !Enum.IsDefined(request.Severity.Value))
            return BadRequest(new { error = "Severity must be Low, Medium, High, or Critical." });

        var context = await GetInspection(inspectionId, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Inspection was not found." });
        if (!await CanAccessProject(context.Project.Id, cancellationToken))
            return ProjectForbidden();

        var review = await db.QcReviews.SingleOrDefaultAsync(
            x => x.SurveySessionId == inspectionId, cancellationToken);
        if (review?.Status == QcReviewStatus.Approved)
            return ReviewLocked();
        if (review?.Status != QcReviewStatus.InReview)
            return Conflict(new { error = "Start the QC review before requesting changes." });

        review.Status = QcReviewStatus.ChangesRequested;
        review.Comments = request.Comments.Trim();
        review.Severity = request.Severity;
        review.ReviewedByUserId = CurrentUserId;
        review.ReviewedAt = DateTimeOffset.UtcNow;
        context.Inspection.Status = SurveyStatus.Returned;
        QcWorkflowHistory.Record(db, inspectionId, CurrentUserId, "QC Requested Changes", review);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToReviewResponse(review));
    }

    [HttpPut("inspections/{inspectionId:guid}/comments")]
    public async Task<ActionResult<QcReviewResponse>> UpdateComments(
        Guid inspectionId, UpdateQcCommentsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Comments))
            return BadRequest(new { error = "Comments are required." });

        var context = await GetInspection(inspectionId, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Inspection was not found." });
        if (!await CanAccessProject(context.Project.Id, cancellationToken))
            return ProjectForbidden();

        var review = await db.QcReviews.SingleOrDefaultAsync(
            x => x.SurveySessionId == inspectionId, cancellationToken);
        if (review?.Status == QcReviewStatus.Approved)
            return ReviewLocked();

        review ??= CreateReview(inspectionId);
        review.Comments = request.Comments.Trim();
        review.ReviewedByUserId = CurrentUserId;
        QcWorkflowHistory.Record(db, inspectionId, CurrentUserId, "QC Comments Updated", review);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToReviewResponse(review));
    }

    private IQueryable<DashboardInspectionRow> SubmittedInspectionsForCurrentQc() =>
        from inspection in db.SurveySessions.AsNoTracking()
        join equipment in db.AreaEquipment.AsNoTracking() on inspection.AreaEquipmentId equals equipment.Id
        join project in AccessibleProjects() on equipment.ProjectId equals project.Id
        join review in db.QcReviews.AsNoTracking() on inspection.Id equals review.SurveySessionId into reviewGroup
        from review in reviewGroup.DefaultIfEmpty()
        where inspection.SubmittedAt != null
        select new DashboardInspectionRow(inspection.DueDate, review == null ? null : review.Status);

    private async Task<List<ReviewSnapshot>> LoadReviewSnapshots(
        Guid[] projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0)
            return [];

        return await (
            from inspection in db.SurveySessions.AsNoTracking()
            join equipment in db.AreaEquipment.AsNoTracking() on inspection.AreaEquipmentId equals equipment.Id
            join review in db.QcReviews.AsNoTracking() on inspection.Id equals review.SurveySessionId into reviewGroup
            from review in reviewGroup.DefaultIfEmpty()
            where projectIds.Contains(equipment.ProjectId) && inspection.SubmittedAt != null
            select new ReviewSnapshot(
                equipment.ProjectId,
                equipment.AreaId,
                equipment.Id,
                inspection.Id,
                inspection.SubmittedAt,
                inspection.DueDate,
                review == null ? QcReviewStatus.PendingReview : review.Status))
            .ToListAsync(cancellationToken);
    }

    private async Task<InspectionContext?> GetInspectionByEquipment(
        Guid equipmentId, CancellationToken cancellationToken) =>
        await (
            from inspection in db.SurveySessions.AsNoTracking()
            join equipment in db.AreaEquipment.AsNoTracking() on inspection.AreaEquipmentId equals equipment.Id
            join project in db.Projects.AsNoTracking() on equipment.ProjectId equals project.Id
            where equipment.Id == equipmentId && inspection.SubmittedAt != null
            select new InspectionContext(inspection, equipment, project))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<InspectionContext?> GetInspection(
        Guid inspectionId, CancellationToken cancellationToken) =>
        await (
            from inspection in db.SurveySessions
            join equipment in db.AreaEquipment on inspection.AreaEquipmentId equals equipment.Id
            join project in db.Projects on equipment.ProjectId equals project.Id
            where inspection.Id == inspectionId
            select new InspectionContext(inspection, equipment, project))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<QcEquipmentInspectionResponse> BuildInspectionResponse(InspectionContext context, CancellationToken cancellationToken) =>
        new QcInspectionReader(db).BuildInspectionResponse(context.Inspection, context.Equipment, context.Project, cancellationToken);

    private QcReview CreateReview(Guid inspectionId)
    {
        var review = new QcReview
        {
            SurveySessionId = inspectionId,
            ReviewedByUserId = CurrentUserId
        };
        db.QcReviews.Add(review);
        return review;
    }

    private async Task<ProjectAccess> CheckProjectAccess(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AsNoTracking().AnyAsync(p => p.Id == projectId, cancellationToken))
            return ProjectAccess.NotFound;
        return await CanAccessProject(projectId, cancellationToken) ? ProjectAccess.Allowed : ProjectAccess.Forbidden;
    }

    private ObjectResult ProjectAccessError(ProjectAccess access) => access == ProjectAccess.NotFound
        ? NotFound(new { error = "Project was not found." })
        : ProjectForbidden();

    private ObjectResult ProjectForbidden() => StatusCode(
        StatusCodes.Status403Forbidden,
        new { error = "This project is outside your QC assignment scope." });

    private ConflictObjectResult ReviewLocked() =>
        Conflict(new { error = "This QC review is approved and locked." });

    private static QcReviewResponse ToReviewResponse(QcReview review) => new(
        review.SurveySessionId,
        review.Status,
        review.Comments,
        review.Severity,
        review.ReviewedByUserId,
        review.StartedAt,
        review.ReviewedAt);

    private static int CountStatus(IEnumerable<ReviewSnapshot> rows, QcReviewStatus status) =>
        rows.Count(x => x.Status == status);

    private static QcReviewStatus AggregateStatus(IReadOnlyCollection<ReviewSnapshot> reviews) =>
        QcWorkflowStatus.Aggregate(reviews.Select(r => r.Status));

    private static DateTimeOffset? EarliestOpenDueDate(IEnumerable<ReviewSnapshot> reviews) =>
        reviews.Where(x => x.Status != QcReviewStatus.Approved && x.DueDate.HasValue)
            .Select(x => x.DueDate)
            .Min();

    private static string? GetLayoutImagePath(Guid projectId, ProjectArea area)
    {
        var layoutPhoto = area.Photos.FirstOrDefault(x => x.FilePath == area.LayoutImagePath)
                          ?? area.Photos.OrderBy(x => x.UploadedAt).ThenBy(x => x.Id).FirstOrDefault();
        return layoutPhoto is null
            ? null
            : $"/api/projects/{projectId}/areas/{area.Id}/photos/{layoutPhoto.Id}";
    }

    // One scope for every QC read/action, including authenticated inspection photos.
    private IQueryable<Project> AccessibleProjects()
    {
        var projects = db.Projects.AsNoTracking();
        if (User.IsInRole(nameof(UserRole.Admin))) return projects;
        if (User.IsInRole(nameof(UserRole.Manager)))
            return projects.Where(p => p.AssignedQc.ManagerId == CurrentUserId);
        if (User.IsInRole(nameof(UserRole.Qc)))
            return projects.Where(p => p.AssignedQcId == CurrentUserId);
        return projects.Where(p => false);
    }

    private IQueryable<Guid> OpenInspectionProjectIds() =>
        from inspection in db.SurveySessions.AsNoTracking()
        join equipment in db.AreaEquipment.AsNoTracking() on inspection.AreaEquipmentId equals equipment.Id
        join review in db.QcReviews.AsNoTracking() on inspection.Id equals review.SurveySessionId into reviews
        from review in reviews.DefaultIfEmpty()
        where inspection.SubmittedAt != null &&
            (inspection.Status == SurveyStatus.Submitted || inspection.Status == SurveyStatus.Returned) &&
            (review == null || review.Status == QcReviewStatus.PendingReview ||
             review.Status == QcReviewStatus.InReview || review.Status == QcReviewStatus.ChangesRequested)
        select equipment.ProjectId;

    private Task<bool> CanAccessProject(Guid projectId, CancellationToken ct) =>
        AccessibleProjects().AnyAsync(p => p.Id == projectId, ct);

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private sealed record DashboardInspectionRow(DateTimeOffset? DueDate, QcReviewStatus? ReviewStatus);
    private sealed record ReviewSnapshot(
        Guid ProjectId,
        Guid AreaId,
        Guid EquipmentId,
        Guid InspectionId,
        DateTimeOffset? SubmittedAt,
        DateTimeOffset? DueDate,
        QcReviewStatus Status);
    private sealed record InspectionContext(SurveySession Inspection, AreaEquipment Equipment, Project Project);
    private enum ProjectAccess { Allowed, NotFound, Forbidden }
}
