using System.ComponentModel.DataAnnotations;

namespace Ex_SurveyApp_api.Models;

public record ProjectScheduleSummary(string Status, DateOnly? ExpectedCompletion, int DelayDays, string? LatestDelayReason);

public record UpsertProjectRequest(

    [Required, StringLength(200, MinimumLength = 1)] string ProjectName,

    [StringLength(150)] string? Sector,

    [Required, StringLength(100, MinimumLength = 1)] string JobNo,

    [Required, StringLength(300, MinimumLength = 1)] string Location,

    [Required, StringLength(200, MinimumLength = 1)] string VesselOrSiteName,

    [Required] Guid AssignedSurveyorId,

    [Required] Guid AssignedQcId,

    Guid? AssignedClientId = null,

    ProjectStatus Status = ProjectStatus.Pending,
    [StringLength(100)] string? UidNumber = null,
    [StringLength(200)] string? DocumentNumber = null,
    [StringLength(100)] string? Revision = null,
    [StringLength(4000)] string? Description = null,
    [Required(ErrorMessage = "Client Company Name is required."), StringLength(200)] string? ClientName = null,
    bool RemoveClientLogo = false, Guid? OurCompanyId = null, bool PreserveSurveyorAssignment = false, DateOnly? TargetDate = null, Guid? AdditionalQcId = null, bool UpdateAdditionalQcAssignment = false, [MaxLength(200)] Guid[]? AssignedClientIds = null, bool ClearClientAssignments = false);

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

    int AreaCount, DateOnly? TargetDate = null, ProjectScheduleSummary? Schedule = null, Guid? AdditionalSurveyorId = null, string ReportStatus = "Ongoing", IReadOnlyCollection<Guid>? AssignedClientIds = null);

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
    string? HazardousAreaPlanContentType = null,
    string? UidNumber = null, string? DocumentNumber = null, string? Revision = null,
    string? Description = null, string? ClientName = null, string? ClientLogoPath = null,
    Guid? OurCompanyId = null, CompanyResponse? OurCompany = null, DateOnly? TargetDate = null, Guid? AdditionalQcId = null, string ReportStatus = "Ongoing", IReadOnlyCollection<Guid>? AssignedClientIds = null, IReadOnlyCollection<ProjectAssignedClientResponse>? AssignedClients = null);

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
    string? RfidTagUid,
    DateTimeOffset CreatedAt,
    string InspectionStatus, string? LayoutImageKey = null, int? MarkingNo = null); public record ProjectAreaPhotoResponse(

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

public record ProjectAssignedClientResponse(Guid Id, string DisplayName, string Email);
