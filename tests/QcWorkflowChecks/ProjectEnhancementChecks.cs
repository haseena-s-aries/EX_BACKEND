using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
internal static class ProjectEnhancementChecks {
 public static async Task Run() {
  await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
  User U(string name, UserRole role) => new() {DisplayName=name, Email=name+"@test.invalid", PasswordHash="test", Role=role,IsActive=true,ApprovalStatus=ApprovalStatus.Approved};
  var admin=U("Assigning Admin",UserRole.Admin);var manager=U("Assigning Manager",UserRole.Manager);var sv=U("Surveyor",UserRole.Surveyor);var sv2=U("Second Surveyor",UserRole.Surveyor);var qc=U("Reviewer",UserRole.Qc);qc.ManagerId=manager.Id;var qc2=U("Additional Reviewer",UserRole.Qc);var client=U("Client",UserRole.Client);var other=U("Unassigned",UserRole.Client);
  db.Users.AddRange(admin,manager,sv,sv2,qc,qc2,client,other);await db.SaveChangesAsync();
  ProjectController C(User u) {var c=new ProjectController(db,new FakePhotos(),NullLogger<ProjectController>.Instance);c.ControllerContext=new(){HttpContext=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,u.Id.ToString()),new(ClaimTypes.Role,u.Role.ToString())],"Test"))}};return c;}
  var ct=CancellationToken.None;var target=ProjectScheduling.Today.AddDays(20);
  var request=new UpsertProjectRequest("Mixed Case Project",null,"Job-x","Dubai","Ship",sv.Id,qc.Id,ClientName:"Client Co",TargetDate:target,AssignedClientIds:[client.Id]);
  var result=(CreatedAtActionResult)(await C(admin).CreateProject(request,ct)).Result!;var project=(ProjectResponse)result.Value!;
  var notices=await ProjectAssignmentNotices.Read(db,db.Projects,sv.Id,ct);
  Check(notices.Count==1&&notices[0].AssignedById==admin.Id&&notices[0].AssignedBy==admin.DisplayName&&notices[0].AssignedTo==sv.DisplayName,"Create assignment captures actual actor and recipient");
  Check(db.AuditLogs.Count(a=>a.Action==ProjectAssignmentNotices.Action)==3,"Exactly one notice for each existing assignment recipient");
  await C(admin).UpdateProject(project.Id,request,ct);
  Check(db.AuditLogs.Count(a=>a.Action==ProjectAssignmentNotices.Action)==3,"Unchanged assignments do not create duplicates");
  await C(manager).UpdateProject(project.Id,request with {AssignedSurveyorId=sv2.Id,AdditionalQcId=qc2.Id,UpdateAdditionalQcAssignment=true},ct);
  notices=await ProjectAssignmentNotices.Read(db,db.Projects,sv2.Id,ct);
  Check(notices.Count==1&&notices[0].AssignedBy==manager.DisplayName,"Reassignment records latest assigning Manager");
  Check((await ProjectAssignmentNotices.Read(db,db.Projects,qc2.Id,ct)).Count==1,"Additional QC receives assignment notice");
  var forbidden=(OkObjectResult)await C(sv).AssignmentNotifications(ct);
  Check(((IReadOnlyList<ProjectAssignmentNotice>)forbidden.Value!).Count==0,"Former Surveyor cannot retrieve inaccessible project notices");
  Check(((IReadOnlyList<ProjectAssignmentNotice>)((OkObjectResult)await C(other).AssignmentNotifications(ct)).Value!).Count==0,"Unassigned Client cannot access notices");
  var scheduling=new ProjectScheduling(db,Options.Create(new TenantFoundationOptions()));
  async Task Save(int delay,string? reason) {
   var saved=await C(sv2).SaveWorkSchedule(project.Id,new(Guid.NewGuid(),target.AddDays(-5),target.AddDays(delay),reason),scheduling,ct);
   Check(saved is OkObjectResult,"Save returns authoritative refreshed schedule");
  }
  Check(await C(sv2).SaveWorkSchedule(project.Id,new(Guid.NewGuid(),target.AddDays(-5),target.AddDays(10),null),scheduling,ct) is BadRequestObjectResult,"Delayed dates require a reason");
  await Save(10,"Vessel access");
  var p=await db.Projects.SingleAsync();
  ProjectScheduleSummary Summary()=>ProjectScheduling.Summarize(p.Status,p.TargetDate,p.AssignedSurveyorId,p.AdditionalSurveyorId,db.ProjectScheduleEntries.ToArray(),ProjectScheduling.Today);
  Check(Summary().DelayDays==10,"Initial delay is ten calendar days");
  await Save(5,"Revised vessel access");Check(Summary().DelayDays==5,"Revised delay is five calendar days");
  int rows=db.ProjectScheduleEntries.Count(),mail=db.ProjectDelayEmails.Count();
  await Save(0,null);Check(Summary().Status=="On Track"&&Summary().DelayDays==0&&Summary().LatestDelayReason==null,"On-track schedule clears stale delay indicator and reason");
  Check(p.TargetDate==target&&p.Status!=ProjectStatus.Completed,"Target and completion status remain unchanged");
  var unchanged=await C(sv2).SaveWorkSchedule(project.Id,new(Guid.NewGuid(),target.AddDays(-5),target,null),scheduling,ct);
  Check(unchanged is NoContentResult&&db.ProjectScheduleEntries.Count()==rows+1&&db.ProjectDelayEmails.Count()==mail,"Unchanged schedule does not duplicate history or alerts");
  Check(await C(sv).SaveWorkSchedule(project.Id,new(Guid.NewGuid(),target.AddDays(-5),target,null),scheduling,ct) is NotFoundResult,"Unassigned Surveyor cannot change schedule");
  Check(db.ProjectScheduleEntries.Count(e=>e.Kind=="Schedule")==3&&mail>0,"Delay history retained and Admin/Manager alerts queued");
  Console.WriteLine("All project enhancement checks passed (in-memory only).");
 }
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: "+message);}
}
