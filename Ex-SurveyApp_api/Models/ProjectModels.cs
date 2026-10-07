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
    public required string TagNumber { get; set; }
    public string? EquipmentCode { get; set; }
    public required string Type { get; set; }
    public string? CircuitId { get; set; }
    public string? Location { get; set; }
    public string? Manufacturer { get; set; }
    public string? ProtectionDetails { get; set; }
    public string? CertificateNumber { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
