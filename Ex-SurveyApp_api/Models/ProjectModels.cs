namespace Ex_SurveyApp_api.Models;

public enum ProjectStatus
{
    Pending = 0,
    Active = 1,
    InProgress = 2,
    Completed = 3,
    OnHold = 4
}

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ProjectName { get; set; }
    public required string Sector { get; set; }
    public required string JobNo { get; set; }
    public required string Location { get; set; }
    public required string VesselOrSiteName { get; set; }
    public string? HazardousAreaPlanPath { get; set; }
    public string? HazardousAreaPlanFileName { get; set; }
    public string? UidNumber { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Revision { get; set; }
    public string? Description { get; set; }
    public string? ClientName { get; set; }
    public string? ClientLogoPath { get; set; }
    public Guid? OurCompanyId {get; set;}
    public CompanyMaster? OurCompany {get; set;}
    public ProjectStatus Status { get; set; } = ProjectStatus.Pending;
    public Guid AssignedSurveyorId { get; set; }
    public Guid AssignedQcId { get; set; }
    public User AssignedQc { get; set; } = null!;
    public Guid? AssignedClientId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ProjectArea> Areas { get; set; } = new List<ProjectArea>();
    public ICollection<AreaEquipment> EquipmentPins { get; set; } = new List<AreaEquipment>();
}

public class ProjectArea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public required string AreaName { get; set; }
    public string? LayoutImagePath { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ProjectAreaPhoto> Photos { get; set; } = new List<ProjectAreaPhoto>();
    public ICollection<AreaEquipment> EquipmentPins { get; set; } = new List<AreaEquipment>();
}

public class ProjectAreaPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectAreaId { get; set; }
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AreaEquipment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid AreaId { get; set; }
    public int SequenceNo { get; set; }
    public decimal XPercent { get; set; }
    public decimal YPercent { get; set; }
    /// <summary>False when field-registered before a layout position is known.</summary>
    public bool IsPlacedOnLayout { get; set; } = true;
    public required string TagNumber { get; set; }
    public string? EquipmentName { get; set; }
    public string? EquipmentCode { get; set; }
    public string? RfidTagUid { get; set; }
    public required string Type { get; set; }
    public string? CircuitId { get; set; }
    public string? Location { get; set; }
    public string? Manufacturer { get; set; }
    public string? ProtectionDetails { get; set; }
    public string? CertificateNumber { get; set; }
    public string? Description { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? ExProtectionType { get; set; }
    public string? GasGroup { get; set; }
    public string? TemperatureClass { get; set; }
    public string? HazardousAreaZone { get; set; }
    public string? AmbientTemperatureRange { get; set; }
    public string? IpRating { get; set; }
    public string? NotifiedBody { get; set; }
    public string? SpecialConditionsOfUse { get; set; }
    public string? RfidIdentificationMethod { get; set; }
    /// <summary>JSON array of metadata keys explicitly confirmed as not applicable. Null means unknown, not N/A.</summary>
    public string? NotApplicableFieldsJson { get; set; }
    public string ReconciliationStatus { get; set; } = "Unreviewed";
    /// <summary>Registration review is independent from inspection QC and defect closure.</summary>
    public string RegistrationStatus { get; set; } = "Verified";
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EquipmentTagAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EquipmentId { get; set; }
    public Guid ProjectId { get; set; }
    public required string Technology { get; set; }
    public required string NormalizedIdentifier { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid AssignedByUserId { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public required string AssignmentReason { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
    public Guid? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
}

public sealed class EquipmentTagHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EquipmentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? AssignmentId { get; set; }
    public required string Technology { get; set; }
    public required string NormalizedIdentifier { get; set; }
    public required string ActionCode { get; set; }
    public required string Reason { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Append-only record of an equipment identifier lookup inside a field job.</summary>
public sealed class EquipmentScanEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientEventId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid AreaId { get; set; }
    public Guid? EquipmentId { get; set; }
    public Guid ActorUserId { get; set; }
    public required string Identifier { get; set; }
    public required string Technology { get; set; }
    public required string MatchResult { get; set; }
    public string? Discrepancy { get; set; }
    public DateTimeOffset ScannedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}
