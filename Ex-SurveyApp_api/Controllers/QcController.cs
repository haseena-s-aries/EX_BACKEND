using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Qc))]
[Route("api/qc")]
public class QcController(SurveyDbContext db) : ControllerBase
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
        CancellationToken cancellationToken)
    {
        var projects = await (
            from project in db.Projects.AsNoTracking()
            join surveyor in db.Users.AsNoTracking() on project.AssignedSurveyorId equals surveyor.Id
            where project.AssignedQcId == CurrentUserId
            orderby project.UpdatedAt descending
            select new
            {
                project.Id,
                project.ProjectName,
                project.JobNo,
                project.Location,
                SurveyorName = surveyor.DisplayName,
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
                EarliestOpenDueDate(projectReviews));
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
        if (project.AssignedQcId != CurrentUserId)
            return ProjectForbidden();

        var userIds = new[] { project.AssignedSurveyorId, project.AssignedQcId, project.AssignedClientId };
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
            names.GetValueOrDefault(project.AssignedClientId, string.Empty),
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
        if (context.Project.AssignedQcId != CurrentUserId)
            return ProjectForbidden();

        return Ok(await BuildInspectionResponse(context, cancellationToken));
    }

    [HttpPost("inspections/{inspectionId:guid}/start-review")]
    public async Task<ActionResult<QcReviewResponse>> StartReview(
        Guid inspectionId, CancellationToken cancellationToken)
    {
        var context = await GetInspection(inspectionId, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Inspection was not found." });
        if (context.Project.AssignedQcId != CurrentUserId)
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
        if (context.Project.AssignedQcId != CurrentUserId)
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
        if (context.Project.AssignedQcId != CurrentUserId)
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
        if (context.Project.AssignedQcId != CurrentUserId)
            return ProjectForbidden();

        var review = await db.QcReviews.SingleOrDefaultAsync(
            x => x.SurveySessionId == inspectionId, cancellationToken);
        if (review?.Status == QcReviewStatus.Approved)
            return ReviewLocked();

        review ??= CreateReview(inspectionId);
        review.Comments = request.Comments.Trim();
        review.ReviewedByUserId = CurrentUserId;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToReviewResponse(review));
    }

    private IQueryable<DashboardInspectionRow> SubmittedInspectionsForCurrentQc() =>
        from inspection in db.SurveySessions.AsNoTracking()
        join equipment in db.AreaEquipment.AsNoTracking() on inspection.AreaEquipmentId equals equipment.Id
        join project in db.Projects.AsNoTracking() on equipment.ProjectId equals project.Id
        join review in db.QcReviews.AsNoTracking() on inspection.Id equals review.SurveySessionId into reviewGroup
        from review in reviewGroup.DefaultIfEmpty()
        where project.AssignedQcId == CurrentUserId && inspection.SubmittedAt != null
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

    private async Task<QcEquipmentInspectionResponse> BuildInspectionResponse(
        InspectionContext context, CancellationToken cancellationToken)
    {
        var review = await db.QcReviews.AsNoTracking().SingleOrDefaultAsync(
            x => x.SurveySessionId == context.Inspection.Id, cancellationToken);
        var answers = await (
            from answer in db.SurveyAnswers.AsNoTracking()
            join question in db.ChecklistQuestions.AsNoTracking() on answer.ChecklistQuestionId equals question.Id
            where answer.SurveySessionId == context.Inspection.Id
            orderby question.SortOrder
            select new QcChecklistAnswerResponse(
                answer.Id, question.Id, question.Text, question.SortOrder, answer.Value, answer.Comment))
            .ToListAsync(cancellationToken);
        var photos = await db.Files.AsNoTracking()
            .Where(x => x.SurveySessionId == context.Inspection.Id &&
                        (x.Category == FileCategory.EquipmentPhoto || x.Category == FileCategory.FindingPhoto))
            .OrderBy(x => x.UploadedAt)
            .Select(x => new QcInspectionPhotoResponse(
                x.Id, x.FileName, x.StorageKey, x.ContentType, x.UploadedAt))
            .ToListAsync(cancellationToken);
        var findings = await db.Findings.AsNoTracking()
            .Where(x => x.SurveySessionId == context.Inspection.Id)
            .Select(x => new QcFindingResponse(
                x.Id, x.Observation, x.Recommendation, x.Severity, x.Status))
            .ToListAsync(cancellationToken);

        var equipment = context.Equipment;
        return new QcEquipmentInspectionResponse(
            context.Inspection.Id,
            context.Project.Id,
            equipment.AreaId,
            new QcEquipmentDetailsResponse(
                equipment.Id,
                equipment.SequenceNo,
                equipment.XPercent,
                equipment.YPercent,
                equipment.TagNumber,
                equipment.EquipmentCode,
                equipment.Type,
                equipment.CircuitId,
                equipment.Location,
                equipment.Manufacturer,
                equipment.ProtectionDetails,
                equipment.CertificateNumber),
            answers,
            photos,
            findings,
            context.Inspection.SurveyorRemarks,
            equipment.CertificateNumber,
            equipment.ProtectionDetails,
            context.Inspection.InspectionTimestamp ?? context.Inspection.SubmittedAt,
            context.Inspection.SubmittedAt,
            context.Inspection.DueDate,
            review?.Comments,
            review?.Status ?? QcReviewStatus.PendingReview,
            review?.Severity,
            review?.ReviewedByUserId,
            review?.ReviewedAt);
    }

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
        var assignedQcId = await db.Projects.AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => (Guid?)x.AssignedQcId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!assignedQcId.HasValue)
            return ProjectAccess.NotFound;
        return assignedQcId == CurrentUserId ? ProjectAccess.Allowed : ProjectAccess.Forbidden;
    }

    private ObjectResult ProjectAccessError(ProjectAccess access) => access == ProjectAccess.NotFound
        ? NotFound(new { error = "Project was not found." })
        : ProjectForbidden();

    private ObjectResult ProjectForbidden() => StatusCode(
        StatusCodes.Status403Forbidden,
        new { error = "This project is assigned to another QC user." });

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

    private static QcReviewStatus AggregateStatus(IReadOnlyCollection<ReviewSnapshot> reviews)
    {
        if (reviews.Count == 0)
            return QcReviewStatus.PendingReview;
        if (reviews.Any(x => x.Status == QcReviewStatus.ChangesRequested))
            return QcReviewStatus.ChangesRequested;
        if (reviews.Any(x => x.Status == QcReviewStatus.InReview))
            return QcReviewStatus.InReview;
        return reviews.All(x => x.Status == QcReviewStatus.Approved)
            ? QcReviewStatus.Approved
            : QcReviewStatus.PendingReview;
    }

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
