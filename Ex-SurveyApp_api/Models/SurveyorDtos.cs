using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Ex_SurveyApp_api.Models;

public record SurveyorDashboardResponse(
    int MyProjectCount,
    int InProgress,
    int OnHold,
    int NotStarted,
    int AwaitingQcReview,
    int ChangesRequested,
    int QcApproved,
    IReadOnlyCollection<SurveyorQcFeedbackResponse> QcFeedback);

public record SurveyorQcFeedbackResponse(
    Guid ProjectId,
    Guid AreaId,
    Guid EquipmentId,
    string ProjectName,
    string AreaName,
    int SequenceNo,
    string TagNumber,
    string Type,
    string QcComments,
    QcSeverity? Severity,
    DateTimeOffset? ReviewedDate);

public record SurveyorProjectSummaryResponse(
    Guid ProjectId,
    string ProjectName,
    string Sector,
    string JobNo,
    string Location,
    string VesselOrSiteName,
    DateTimeOffset CreatedDate,
    ProjectStatus Status,
    int AreaCount, bool IsTransferred = false, string ReportStatus = "Ongoing", ProjectScheduleSummary? Schedule = null);

public record SurveyorProjectDetailsResponse(
    Guid ProjectId,
    string ProjectName,
    string Sector,
    string JobNo,
    string Location,
    string VesselOrSiteName,
    DateTimeOffset CreatedDate,
    ProjectStatus Status,
    string AssignedSurveyor,
    string AssignedQc,
    string AssignedClient,
    IReadOnlyCollection<SurveyorAreaSummaryResponse> Areas, DateOnly? TargetDate = null, string ReportStatus = "Ongoing");

public record SurveyorAreaSummaryResponse(
    Guid AreaId,
    string AreaName,
    string? LayoutImagePath,
    int PhotoCount,
    int EquipmentCount);

public record SurveyorAreaDetailsResponse(
    Guid AreaId,
    string AreaName,
    string? LayoutImagePath,
    IReadOnlyCollection<AreaEquipmentResponse> Equipment);

public record UpsertAreaEquipmentRequest(
    [Range(typeof(decimal), "0", "100")] decimal XPercent,
    [Range(typeof(decimal), "0", "100")] decimal YPercent,
    [Required, StringLength(100, MinimumLength = 1)] string TagNumber,
    [StringLength(100)] string? EquipmentId,
    [Required, StringLength(100, MinimumLength = 1)] string Type,
    [StringLength(100)] string? CircuitId,
    [StringLength(300)] string? Location,
    [StringLength(200)] string? Manufacturer,
    [StringLength(500)] string? ProtectionDetails,
    [StringLength(150)] string? CertificateNumber,
    [StringLength(100)] string? RfidTagUid = null,
    bool IsPlacedOnLayout = true,
    [StringLength(1000)] string? Description = null,
    [StringLength(150)] string? Model = null,
    [StringLength(150)] string? SerialNumber = null,
    [StringLength(150)] string? ExProtectionType = null,
    [StringLength(100)] string? GasGroup = null,
    [StringLength(100)] string? TemperatureClass = null,
    [StringLength(100)] string? HazardousAreaZone = null,
    [StringLength(100)] string? AmbientTemperatureRange = null,
    [StringLength(100)] string? IpRating = null,
    [StringLength(250)] string? NotifiedBody = null,
    [StringLength(1000)] string? SpecialConditionsOfUse = null,
    [StringLength(50)] string? RfidIdentificationMethod = null,
    Guid? ClientEquipmentId = null,
    [StringLength(80)] string? LayoutImageKey = null);

public record RecordEquipmentScanEventRequest(
    Guid ClientEventId,
    [Required, StringLength(200, MinimumLength = 1)] string Identifier,
    [Required, StringLength(20)] string Technology,
    [Required, StringLength(20)] string MatchResult,
    Guid? EquipmentId = null,
    DateTimeOffset? ScannedAt = null,
    [StringLength(1000)] string? Discrepancy = null);

public record AreaEquipmentResponse(
    Guid Id,
    Guid ProjectId,
    Guid AreaId,
    int SequenceNo,
    decimal XPercent,
    decimal YPercent,
    string TagNumber,
    string? EquipmentId,
    string Type,
    string? CircuitId,
    string? Location,
    string? Manufacturer,
    string? ProtectionDetails,
    string? CertificateNumber,
    string? RfidTagUid,
    string Status,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? InspectionId = null,
    QcReviewStatus? QcReviewStatus = null,
    string? QcComments = null,
    QcSeverity? QcSeverity = null,
    bool IsPlacedOnLayout = true,
    string? Description = null,
    string? Model = null,
    string? SerialNumber = null,
    string? ExProtectionType = null,
    string? GasGroup = null,
    string? TemperatureClass = null,
    string? HazardousAreaZone = null,
    string? AmbientTemperatureRange = null,
    string? IpRating = null,
    string? NotifiedBody = null,
    string? SpecialConditionsOfUse = null,
    string? RfidIdentificationMethod = null,
    string RegistrationStatus = "Verified",
    string? LayoutImageKey = null,
    int? MarkingNo = null);

public record ReviewEquipmentRegistrationRequest(
    [Required] string Decision,
    [StringLength(2000)] string? Comments = null);

public record SurveyorEquipmentDetailsResponse(
    Guid Id,
    Guid ProjectId,
    Guid AreaId,
    string AreaName,
    int SequenceNo,
    decimal XPercent,
    decimal YPercent,
    string TagNumber,
    string? EquipmentId,
    string Type,
    string? CircuitId,
    string? Location,
    string? Manufacturer,
    string? ProtectionDetails,
    string? CertificateNumber,
    string? RfidTagUid,
    DateTimeOffset CreatedAt,
    Guid? InspectionId,
    EquipmentInspectionStatus InspectionStatus,
    bool InspectionEditable,
    string? QcComments,
    QcSeverity? QcSeverity,
    int? MarkingNo = null);

public record SurveyorChecklistItemResponse(
    Guid ChecklistItemId,
    InspectionType InspectionType,
    string Text,
    int SortOrder,
    bool IsRequired,
    string? SectionCode = null, string? ApplicableProtectionTypes = null,
    int? SourceItemNumber = null, string? SourceItemReference = null,
    string? ApplicableGrades = null, string? SubChecksJson = null);

public record SaveInspectionAnswerRequest(
    [Required] Guid ChecklistItemId,
    bool IsChecked,
    [StringLength(1000)] string? Remarks);

public record SaveEquipmentInspectionRequest(
    [Required] DateTimeOffset? InspectionDate,
    [Required, StringLength(200, MinimumLength = 1)] string InspectorName,
    [StringLength(2000)] string? Comments,
    IReadOnlyCollection<SaveInspectionAnswerRequest> Answers,
    bool IsCompleted = true, DateTimeOffset? ExpectedUpdatedAt = null);

public record SurveyorInspectionAnswerResponse(
    Guid ChecklistItemId,
    InspectionType InspectionType,
    string Text,
    int SortOrder,
    bool IsRequired,
    bool IsChecked,
    string? Remarks,
    string? SectionCode = null, string? ApplicableProtectionTypes = null,
    int? SourceItemNumber = null, string? SourceItemReference = null,
    string? ApplicableGrades = null, string? SubChecksJson = null);

public record SurveyorInspectionPhotoResponse(
    Guid Id,
    string FileName,
    string PhotoPath,
    DateTimeOffset UploadedAt);

public record SurveyorEquipmentInspectionResponse(
    Guid InspectionId,
    Guid EquipmentId,
    DateTimeOffset InspectionDate,
    string InspectorName,
    string? Comments,
    EquipmentInspectionStatus Status,
    bool IsEditable,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<SurveyorInspectionAnswerResponse> Answers,
    IReadOnlyCollection<SurveyorInspectionPhotoResponse> Photos,
    string? QcComments,
    QcSeverity? QcSeverity);

public class UploadInspectionPhotosRequest
{
    [Required]
    public List<IFormFile> Files { get; set; } = [];
}

public class UploadEquipmentPhotosRequest
{
    [Required, StringLength(40)]
    public string Role { get; set; } = "Additional";

    [Required]
    public List<IFormFile> Files { get; set; } = [];

    public Guid? ClientPhotoId { get; set; }
}
