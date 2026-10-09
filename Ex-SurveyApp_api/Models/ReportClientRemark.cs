using System.ComponentModel.DataAnnotations;
namespace Ex_SurveyApp_api.Models;

// Client discussion is separate from Surveyor answers, findings and QC comments.
public class ReportClientRemark
{
    public Guid Id {get; set;} = Guid.NewGuid();
    public Guid ClientUserId {get; set;}
    public Guid ProjectId {get; set;}
    public Guid? EquipmentId {get; set;}
    public Guid? FindingId {get; set;}
    public Guid? ReportId {get; set;}
    public bool AgainstReport {get; set;}
    public required string Remark {get; set;}
    public DateTimeOffset CreatedAt {get; set;} = DateTimeOffset.UtcNow;
    public bool IsRead {get; set;}
    public DateTimeOffset? ReadAt {get; set;}
}
public record AddClientRemarkRequest(Guid ProjectId, [Required, StringLength(4000, MinimumLength=1)] string Remark,
    Guid? EquipmentId = null, Guid? FindingId = null, Guid? ReportId = null, bool AgainstReport = false);
