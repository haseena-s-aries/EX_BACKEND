using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public class QcInspectionReader(SurveyDbContext db)
{
    public async Task<QcEquipmentInspectionResponse> BuildInspectionResponse(
        SurveySession inspection, AreaEquipment equipment, Project project, CancellationToken cancellationToken)
    {
        var review = await db.QcReviews.AsNoTracking().SingleOrDefaultAsync(
            x => x.SurveySessionId == inspection.Id, cancellationToken);
        var answers = await (
            from answer in db.SurveyAnswers.AsNoTracking()
            join question in db.ChecklistQuestions.AsNoTracking() on answer.ChecklistQuestionId equals question.Id
            where answer.SurveySessionId == inspection.Id
            orderby question.SortOrder
            select new QcChecklistAnswerResponse(
                answer.Id, question.Id, question.Text, question.SortOrder, answer.Value, answer.Comment))
            .ToListAsync(cancellationToken);
        var photos = await db.Files.AsNoTracking()
            .Where(x => x.SurveySessionId == inspection.Id &&
                        (x.Category == FileCategory.EquipmentPhoto || x.Category == FileCategory.FindingPhoto))
            .OrderBy(x => x.UploadedAt)
            .Select(x => new QcInspectionPhotoResponse(
                x.Id, x.FileName, x.StorageKey, x.ContentType, x.UploadedAt))
            .ToListAsync(cancellationToken);
        var findings = await db.Findings.AsNoTracking()
            .Where(x => x.SurveySessionId == inspection.Id)
            .Select(x => new QcFindingResponse(
                x.Id, x.Observation, x.Recommendation, x.Severity, x.Status))
            .ToListAsync(cancellationToken);

        return new QcEquipmentInspectionResponse(
            inspection.Id,
            project.Id,
            equipment.AreaId,
            new QcEquipmentDetailsResponse(
                equipment.Id,
                equipment.SequenceNo,
                equipment.XPercent,
                equipment.YPercent,
                equipment.TagNumber,
                equipment.EquipmentCode,
                equipment.Type,
                equipment.CircuitId,
                equipment.Location,
                equipment.Manufacturer,
                equipment.ProtectionDetails,
                equipment.CertificateNumber,
                equipment.RfidTagUid),
            answers,
            photos,
            findings,
            inspection.SurveyorRemarks,
            equipment.CertificateNumber,
            equipment.ProtectionDetails,
            inspection.InspectionTimestamp ?? inspection.SubmittedAt,
            inspection.SubmittedAt,
            inspection.DueDate,
            review?.Comments,
            review?.Status ?? QcReviewStatus.PendingReview,
            review?.Severity,
            review?.ReviewedByUserId,
            review?.ReviewedAt,
            inspection.InspectorName);
    }

}
