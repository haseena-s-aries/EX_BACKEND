using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class EquipmentReferenceDocumentChecks
{
    public static async Task Run()
    {
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var admin=User("Admin",UserRole.Admin);var manager=User("Manager",UserRole.Manager);var other=User("Other",UserRole.Manager);var surveyor=User("Surveyor",UserRole.Surveyor);
        var project=new Project{ProjectName="Doc test",Sector="Marine",JobNo="DOC-1",Location="Test",VesselOrSiteName="Site",CreatedByUserId=manager.Id,AssignedQcId=admin.Id,AssignedSurveyorId=surveyor.Id};
        var area=new ProjectArea{ProjectId=project.Id,AreaName="Deck"};var equipment=new AreaEquipment{ProjectId=project.Id,AreaId=area.Id,TagNumber="P-1",Type="Motor",CreatedByUserId=manager.Id};
        var otherProject=new Project{ProjectName="Private",Sector="Marine",JobNo="DOC-2",Location="Test",VesselOrSiteName="Site",CreatedByUserId=other.Id,AssignedQcId=admin.Id,AssignedSurveyorId=surveyor.Id};
        db.Users.AddRange(admin,manager,other,surveyor);db.Projects.AddRange(project,otherProject);db.ProjectAreas.Add(area);db.AreaEquipment.Add(equipment);await db.SaveChangesAsync();
        var store=new DocumentStorage();var opts=Options.Create(new EquipmentDocumentOptions{Enabled=true,ApprovalRoles=["Admin"],UseAuditEnabled=true});var offline=Options.Create(new OfflineInspectionOptions{Enabled=true});
        var managerApi=As(new EquipmentReferenceDocumentsController(db,store,opts,offline),manager);
        var file=UploadFile("cert.pdf",[0x25,0x50,0x44,0x46,0x2d,0x31]);
        var uploaded=await managerApi.Upload(new UploadReferenceDocumentRequest{EquipmentId=equipment.Id,Title="Certificate",CategoryCode="ExCertificate",Revision="R1",File=file},default);
        Check(uploaded is CreatedAtActionResult,"manager upload creates a draft reference document against existing equipment");
        var doc=db.EquipmentReferenceDocuments.Single();Check(doc.ProjectId==project.Id&&doc.AreaId==area.Id&&doc.Status==ReferenceDocumentStatus.Draft&&doc.Sha256.Length==64,"scope and file checksum are stored without creating equipment records");
        Check(await managerApi.Submit(doc.Id,default) is OkObjectResult,"uploader can submit the document for review");
        Check(await managerApi.Approve(doc.Id,new DocumentApprovalRequest("reviewed"),default) is ForbidResult,"manager cannot approve when approval role is Admin");
        var adminApi=As(new EquipmentReferenceDocumentsController(db,store,opts,offline),admin);
        Check(await adminApi.Approve(doc.Id,new DocumentApprovalRequest("approved"),default) is OkObjectResult,"independent configured reviewer approves submitted document");
        var list=await adminApi.List(equipment.Id,null,default);Check(list is OkObjectResult,"authorized equipment document list is available");
        var outsider=As(new EquipmentReferenceDocumentsController(db,store,opts,offline),other);
        Check(await outsider.Download(doc.Id,default) is NotFoundResult,"unrelated project user cannot download a document by guessed ID");
        var adminOwn=await adminApi.Upload(new UploadReferenceDocumentRequest{EquipmentId=equipment.Id,Title="New certificate",CategoryCode="ExCertificate",Revision="R2",SupersedesDocumentId=doc.Id,File=UploadFile("r2.pdf",[0x25,0x50,0x44,0x46,0x2d,0x32])},default);
        var second=db.EquipmentReferenceDocuments.OrderBy(x=>x.UploadedAt).Last();
        Check(adminOwn is CreatedAtActionResult&&second.SupersedesDocumentId==doc.Id,"replacement revision preserves the original file and points to its predecessor");
        Check(await adminApi.Submit(second.Id,default) is OkObjectResult,"replacement draft can be submitted");
        Check(await adminApi.Approve(second.Id,new DocumentApprovalRequest(null),default) is ForbidResult,"uploader cannot self-approve a replacement revision");
        var managerReviewer=As(new EquipmentReferenceDocumentsController(db,store,Options.Create(new EquipmentDocumentOptions{Enabled=true,ApprovalRoles=["Manager"]}),offline),manager);
        var managerApproval=await managerReviewer.Approve(second.Id,new DocumentApprovalRequest(null),default);Check(managerApproval is OkObjectResult,$"configured role can approve when reviewer differs from uploader (result {managerApproval.GetType().Name})");
        Check(db.EquipmentReferenceDocuments.Single(x=>x.Id==doc.Id).Status==ReferenceDocumentStatus.Superseded&&second.Status==ReferenceDocumentStatus.Approved,"approval supersedes prior current revision without deleting it");
        Console.WriteLine("PASS: equipment reference document API checks");
    }
    private static User User(string n,UserRole role)=>new(){Email=n+"@doc.test",DisplayName=n,PasswordHash="test",Role=role,ApprovalStatus=ApprovalStatus.Approved};
    private static T As<T>(T c,User u) where T:ControllerBase{c.ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,u.Id.ToString()),new Claim(ClaimTypes.Role,u.Role.ToString())],"test"))}};return c;}
    private static FormFile UploadFile(string name,byte[] bytes){var stream=new MemoryStream(bytes);var file=new FormFile(stream,0,bytes.Length,"file",name){Headers=new HeaderDictionary()};file.Headers.ContentType="application/pdf";return file;}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    private sealed class DocumentStorage:IProjectPhotoStorage
    {
        private readonly Dictionary<string,byte[]> files=new();public int MaxFilesPerRequest=>20;
        public Task<StoredProjectPhoto> SaveReferenceDocumentAsync(IFormFile file,Guid projectId,Guid documentId,CancellationToken ct){using var ms=new MemoryStream();file.CopyTo(ms);var path=$"reference-documents/{projectId:N}/{documentId:N}/{file.FileName}";files[path]=ms.ToArray();return Task.FromResult(new StoredProjectPhoto(file.FileName,path));}
        public Task<StoredProjectPhoto> SaveHazardousAreaPlanAsync(IFormFile f,Guid p,CancellationToken c)=>throw new NotSupportedException();public Task<StoredProjectPhoto> SaveAsync(IFormFile f,Guid p,Guid a,CancellationToken c)=>throw new NotSupportedException();public Task<StoredProjectPhoto> SaveInspectionAsync(IFormFile f,Guid p,Guid i,CancellationToken c)=>throw new NotSupportedException();
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string path,CancellationToken ct=default)=>Task.FromResult<StoredProjectPhotoContent?>(files.TryGetValue(path,out var bytes)?new(new MemoryStream(bytes),"application/pdf"):null);
        public Task DeleteAsync(string path,CancellationToken ct=default){files.Remove(path);return Task.CompletedTask;}
    }
}
