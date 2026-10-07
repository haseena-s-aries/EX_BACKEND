using Ex_SurveyApp_api.Models;

namespace Ex_SurveyApp_api.Services;

public static class QcWorkflowStatus
{
    public static EquipmentInspectionStatus InspectionStatus(SurveyStatus? surveyStatus, QcReviewStatus? qcStatus)
    {
        if (qcStatus == QcReviewStatus.Approved || surveyStatus == SurveyStatus.Approved)
            return EquipmentInspectionStatus.QcApproved;
        if (qcStatus == QcReviewStatus.ChangesRequested || surveyStatus == SurveyStatus.Returned)
            return EquipmentInspectionStatus.ChangesRequested;
        if (surveyStatus is null) return EquipmentInspectionStatus.NotStarted;
        return surveyStatus == SurveyStatus.Submitted ? EquipmentInspectionStatus.Completed : EquipmentInspectionStatus.InProgress;
    }
    public static QcReviewStatus Aggregate(IEnumerable<QcReviewStatus> values)
    {
        var statuses = values.ToArray();
        if (statuses.Length == 0) return QcReviewStatus.PendingReview;
        if (statuses.Contains(QcReviewStatus.ChangesRequested)) return QcReviewStatus.ChangesRequested;
        if (statuses.Contains(QcReviewStatus.InReview)) return QcReviewStatus.InReview;
        return statuses.All(s => s == QcReviewStatus.Approved) ? QcReviewStatus.Approved : QcReviewStatus.PendingReview;
    }
}
