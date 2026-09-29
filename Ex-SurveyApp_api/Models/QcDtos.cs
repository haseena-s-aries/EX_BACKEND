using System.ComponentModel.DataAnnotations;

namespace Ex_SurveyApp_api.Models;

public record QcDashboardResponse(
    int AwaitingQcReview,
    int ChangesRequested,
    int Approved,
    int Overdue);

public record QcProjectSummaryResponse(
    Guid ProjectId,
    string ProjectName,
    string JobNo,
    string Location,
    string SurveyorName,
    ProjectStatus ProjectStatus,
    QcReviewStatus QcReviewStatus,
    DateTimeOffset? SubmissionDate,
    DateTimeOffset? DueDate);

public record QcProjectDetailsResponse(
    Guid ProjectId,
    string ProjectName,
    string Sector,
    string JobNo,
    string Location,
    string VesselOrSiteName,
    ProjectStatus ProjectStatus,
    DateTimeOffset CreatedAt,
    string AssignedSurveyor,
    string AssignedQc,
    string AssignedClient,
    IReadOnlyCollection<QcProjectAreaSummaryResponse> Areas,
    int EquipmentCount,
    int PendingQcCount,
    int ApprovedCount,
    int ChangesRequestedCount);

public record QcProjectAreaSummaryResponse(
    Guid AreaId,
    string AreaName,
    string? LayoutImagePath,
    int EquipmentCount,
    int PendingQcCount,
    int ApprovedCount,
    int ChangesRequestedCount);

public record QcAreaDetailsResponse(
    Guid AreaId,
    string AreaName,
    string? LayoutImagePath,
    IReadOnlyCollection<QcAreaEquipmentResponse> Equipment);

public record QcAreaEquipmentResponse(
    Guid EquipmentId,
    Guid? InspectionId,
    int SequenceNo,
    decimal XPercent,
    decimal YPercent,
    string TagNumber,
    string Type,
    QcReviewStatus QcReviewStatus);

public record QcEquipmentDetailsResponse(
    Guid EquipmentId,
    int SequenceNo,
    decimal XPercent,
    decimal YPercent,
    string TagNumber,
    string? EquipmentCode,
    string Type,
    string? CircuitId,
    string? Location,
    string? Manufacturer,
    string? ProtectionDetails,
    string? CertificateNumber);

public record QcChecklistAnswerResponse(
    Guid AnswerId,
    Guid QuestionId,
    string Question,
    int SortOrder,
    string? Value,
    string? Comment);

public record QcInspectionPhotoResponse(
    Guid FileId,
    string FileName,
    string StorageKey,
    string ContentType,
    DateTimeOffset UploadedAt);

public record QcFindingResponse(
    Guid FindingId,
    string Observation,
    string? Recommendation,
    string? Severity,
    FindingStatus Status);

public record QcEquipmentInspectionResponse(
    Guid InspectionId,
    Guid ProjectId,
    Guid AreaId,
    QcEquipmentDetailsResponse Equipment,
    IReadOnlyCollection<QcChecklistAnswerResponse> ChecklistAnswers,
    IReadOnlyCollection<QcInspectionPhotoResponse> InspectionPhotos,
    IReadOnlyCollection<QcFindingResponse> Findings,
    string? SurveyorRemarks,
    string? CertificateNumber,
    string? CertificateDetails,
    DateTimeOffset? InspectionTimestamp,
    DateTimeOffset? SubmissionDate,
    DateTimeOffset? DueDate,
    string? QcComments,
    QcReviewStatus QcReviewStatus,
    QcSeverity? Severity,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt);

public record QcReviewResponse(
    Guid InspectionId,
    QcReviewStatus QcReviewStatus,
    string? QcComments,
    QcSeverity? Severity,
    Guid ReviewedByUserId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? ReviewedAt);

public record ApproveQcInspectionRequest(
    [StringLength(4000)] string? Comments);

public record RequestQcChangesRequest(
    [Required, StringLength(4000, MinimumLength = 1)] string Comments,
    [Required] QcSeverity? Severity);

public record UpdateQcCommentsRequest(
    [Required, StringLength(4000, MinimumLength = 1)] string Comments);
