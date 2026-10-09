namespace Ex_SurveyApp_api.Models;
public record ClientProgress(int TotalAreas, int AreasCompleted, int TotalEquipment, int EquipmentInspected,
    int TotalSubmitted, int AwaitingQc, int InReview, int ChangesRequested, int QcApproved, int OpenDefects, int ClosedDefects, decimal SurveyProgress);
public record ClientProjectSummary(Guid ProjectId, string ProjectName, string Sector, string JobNumber, string Location,
    string VesselOrSiteName, ProjectStatus ProjectStatus, string SurveyorName, string AssignedQcName,
    string QcStatus, string? DocumentNumber, string? Revision, DateTimeOffset? GeneratedDate, ClientProgress Progress);
public record ClientDashboardCounts(int ActiveProjects, int SurveysInProgress, int AwaitingQc, int QcApproved,
    int CompletedProjects, int OpenDefects, int ClosedDefects, int ReportsAvailable);
public record ClientActivity(Guid Id, Guid ProjectId, string ProjectName, string Action, string? Detail, DateTimeOffset OccurredAt);
