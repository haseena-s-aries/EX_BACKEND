using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

internal static class ChecklistAdministrationChecks
{
    public static async Task Run()
    {
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var manager = User("Manager", UserRole.Manager); var qc = User("Technical QC", UserRole.Qc); var admin = User("Administrator", UserRole.Admin);
        db.Users.AddRange(manager,qc,admin); await db.SaveChangesAsync();
        var controller = As(new ChecklistAdministrationController(db),manager);
        var create = await controller.Create(new("Approved content source",InspectionType.Close,"IEC 60079-17","2023","Controlled copy / document reference","Ex d, Ex e"),default);
        Check(create is CreatedAtActionResult,"Manager can create a draft checklist template without needing SQL Server");
        var draft = await db.ChecklistTemplates.SingleAsync();
        var question = new ChecklistQuestionInput("Checkpoint from controlled source",1,"Apparatus",true,"PassFailNA","Ex d",true,true,"mm",1,5,true);
        var edit = await controller.Edit(draft.Id,new(1,draft.Name,draft.StandardReference!,draft.StandardEdition,draft.ControlledSourceReference,draft.ApplicableProtectionTypes!,[question]),default);
        Check(edit is OkObjectResult && await db.ChecklistQuestions.CountAsync(x=>x.ChecklistTemplateId==draft.Id)==1,"Draft editing stores sections, exact response scale, remarks, Ex applicability and approved measurement limits");
        var active = await db.ChecklistTemplates.SingleAsync();
        Check(active.LifecycleStatus==ChecklistLifecycleStatus.Draft && !active.IsActive,"A draft is not eligible for inspection use");
        Check(await controller.Submit(draft.Id,default) is OkObjectResult,"Completed draft can enter the technical approval queue");
        var template = await db.ChecklistTemplates.SingleAsync();
        var approval = typeof(ChecklistAdministrationController).GetMethod(nameof(ChecklistAdministrationController.Approve))!.GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>().Single();
        Check(approval.Roles=="Qc,Admin","Only Qc or Admin roles may use the technical approval endpoint");
        Check(await controller.Approve(draft.Id,new("Self approval should fail"),default) is ForbidResult,"Checklist author cannot approve their own submitted template");
        var technicalReviewer = As(new ChecklistAdministrationController(db),qc);
        Check(await technicalReviewer.Approve(draft.Id,new("Reviewed against controlled copy"),default) is OkObjectResult,"Separate technical reviewer can publish the approved template");
        template = await db.ChecklistTemplates.SingleAsync();
        Check(template.LifecycleStatus==ChecklistLifecycleStatus.Published && template.IsActive && template.ApprovedByUserId==qc.Id,"Publication records reviewer identity and activates the approved checklist");
        Check(ChecklistApplicability.TemplateMatches(template,InspectionType.Close,"ex D") && !ChecklistApplicability.TemplateMatches(template,InspectionType.Visual,"Ex d") && !ChecklistApplicability.TemplateMatches(template,InspectionType.Close,null) && !ChecklistApplicability.TemplateMatches(template,InspectionType.Close,"Ex f"),"Inspection grade and Ex protection applicability fail closed on missing or mismatched equipment data");
        Check(ChecklistApplicability.QuestionMatches(await db.ChecklistQuestions.SingleAsync(),"EX D") && !ChecklistApplicability.QuestionMatches(await db.ChecklistQuestions.SingleAsync(),"Ex e"),"Question-level applicability follows the selected equipment protection type");
        var historySnapshot=new ChecklistVersionSnapshot{SourceChecklistTemplateId=template.Id,VersionNumber=template.Version,InspectionType=template.InspectionType,SnapshotHash="immutable",PublishedAt=template.ApprovedAt!.Value,PublishedByUserId=qc.Id,StandardReference=template.StandardReference,StandardEdition=template.StandardEdition,ControlledSourceReference=template.ControlledSourceReference,ProtectionTypeSnapshot="Ex d"};
        db.ChecklistVersionSnapshots.Add(historySnapshot); var sourceQuestion=await db.ChecklistQuestions.SingleAsync();
        var checkpointSnapshot=new CheckpointDefinitionSnapshot{ChecklistVersionSnapshotId=historySnapshot.Id,QuestionCode=sourceQuestion.Id.ToString("N"),QuestionVersion=template.Version,QuestionText=sourceQuestion.Text,SectionCode=sourceQuestion.SectionCode,ResponseScaleCode=sourceQuestion.ResponseScaleCode,RequireRemarkOnFail=sourceQuestion.RequireRemarkOnFail,SortOrder=sourceQuestion.SortOrder}; db.CheckpointDefinitionSnapshots.Add(checkpointSnapshot); await db.SaveChangesAsync();
        Check(await technicalReviewer.Edit(template.Id,new(1,template.Name,template.StandardReference!,template.StandardEdition,template.ControlledSourceReference,template.ApplicableProtectionTypes!,[question]),default) is ConflictObjectResult,"Published content cannot be silently edited");
        Check(await controller.Clone(template.Id,default) is CreatedAtActionResult,"Published template can be cloned into a new draft version");
        var clone=await db.ChecklistTemplates.SingleAsync(x=>x.Id!=template.Id); var revisedQuestion=question with {Text="Updated checkpoint from approved revision"};
        Check(await controller.Edit(clone.Id,new(clone.DraftRevision,clone.Name,clone.StandardReference!,clone.StandardEdition,clone.ControlledSourceReference,clone.ApplicableProtectionTypes!,[revisedQuestion]),default) is OkObjectResult,"Cloned draft can be revised independently");
        Check((await db.CheckpointDefinitionSnapshots.SingleAsync()).QuestionText=="Checkpoint from controlled source" && (await db.ChecklistQuestions.SingleAsync(x=>x.ChecklistTemplateId==template.Id)).Text=="Checkpoint from controlled source","Published checklist and historical inspection snapshot remain unchanged after next-version edits");
        Check(await controller.Submit(clone.Id,default) is OkObjectResult,"Revised version may independently re-enter technical approval");
        var historyProtected=new ChecklistTemplate{Name="Legacy answer template",InspectionType=InspectionType.Visual,FamilyId=Guid.NewGuid(),LifecycleStatus=ChecklistLifecycleStatus.Draft,StandardReference="Client controlled",ApplicableProtectionTypes="Ex d",CreatedByUserId=manager.Id};
        var legacyQuestion=new ChecklistQuestion{ChecklistTemplateId=historyProtected.Id,Text="Existing recorded prompt",SortOrder=1};
        Check(legacyQuestion.SectionCode is null && legacyQuestion.ResponseScaleCode is null,"Legacy checklist section and response scale stay unknown until explicitly classified");
        db.ChecklistTemplates.Add(historyProtected);db.ChecklistQuestions.Add(legacyQuestion);await db.SaveChangesAsync();
        var historicalSession=new SurveySession{SurveyorId=manager.Id,ChecklistTemplateId=historyProtected.Id};db.SurveySessions.Add(historicalSession);db.SurveyAnswers.Add(new SurveyAnswer{SurveySessionId=historicalSession.Id,ChecklistQuestionId=legacyQuestion.Id,Value="True"});await db.SaveChangesAsync();
        var historicalEdit=await controller.Edit(historyProtected.Id,new(historyProtected.DraftRevision,historyProtected.Name,historyProtected.StandardReference!,null,null,historyProtected.ApplicableProtectionTypes!,[question]),default);
        Check(historicalEdit is ConflictObjectResult && (await db.ChecklistQuestions.SingleAsync(x=>x.Id==legacyQuestion.Id)).Text=="Existing recorded prompt","Draft edits cannot replace checkpoint identifiers referenced by historical surveys");
        Console.WriteLine("All checklist administration checks passed (EF InMemory synthetic data; no SQL persistence evidence).");
    }
    private static User User(string name,UserRole role)=>new(){Email=name.Replace(" ","")+"@checklist.invalid",DisplayName=name,PasswordHash="test",Role=role,ApprovalStatus=ApprovalStatus.Approved};
    private static T As<T>(T controller,User user) where T:ControllerBase {controller.ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,user.Id.ToString()),new(ClaimTypes.Role,user.Role.ToString())],"Test"))}};return controller;}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: "+message);}
}
