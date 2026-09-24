namespace Ex_SurveyApp_api.Models;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ProjectName { get; set; }
    public required string Sector { get; set; }
    public required string JobNo { get; set; }
    public required string Location { get; set; }
    public required string VesselOrSiteName { get; set; }
    public Guid AssignedSurveyorId { get; set; }
    public Guid AssignedQcId { get; set; }
    public Guid AssignedClientId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ProjectArea> Areas { get; set; } = new List<ProjectArea>();
}

public class ProjectArea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public required string AreaName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ProjectAreaPhoto> Photos { get; set; } = new List<ProjectAreaPhoto>();
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
