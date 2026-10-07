using System.ComponentModel.DataAnnotations;

namespace Ex_SurveyApp_api.Models;

public record UpsertProjectRequest(

    [Required, StringLength(200, MinimumLength = 1)] string ProjectName,

    [Required, StringLength(150, MinimumLength = 1)] string Sector,

    [Required, StringLength(100, MinimumLength = 1)] string JobNo,

    [Required, StringLength(300, MinimumLength = 1)] string Location,

    [Required, StringLength(200, MinimumLength = 1)] string VesselOrSiteName,

    [Required] Guid AssignedSurveyorId,

    [Required] Guid AssignedQcId,

    Guid? AssignedClientId = null,

    ProjectStatus Status = ProjectStatus.Pending);

public record UpsertProjectAreaRequest(

    [Required, StringLength(200, MinimumLength = 1)] string AreaName);

public class UploadProjectAreaPhotosRequest
{

    [Required]

    public List<IFormFile> Files { get; set; } = [];

}

public record ProjectSummaryResponse(

    Guid Id,

    string ProjectName,

    string Sector,

    string JobNo,

    string Location,

    string VesselOrSiteName,

    ProjectStatus Status,

    Guid AssignedSurveyorId,

    Guid AssignedQcId,

    Guid? AssignedClientId,

    Guid CreatedByUserId,

    DateTimeOffset CreatedAt,

    DateTimeOffset UpdatedAt,

    int AreaCount);

public record ProjectResponse(

    Guid Id,

    string ProjectName,

    string Sector,

    string JobNo,

    string Location,

    string VesselOrSiteName,

    ProjectStatus Status,

    Guid AssignedSurveyorId,

    Guid AssignedQcId,

    Guid? AssignedClientId,

    Guid CreatedByUserId,

    DateTimeOffset CreatedAt,

    DateTimeOffset UpdatedAt,

    IReadOnlyCollection<ProjectAreaResponse> Areas,
    string? HazardousAreaPlanPath = null,
    string? HazardousAreaPlanFileName = null,
    string? HazardousAreaPlanContentType = null);

public record ProjectAreaResponse(

    Guid Id,

    Guid ProjectId,

    string AreaName,

    string? LayoutImagePath,

    DateTimeOffset CreatedAt,

    int EquipmentCount,

    IReadOnlyCollection<ProjectAreaPhotoResponse> Photos,

    IReadOnlyCollection<ProjectAreaEquipmentResponse> Equipment);

public record ProjectAreaEquipmentResponse(
    Guid Id,
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
    string? CertificateNumber,
    DateTimeOffset CreatedAt,
    string InspectionStatus); public record ProjectAreaPhotoResponse(

    Guid Id,

    Guid ProjectAreaId,

    string FileName,

    string FilePath,

    string DownloadUrl,

    Guid UploadedByUserId,

    DateTimeOffset UploadedAt);

public record ProjectAssignableUserResponse(

    Guid Id,

    string DisplayName,

    string Email,

    UserRole Role,
    Guid? ManagerId = null,
    string? ManagerName = null);
