using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;
using System.Security.Cryptography;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using PdfSharp.Pdf.IO;

internal static class OperationalReportChecks
{
    private static int checks;
    private static void Check(bool ok,string name){checks++;if(!ok)throw new InvalidOperationException("Operational report check failed: "+name);Console.WriteLine("PASS: "+name);}
    public static async Task Run()
    {
        var dbOptions=new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db=new SurveyDbContext(dbOptions);
        var inspector=new User{Email="daily-inspector@example.invalid",DisplayName="Daily Inspector",PasswordHash="synthetic",Role=UserRole.Surveyor,ApprovalStatus=ApprovalStatus.Approved};
        var qc=new User{Email="daily-qc@example.invalid",DisplayName="Daily QC",PasswordHash="synthetic",Role=UserRole.Qc,ApprovalStatus=ApprovalStatus.Approved};db.Users.AddRange(inspector,qc);
        var project=new Project{ProjectName="Synthetic reporting",JobNo="SYN-REPORT",Sector="Test",Location="Test yard",VesselOrSiteName="Synthetic Site",CreatedByUserId=inspector.Id,AssignedSurveyorId=inspector.Id,AssignedQcId=qc.Id};db.Projects.Add(project);
        var area=new ProjectArea{ProjectId=project.Id,AreaName="Area A"};db.ProjectAreas.Add(area);
        var equipment=new AreaEquipment{ProjectId=project.Id,AreaId=area.Id,SequenceNo=1,TagNumber="EQ-001",Type="Motor",CreatedByUserId=inspector.Id};db.AreaEquipment.Add(equipment);
        var plan=new InspectionPlan{ProjectId=project.Id,Name="Synthetic plan"};db.InspectionPlans.Add(plan);
        var task=new InspectionTask{InspectionPlanId=plan.Id,AreaEquipmentId=equipment.Id,AssignedUserId=inspector.Id,ScheduledAt=DateTimeOffset.UtcNow};db.InspectionTasks.Add(task);
        var equipment2=new AreaEquipment{ProjectId=project.Id,AreaId=area.Id,SequenceNo=2,TagNumber="EQ-002",Type="Motor",CreatedByUserId=inspector.Id};db.AreaEquipment.Add(equipment2);
        db.InspectionTasks.Add(new InspectionTask{InspectionPlanId=plan.Id,AreaEquipmentId=equipment2.Id,AssignedUserId=inspector.Id,ScheduledAt=DateTimeOffset.UtcNow,DueAt=DateTimeOffset.UtcNow.AddDays(-1),IsInaccessible=true,CarryForwardReason="Synthetic access delay"});
        var template=new ChecklistTemplate{Name="Synthetic approved checklist",InspectionType=InspectionType.Close};db.ChecklistTemplates.Add(template);
        var checklist=new ChecklistVersionSnapshot{SourceChecklistTemplateId=template.Id,VersionNumber=2,InspectionType=InspectionType.Close,SnapshotHash="snapshot-v2",PublishedAt=DateTimeOffset.UtcNow,PublishedByUserId=qc.Id,StandardReference="Approved controlled source"};db.ChecklistVersionSnapshots.Add(checklist);
        var activity=new InspectionActivity{InspectionTaskId=task.Id,Grade=InspectionType.Close,InspectorUserId=inspector.Id,InspectorNameSnapshot=inspector.DisplayName,StartedAt=DateTimeOffset.UtcNow.AddHours(-2),CompletedAt=DateTimeOffset.UtcNow,ChecklistVersionId=checklist.Id,ReviewStatus=InspectionActivityReviewStatus.Accepted};
        var first=new InspectionActivity{InspectionTaskId=task.Id,Grade=InspectionType.Visual,InspectorUserId=inspector.Id,StartedAt=DateTimeOffset.UtcNow.AddDays(-1),CompletedAt=DateTimeOffset.UtcNow.AddDays(-1),CreatedAt=DateTimeOffset.UtcNow.AddDays(-1),UpdatedAt=DateTimeOffset.UtcNow.AddDays(-1),ChecklistVersionId=checklist.Id,ReviewStatus=InspectionActivityReviewStatus.Submitted};
        db.InspectionActivities.AddRange(activity,first);
        var activityRevision=new InspectionActivityRevision{InspectionActivityId=activity.Id,RevisionNumber=2,CreatedByUserId=inspector.Id,ContentHash="synthetic-hash"};db.InspectionActivityRevisions.Add(activityRevision);
        var checkpoint=new CheckpointDefinitionSnapshot{ChecklistVersionSnapshotId=checklist.Id,QuestionCode="CP-1",QuestionText="Synthetic checkpoint",SectionCode="Apparatus",SortOrder=1};db.CheckpointDefinitionSnapshots.Add(checkpoint);
        db.CheckpointResponseRevisions.Add(new CheckpointResponseRevision{ActivityRevisionId=activityRevision.Id,CheckpointDefinitionSnapshotId=checkpoint.Id,ResponseCode="Pass",Observation="Checked",RecordedByUserId=inspector.Id});
        db.ActivityReviewEvents.Add(new ActivityReviewEvent{InspectionActivityId=activity.Id,Decision=InspectionActivityReviewStatus.Accepted,ActorUserId=qc.Id,Comments="Reviewed synthetic activity"});
        await db.SaveChangesAsync();
        var claims=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,inspector.Id.ToString()),new Claim(ClaimTypes.Role,"Surveyor")],"synthetic"));
        var reports=new OperationalReports(db);
        var day=DateOnly.FromDateTime(DateTime.UtcNow);
        var progress=await reports.Daily(project.Id,day,claims,default);
        Check(progress is not null&&progress.Planned==2&&progress.Completed==1&&progress.CloseCompleted==1&&progress.QcApproved==1,"daily planned/completed/grade/QC counts distinguish acceptance from compliance");
        Check(progress!.Delays.Count==1&&progress.CarryForwardItems.Count==1&&progress.Inaccessible==1,"daily progress identifies overdue delays, inaccessible items and carry-forward reasons");
        Check(progress!.CumulativeCompleted==1,"repeated activity attempts do not double-count one task");
        var pdf=await reports.InspectionPdf(activity.Id,claims,default);
        Check(pdf is {Length:>500}&&pdf.AsSpan(0,5).SequenceEqual("%PDF-"u8),"inspection test record is generated as a readable PDF stream");
        ProjectReportPdf.EnsureFonts();using(var document=PdfReader.Open(new MemoryStream(pdf!),PdfDocumentOpenMode.Import))Check(document.Pages.Count>0&&document.Info.Title.Contains("Inspection Test Record"),"inspection report PDF opens and carries report metadata");
        var handover=OperationalReports.BuildHandoverZip("SYN-REPORT",DateTimeOffset.UtcNow,new Dictionary<string,byte[]>{{"report/sample.pdf",pdf!},{"register/haer.csv","synthetic"u8.ToArray()}},"Prepared for review");
        using(var zip=new ZipArchive(new MemoryStream(handover),ZipArchiveMode.Read))
        {
            Check(zip.GetEntry("manifest.json") is not null&&zip.GetEntry("report/sample.pdf") is not null,"handover ZIP contains report and manifest");
            using var reader=new StreamReader(zip.GetEntry("manifest.json")!.Open());using var json=JsonDocument.Parse(reader.ReadToEnd());
            Check(json.RootElement.GetProperty("final").GetBoolean()==false&&json.RootElement.GetProperty("compliance").GetString()=="Not asserted by this package","handover manifest never claims final status or compliance");
            Check(json.RootElement.GetProperty("files").GetArrayLength()==2,"handover manifest lists each artifact for checksum verification");
            var firstArtifact=json.RootElement.GetProperty("files")[0];var listedName=firstArtifact.GetProperty("file").GetString()!;
            using var artifact=zip.GetEntry(listedName)!.Open();var actualHash=Convert.ToHexString(SHA256.HashData(artifact));
            Check(actualHash==firstArtifact.GetProperty("sha256").GetString(),"handover manifest checksum matches the bundled artifact bytes");
        }
        var archiveStore=new MemoryReportArtifactStorage();
        var reportController=Controller(db,claims,archiveStore);
        var generatedOne=(FileContentResult)(await reportController.InspectionRecord(activity.Id,default));
        var generatedTwo=(FileContentResult)(await reportController.InspectionRecord(activity.Id,default));
        var archived=await db.GeneratedReportArtifacts.AsNoTracking().Where(x=>x.ReportType=="InspectionTestRecord"&&x.LinkedEntityId==activity.Id).OrderBy(x=>x.Revision).ToListAsync();
        Check(generatedOne.ContentType=="application/pdf"&&archived.Count==2&&archived[0].Revision==1&&archived[1].Revision==2,"each generated inspection PDF receives an immutable numbered archive revision");
        Check(archived.All(x=>x.ApprovalState=="Draft"&&x.GeneratedByUserId==inspector.Id&&x.Sha256==Convert.ToHexString(SHA256.HashData(archiveStore.Files[x.StorageKey]))),"archive stores generator identity, draft state and matching SHA-256 metadata");
        var retrieved=(FileStreamResult)(await reportController.DownloadArtifact(project.Id,archived[0].Id,default));
        using(var retrievedBytes=new MemoryStream()){await retrieved.FileStream.CopyToAsync(retrievedBytes);Check(retrievedBytes.ToArray().SequenceEqual(archiveStore.Files[archived[0].StorageKey]),"authorized artifact retrieval returns the exact archived PDF bytes");}
        var stranger=new User{Email="stranger-reports@example.invalid",DisplayName="Unassigned",PasswordHash="synthetic",Role=UserRole.Manager,ApprovalStatus=ApprovalStatus.Approved};db.Users.Add(stranger);await db.SaveChangesAsync();
        var strangerController=Controller(db,new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,stranger.Id.ToString()),new Claim(ClaimTypes.Role,"Manager")],"synthetic")),archiveStore);
        Check(await strangerController.DownloadArtifact(project.Id,archived[0].Id,default) is NotFoundResult,"unassigned manager cannot download an archived project report");
        Check(generatedTwo.FileDownloadName.EndsWith("-r2.pdf"),"report download filename exposes the generated revision");
        var archiveRoot=Path.Combine(Path.GetTempPath(),"ex-survey-report-store-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(archiveRoot);
        try
        {
            var diskStore=new GeneratedReportArtifactStorage(Options.Create(new GeneratedReportArtifactStorageSettings{RootPath=archiveRoot}),new TestWebEnvironment(Path.GetTempPath()));
            var bytes="synthetic immutable artifact"u8.ToArray();var stored=await diskStore.SaveAsync(project.Id,Guid.NewGuid(),"archive.pdf",bytes,default);
            var reopened=await diskStore.OpenReadAsync(stored.StorageKey,default);using var stream=new MemoryStream();await reopened!.Stream.CopyToAsync(stream);
            Check(stream.ToArray().SequenceEqual(bytes)&&stored.Sha256==Convert.ToHexString(SHA256.HashData(bytes)),"filesystem artifact storage preserves exact bytes and returns its checksum");
            try{await diskStore.OpenReadAsync("../../outside.pdf",default);Check(false,"artifact storage rejects path traversal");}catch(InvalidOperationException){Check(true,"artifact storage rejects path traversal");}
        }
        finally{Directory.Delete(archiveRoot,true);}
        if(Environment.GetEnvironmentVariable("EX_SURVEY_WRITE_REPORT_SAMPLES")=="1")
        {
            var output=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","artifacts","operational-report-samples"));Directory.CreateDirectory(output);
            await File.WriteAllBytesAsync(Path.Combine(output,"Synthetic-Inspection-Test-Record.pdf"),pdf!);
            await File.WriteAllBytesAsync(Path.Combine(output,"Synthetic-Campaign-Handover.zip"),handover);
            Console.WriteLine("Synthetic sample artifacts written to "+output);
        }
        Console.WriteLine($"Operational reporting checks passed: {checks} checks with synthetic in-memory data.");
    }

    private static OperationalReportsController Controller(SurveyDbContext db,ClaimsPrincipal user,MemoryReportArtifactStorage archive)
    {
        var controller=new OperationalReportsController(db,new EmptyPhotoStorage(),archive,NullLogger<OperationalReportsController>.Instance);
        controller.ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=user}};return controller;
    }

    private sealed class MemoryReportArtifactStorage : IGeneratedReportArtifactStorage
    {
        public Dictionary<string,byte[]> Files {get;}=new(StringComparer.Ordinal);
        public Task<ArchivedReportFile> SaveAsync(Guid projectId,Guid artifactId,string fileName,byte[] content,CancellationToken ct)
        {var key=$"{projectId:N}/{artifactId:N}_{fileName}";Files.Add(key,content.ToArray());return Task.FromResult(new ArchivedReportFile(key,Convert.ToHexString(SHA256.HashData(content))));}
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string storageKey,CancellationToken ct)=>Task.FromResult(Files.TryGetValue(storageKey,out var bytes)?new StoredProjectPhotoContent(new MemoryStream(bytes.ToArray()),"application/pdf"):null);
    }

    private sealed class EmptyPhotoStorage : IProjectPhotoStorage
    {
        public int MaxFilesPerRequest=>20;
        public Task<StoredProjectPhoto> SaveAsync(IFormFile file,Guid projectId,Guid areaId,CancellationToken ct)=>throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveHazardousAreaPlanAsync(IFormFile file,Guid projectId,CancellationToken ct)=>throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveInspectionAsync(IFormFile file,Guid projectId,Guid inspectionId,CancellationToken ct)=>throw new NotSupportedException();
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string relativePath,CancellationToken ct=default)=>Task.FromResult<StoredProjectPhotoContent?>(null);
        public Task DeleteAsync(string relativePath,CancellationToken ct=default)=>Task.CompletedTask;
    }

    private sealed class TestWebEnvironment(string contentRoot):IWebHostEnvironment
    {
        public string ApplicationName{get;set;}="ExSurveySyntheticTests";
        public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
        public string WebRootPath{get;set;}=contentRoot;
        public string EnvironmentName{get;set;}="Test";
        public string ContentRootPath{get;set;}=contentRoot;
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
}
