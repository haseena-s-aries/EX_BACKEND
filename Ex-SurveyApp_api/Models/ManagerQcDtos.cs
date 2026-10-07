namespace Ex_SurveyApp_api.Models;

public record ManagerQcCounts(int AwaitingQcReview, int InReview, int ChangesRequested, int Approved, int Overdue);
public record ManagerQcProject(Guid ProjectId, string ProjectName, string JobNo, string Location,
    string SurveyorName, string AssignedQc, Guid? ManagerId, string? ManagerName,
    ProjectStatus ProjectStatus, QcReviewStatus QcReviewStatus, int TotalEquipment,
    ManagerQcCounts Counts, DateTimeOffset? LastQcActivity);
public record ManagerQcEquipment(Guid EquipmentId, Guid? InspectionId, int SequenceNo, string TagNumber,
    string Type, EquipmentInspectionStatus InspectionStatus, QcReviewStatus? QcReviewStatus,
    string? QcComments, QcSeverity? Severity, string? ReviewedBy,
    DateTimeOffset? ReviewStartedAt, DateTimeOffset? ReviewedAt);
public record ManagerQcArea(Guid AreaId, string AreaName, int TotalEquipment, ManagerQcCounts Counts);
public record ManagerQcProjectDetails(ManagerQcProject Project, IReadOnlyCollection<ManagerQcArea> Areas);
public record ManagerQcAreaDetails(Guid AreaId, string AreaName, ManagerQcCounts Counts,
    IReadOnlyCollection<ManagerQcEquipment> Equipment);
public record ManagerQcHistory(Guid Id, string Action, DateTimeOffset OccurredAt, string? ActorName,
    string? Comments, QcSeverity? Severity, QcReviewStatus? QcStatus,
    DateTimeOffset? ReviewStartedAt, DateTimeOffset? ReviewedAt);
public record ManagerQcInspection(ManagerQcEquipment Equipment, QcEquipmentInspectionResponse? Inspection,
    IReadOnlyCollection<ManagerQcHistory> History);
public record SetReportingManagerRequest(Guid? ManagerId);
