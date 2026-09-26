using System.ComponentModel.DataAnnotations;

namespace Ex_SurveyApp_api.Models;

public record SurveyorProjectSummaryResponse(
    Guid ProjectId,
    string ProjectName,
    string Sector,
    string JobNo,
    string Location,
    string VesselOrSiteName,
    DateTimeOffset CreatedDate,
    string Status,
    int AreaCount);

public record SurveyorProjectDetailsResponse(
    Guid ProjectId,
    string ProjectName,
    string Sector,
    string JobNo,
    string Location,
    string VesselOrSiteName,
    DateTimeOffset CreatedDate,
    string Status,
    string AssignedSurveyor,
    string AssignedQc,
    string AssignedClient,
    IReadOnlyCollection<SurveyorAreaSummaryResponse> Areas);

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
    [StringLength(100)] string? EquipmentCode,
    [Required, StringLength(100, MinimumLength = 1)] string Type,
    [StringLength(100)] string? CircuitId,
    [StringLength(300)] string? Location,
    [StringLength(200)] string? Manufacturer,
    [StringLength(500)] string? ProtectionDetails,
    [StringLength(150)] string? CertificateNumber);

public record AreaEquipmentResponse(
    Guid Id,
    Guid ProjectId,
    Guid AreaId,
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
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
