using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class SampleChecklistChecks
{
    public static async Task Run()
    {
        var source = SampleChecklistImporter.ReadSource();
        foreach (var (section, count) in new[] { ("Apparatus", 23), ("Installation", 33), ("Environment", 3) })
            Check(source.Items.Where(x => x.Section == section).Select(x => x.ItemNumber).SequenceEqual(Enumerable.Range(1, count)), $"All {count} {section} points retain original numbering");
        var a9 = source.Items.Single(x => x.Section == "Apparatus" && x.ItemNumber == 9);
        var b7 = source.Items.Single(x => x.Section == "Installation" && x.ItemNumber == 7);
        Check(a9.SubChecks.Length == 2 && a9.SubChecks[0].ApplicableGrades.SequenceEqual(new[] { "Detailed", "Close" }) &&
              a9.SubChecks[1].ApplicableGrades.SequenceEqual(new[] { "Visual" }) && b7.SubChecks.Length == 2,
            "Physical/visual sub-checks retain their parent and separately mapped grade labels");
        Check(source.Items.Single(x => x.Section == "Apparatus" && x.ItemNumber == 14).ApplicableGrades.Length == 3,
            "Filled NA sample answers do not disable white grade cells");
        Check(source.Items.Single(x => x.Section == "Installation" && x.ItemNumber == 33).ApplicableGrades.SequenceEqual(new[] { "Detailed", "Close" }),
            "Grey Visual cell on Installation 33 is explicitly unavailable");

        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var manager = User("author", UserRole.Manager); var qc = User("reviewer", UserRole.Qc); var surveyor = User("inspector", UserRole.Surveyor);
        db.Users.AddRange(manager, qc, surveyor);
        var project = new Project {ProjectName="History protection", Sector="Marine", JobNo="SAMPLE-TEST", Location="Test", VesselOrSiteName="Test", CreatedByUserId=manager.Id, AssignedSurveyorId=surveyor.Id, AssignedQcId=qc.Id};
        var area = new ProjectArea {ProjectId=project.Id, AreaName="Test area"};
        var equipment = new AreaEquipment {ProjectId=project.Id, AreaId=area.Id, TagNumber="OLD", Type="Motor", CreatedByUserId=surveyor.Id};
        var nextEquipment = new AreaEquipment {ProjectId=project.Id, AreaId=area.Id, TagNumber="NEW", Type="Motor", CreatedByUserId=surveyor.Id};
        db.Projects.Add(project); db.ProjectAreas.Add(area); db.AreaEquipment.AddRange(equipment, nextEquipment);
        var approvedEquipment = new AreaEquipment {ProjectId=project.Id, AreaId=area.Id, TagNumber="APPROVED", Type="Motor", CreatedByUserId=surveyor.Id};
        db.AreaEquipment.Add(approvedEquipment);
        var oldQuestions = new List<ChecklistQuestion>();
        foreach (var grade in Enum.GetValues<InspectionType>())
        {
            var template = new ChecklistTemplate {Name=$"Default {grade} Inspection", InspectionType=grade};
            var question = new ChecklistQuestion {ChecklistTemplateId=template.Id, Text=$"Original {grade} wording", SortOrder=1};
            db.ChecklistTemplates.Add(template); db.ChecklistQuestions.Add(question); oldQuestions.Add(question);
        }
        var oldSession = new SurveySession {AreaEquipmentId=equipment.Id, SurveyorId=surveyor.Id, InspectorName="Original inspector", Status=SurveyStatus.Draft};
        db.SurveySessions.Add(oldSession);
        foreach (var q in oldQuestions) db.SurveyAnswers.Add(new SurveyAnswer {SurveySessionId=oldSession.Id, ChecklistQuestionId=q.Id, Value="True", Comment="Original remark"});
        var completed = new SurveySession {AreaEquipmentId=approvedEquipment.Id, SurveyorId=surveyor.Id, InspectorName="Approved inspector", Status=SurveyStatus.Approved, SubmittedAt=DateTimeOffset.UtcNow};
        db.SurveySessions.Add(completed);
        foreach (var q in oldQuestions) db.SurveyAnswers.Add(new SurveyAnswer {SurveySessionId=completed.Id, ChecklistQuestionId=q.Id, Value="False", Comment="Approved original remark"});
        db.QcReviews.Add(new QcReview {SurveySessionId=completed.Id, ReviewedByUserId=qc.Id, Status=QcReviewStatus.Approved, Comments="Original QC approval"});
        db.Files.Add(new SurveyFile {SurveySessionId=completed.Id, ProjectId=project.Id, AreaEquipmentId=approvedEquipment.Id, Category=FileCategory.EquipmentPhoto, FileName="original.jpg", StorageKey="private/original.jpg", ContentType="image/jpeg", UploadedById=surveyor.Id});
        await db.SaveChangesAsync();
        var answerIds = await db.SurveyAnswers.Where(x=>x.SurveySessionId==oldSession.Id).Select(x => x.Id).ToArrayAsync();

        var templates = await SampleChecklistImporter.ImportDraftsAsync(db, manager.Id);
        Check(templates.Count == 3 && templates.All(x => x.LifecycleStatus == ChecklistLifecycleStatus.Draft && !x.IsActive), "Import creates only drafts, never fabricated technical approval");
        var ids = templates.Select(x => x.Id).ToArray();
        var imported = await db.ChecklistQuestions.Where(x => ids.Contains(x.ChecklistTemplateId)).ToListAsync();
        Check(imported.Select(x => x.SourceItemReference).Distinct().Count() == 59 && imported.All(x => x.ResponseScaleCode == "BooleanChecked"), "59 source points are represented across grade templates with boolean answers");
        var stableIds = imported.Select(x => x.Id).Order().ToArray();
        await SampleChecklistImporter.ImportDraftsAsync(db, manager.Id);
        Check((await db.ChecklistQuestions.Where(x => ids.Contains(x.ChecklistTemplateId)).Select(x => x.Id).ToListAsync()).Order().SequenceEqual(stableIds), "Repeated import preserves IDs and does not duplicate questions");

        var previewController = As(new SurveyorController(db, null!, NullLogger<SurveyorController>.Instance), surveyor);
        var previewResult = (OkObjectResult)await previewController.GetSampleChecklistPreview(default);
        var previewJson = System.Text.Json.JsonSerializer.SerializeToElement(previewResult.Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Check(previewJson.GetProperty("pointCount").GetInt32()==59 && previewJson.GetProperty("items").GetArrayLength()==imported.Count &&
              !previewJson.GetProperty("isPublished").GetBoolean(), "Same-screen source preview exposes all sections before approval without turning them into saved answers");

        var author = As(new ChecklistAdministrationController(db), manager);
        var reviewer = As(new ChecklistAdministrationController(db), qc);
        var first = templates[0];
        var firstQuestions = imported.Where(x=>x.ChecklistTemplateId==first.Id).ToArray();
        var editInputs = firstQuestions.Select(q=>new ChecklistQuestionInput(q.Text,q.SortOrder,q.SectionCode!,q.IsRequired,q.ResponseScaleCode!,q.ApplicableProtectionTypes,false,false,null,null,null,false,q.SourceItemReference,q.SourceItemNumber,q.ApplicableGrades,q.SubChecksJson)).ToArray();
        Check(await author.Edit(first.Id,new(first.DraftRevision,first.Name,first.StandardReference!,null,first.ControlledSourceReference,first.ApplicableProtectionTypes!,editInputs),default) is OkObjectResult, "Source draft metadata can be reviewed and saved using BooleanChecked");
        Check((await db.ChecklistQuestions.Where(x=>x.ChecklistTemplateId==first.Id).Select(x=>x.Id).ToListAsync()).Order().SequenceEqual(firstQuestions.Select(x=>x.Id).Order()), "Saving a source draft preserves stable question IDs");
        foreach (var template in templates)
        {
            Check(await author.Submit(template.Id, default) is OkObjectResult, "Imported source draft can enter technical review");
            Check(await author.Approve(template.Id, new("Self-review"), default) is ForbidResult, "Author cannot approve own source import");
            Check(await reviewer.Approve(template.Id, new("Synthetic fixture review only"), default) is OkObjectResult, "Independent fixture reviewer can publish through existing workflow");
        }
        var survey = As(new SurveyorController(db, null!, NullLogger<SurveyorController>.Instance), surveyor);
        var approvedResponse = Value<SurveyorEquipmentInspectionResponse>(await survey.GetInspection(approvedEquipment.Id, default));
        Check(approvedResponse.Status == EquipmentInspectionStatus.QcApproved && !approvedResponse.IsEditable &&
              approvedResponse.Answers.All(x=>x.Text.StartsWith("Original") && !x.IsChecked && x.Remarks=="Approved original remark") &&
              approvedResponse.Photos.Single().FileName == "original.jpg" && approvedResponse.QcComments=="Original QC approval",
              "Publishing retains completed inspection text, unchecked answers, photos, QC approval and locking");
        var oldResponse = Value<SurveyorEquipmentInspectionResponse>(await survey.GetInspection(equipment.Id, default));
        Check(oldResponse.Answers.Count == 3 && oldResponse.Answers.All(x => x.Text.StartsWith("Original") && x.IsChecked && x.Remarks == "Original remark"), "Publishing new versions does not replace historical content or answers");
        var update = new SaveEquipmentInspectionRequest(DateTimeOffset.UtcNow, "Original inspector", "Kept", oldQuestions.Select(x => new SaveInspectionAnswerRequest(x.Id, true, "Original remark")).ToArray(), false);
        Check((await survey.UpdateInspection(oldSession.Id, update, default)).Result is OkObjectResult, "Historical draft remains editable using its original archived questions");
        Check((await db.SurveyAnswers.Where(x=>x.SurveySessionId==oldSession.Id).Select(x=>x.Id).ToListAsync()).Order().SequenceEqual(answerIds.Order()), "Updating historical draft preserves answer IDs");
        var current = Value<IEnumerable<SurveyorChecklistItemResponse>>(await survey.GetChecklist(default)).ToArray();
        Check(current.Length == imported.Count && current.All(x => x.SectionCode is not null && x.SourceItemNumber.HasValue && x.ApplicableGrades is not null), "Surveyor API exposes source metadata from published versions only");
        var created = Value<SurveyorEquipmentInspectionResponse>(await survey.CreateInspection(nextEquipment.Id, new(DateTimeOffset.UtcNow, "Inspector", null, [], false), default));
        Check(created.Answers.Count == imported.Count && created.Answers.All(x => !x.IsChecked && x.Remarks is null), "New inspections pin the approved checklist without copying sample answers");
        Check((await survey.UpdateInspection(oldSession.Id, update with {Answers=[new(current[0].ChecklistItemId,true,null)]}, default)).Result is BadRequestObjectResult, "New-version IDs cannot be injected into historical inspections");
        Console.WriteLine($"Sample checklist checks passed: 59 parent points; {imported.Count} grade-specific boolean questions.");
    }
    private static User User(string name, UserRole role) => new() {Email=name+"@sample.invalid",DisplayName=name,PasswordHash="test",Role=role,ApprovalStatus=ApprovalStatus.Approved};
    private static T As<T>(T controller, User user) where T : ControllerBase {controller.ControllerContext=new(){HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,user.Id.ToString()),new(ClaimTypes.Role,user.Role.ToString())],"Test"))}};return controller;}
    private static T Value<T>(ActionResult<T> result) => (T)((ObjectResult)result.Result!).Value!;
    private static void Check(bool condition,string message) {if(!condition)throw new Exception(message);Console.WriteLine("PASS: "+message);}
}
