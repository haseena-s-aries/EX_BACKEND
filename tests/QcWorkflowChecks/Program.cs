using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

if (args.Contains("--inspect-backup")) {await BackupInspection.Run(args.Contains("--restore-approved-audit"), args.Contains("--read-audit"));return;}
if(args.Contains("--equipment-sequence-checks")){await EquipmentSequenceChecks.Run();return;}
if(args.Contains("--project-enhancement-checks")){await ProjectEnhancementChecks.Run();return;}
var ct = CancellationToken.None;
if (args.Contains("--user-access-checks")) {await UserAccessChecks.Run(); return;}
if (args.Contains("--account-security-checks")) {await AccountSecurityChecks.Run(); return;}
if (args.Contains("--qc-shared-modules-checks")) {await QcSharedModulesChecks.Run(); return;}
if (args.Contains("--project-multi-client-checks")) {await ProjectMultiClientChecks.Run(); return;}
if (args.Contains("--report-workflow-checks")) { await ReportWorkflowChecks.Run(); return; }
if (args.Contains("--defect-report-checks")) { await DefectReportChecks.Run(); return; }
if (args.Contains("--sample-checklist-checks")) { await SampleChecklistChecks.Run(); return; }
if (args.Contains("--database-startup-checks")) { await DatabaseStartupChecks.Run(); return; }
if (args.Contains("--project-http-checks")) { await ProjectUploadHttpChecks.Run(); return; }
if (args.Contains("--checklist-seed-checks")) { await ChecklistSeedChecks.Run(); return; }
if (args.Contains("--job-centric-equipment-checks")) { await JobCentricEquipmentChecks.Run(); return; }
if (args.Contains("--bootstrap-checks")) { await BootstrapChecks.Run(); return; }
if (args.Contains("--security-checks")) { await OtpSecurityChecks.Run(); return; }
if (args.Contains("--migration-script-check")) { MigrationChecks.RunOtpSecurityScript(); return; }
if (args.Contains("--tenant-migration-script-check")) { MigrationChecks.RunTenantFoundationScript(); return; }
if (args.Contains("--tenant-foundation-checks")) { await TenantFoundationChecks.Run(); return; }
if (args.Contains("--tenant-backfill-checks")) { await TenantBackfillChecks.Run(); return; }
if (args.Contains("--local-sql-diagnostic")) { await LocalSqlDiagnostics.Run(); return; }
if (args.Contains("--localdb-sql-diagnostic")) { await LocalSqlDiagnostics.Run("Server=(localdb)\\ExSurveySprint2;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Connect Timeout=5;Application Name=ExSurvey-LocalDB-Diagnostic"); return; }
if (args.Contains("--prepare-localdb-test-database")) { await LocalSqlDiagnostics.PrepareIsolatedTestDatabaseAsync(); return; }
if (args.Contains("--inspection-activity-sql-checks")) { await InspectionActivitySqlChecks.Run(); return; }
if (args.Contains("--seed-localdb-ui-e2e")) { await LocalDbUiSeed.Run(); return; }
if (args.Contains("--otp-sql-checks")) { await OtpSqlChecks.Run(); return; }
if (args.Contains("--endpoint-scope-checks")) { EndpointScopeChecks.Run(); return; }
if (args.Contains("--inspection-activity-checks")) { await InspectionActivityChecks.Run(); return; }
if (args.Contains("--offline-inspection-checks")) { await OfflineInspectionChecks.Run(); return; }
if (args.Contains("--equipment-reference-document-checks")) { await EquipmentReferenceDocumentChecks.Run(); return; }
if (args.Contains("--equipment-identification-checks")) { await EquipmentIdentificationChecks.Run(); return; }
if (args.Contains("--inspection-planning-checks")) { InspectionPlanningChecks.Run(); return; }
if (args.Contains("--inspection-planning-api-checks")) { await InspectionPlanningApiChecks.Run(); return; }
if (args.Contains("--write-tenant-matrix"))
{
    var index = Array.IndexOf(args, "--write-tenant-matrix");
    var outputPath = index + 1 < args.Length ? args[index + 1] : Path.Combine("..", "..", "..", "..", "docs", "ex-survey-modernization", "TENANT_ENDPOINT_ENFORCEMENT_MATRIX.md");
    EndpointScopeChecks.WriteTenantMatrix(Path.GetFullPath(outputPath));
    return;
}
if (args.Contains("--client-checks")) {await ClientDashboardChecks.Run(); return;}
if (args.Contains("--client-sql")) {await ClientDashboardChecks.Sql(); return;}
if (args.Contains("--report-checks")) {await ReportChecks.Run(); return;}
if (args.Contains("--operational-report-checks")||args.Contains("--write-operational-report-samples")) {if(args.Contains("--write-operational-report-samples"))Environment.SetEnvironmentVariable("EX_SURVEY_WRITE_REPORT_SAMPLES","1");await OperationalReportChecks.Run(); return;}
if (args.Contains("--checklist-admin-checks")) {await ChecklistAdministrationChecks.Run(); return;}
if (args.Contains("--report-sql")) {await ReportChecks.SqlPreview(); return;}
if (args.Contains("--verify-sql") || args.Contains("--repair-sole-reporting-link"))
{
    await SqlAssignmentVerification.Run(args.Contains("--repair-sole-reporting-link"));
    return;
}
await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
var manager = MakeUser("Manager", UserRole.Manager);
var unrelated = MakeUser("Other Manager", UserRole.Manager);
var admin = MakeUser("Admin", UserRole.Admin);
var qc = MakeUser("QC", UserRole.Qc); qc.ManagerId = manager.Id;
var surveyor = MakeUser("Surveyor", UserRole.Surveyor);
var client = MakeUser("Client", UserRole.Client);
db.Users.AddRange(manager, unrelated, admin, qc, surveyor, client);
var project = new Project {ProjectName = "Reporting project", JobNo = "JOB-1", Sector = "Marine", Location = "Dubai",
    VesselOrSiteName = "Site", CreatedByUserId = admin.Id, AssignedQcId = qc.Id,
    AssignedSurveyorId = surveyor.Id, AssignedClientId = client.Id};
var area = new ProjectArea {ProjectId = project.Id, AreaName = "Deck"};
var equipment = new AreaEquipment {ProjectId = project.Id, AreaId = area.Id, SequenceNo = 1, TagNumber = "TAG-1", Type = "Motor", CreatedByUserId = surveyor.Id};
var untouched = new AreaEquipment {ProjectId = project.Id, AreaId = area.Id, SequenceNo = 2, TagNumber = "TAG-2", Type = "Light", CreatedByUserId = surveyor.Id};
var emptyProject = new Project {ProjectName = "Empty", JobNo = "JOB-2", Sector = "Marine", Location = "Dubai", VesselOrSiteName = "Site",
    CreatedByUserId = manager.Id, AssignedQcId = qc.Id, AssignedSurveyorId = surveyor.Id, AssignedClientId = client.Id};
db.Projects.AddRange(project, emptyProject); db.ProjectAreas.Add(area); db.AreaEquipment.AddRange(equipment, untouched);
var template = new ChecklistTemplate {Name = "Visual", InspectionType = InspectionType.Visual};
var question = new ChecklistQuestion {ChecklistTemplateId = template.Id, Text = "Check enclosure", SortOrder = 1};
db.ChecklistTemplates.Add(template); db.ChecklistQuestions.Add(question); await db.SaveChangesAsync(ct);
var storage = new FakePhotos();
var managerPending = As(new QcController(db, storage), manager);
Check(!Value<IEnumerable<QcProjectSummaryResponse>>(await managerPending.GetProjects(ct, pendingOnly: true)).Any(), "Pending queue excludes projects without submitted inspections");
var monitor = As(new ManagerQcReviewController(db, storage), manager);
var otherMonitor = As(new ManagerQcReviewController(db, storage), unrelated);
var adminMonitor = As(new ManagerQcReviewController(db, storage), admin);
Check(Value<IEnumerable<ManagerQcProject>>(await monitor.Projects(ct)).Count() == 2, "Manager scope includes QC reports and own projects");
Check(!Value<IEnumerable<ManagerQcProject>>(await otherMonitor.Projects(ct)).Any(), "Unrelated manager sees no projects");
Check((await otherMonitor.Project(project.Id, ct)).Result is NotFoundResult, "Project ID cannot bypass scope");
Check((await otherMonitor.Area(project.Id, area.Id, ct)).Result is NotFoundResult, "Area ID cannot bypass scope");
Check((await otherMonitor.Inspection(equipment.Id, ct)).Result is NotFoundResult, "Equipment ID cannot bypass scope");
Check(Value<ManagerQcInspection>(await monitor.Inspection(untouched.Id, ct)).Inspection is null, "Unstarted equipment remains viewable");
var projectController = As(new ProjectController(db, storage, NullLogger<ProjectController>.Instance), manager);
var options = Value<IEnumerable<ProjectAssignableUserResponse>>(await projectController.GetAssignmentOptions(ct));
Check(options.Single(u => u.Id == qc.Id).ManagerName == manager.DisplayName, "Assignment options include manager name");
var survey = As(new SurveyorController(db, storage, NullLogger<SurveyorController>.Instance), surveyor);
var payload = new SaveEquipmentInspectionRequest(DateTimeOffset.UtcNow, "Surveyor", "Ready", [new(question.Id, true, "Checked")], true);
var created = await survey.CreateInspection(equipment.Id, payload, ct);
Check(created.Result is ObjectResult {StatusCode: 201}, "Existing surveyor creation succeeds");
var inspectionId = db.SurveySessions.Single().Id;
var reviewController = As(new QcController(db, storage), qc);
Check(Value<IEnumerable<QcProjectSummaryResponse>>(await managerPending.GetProjects(ct, pendingOnly: true)).Single().ProjectId == project.Id, "Reporting Manager sees submitted project in pending queue");
Check(Value<IEnumerable<QcProjectSummaryResponse>>(await reviewController.GetProjects(ct, pendingOnly: true)).Single().ProjectId == project.Id, "Assigned QC sees the same submitted project in pending queue");
Check(Value<QcDashboardResponse>(await managerPending.GetDashboard(ct)).AwaitingQcReview == 1 &&
      Value<QcDashboardResponse>(await reviewController.GetDashboard(ct)).AwaitingQcReview == 1,
      "Sidebar source counts submitted inspections for both assigned QC and reporting Manager");
Check(Value<QcDashboardResponse>(await As(new QcController(db, storage), unrelated).GetDashboard(ct)).AwaitingQcReview == 0,
      "Sidebar source excludes unrelated Manager projects");
var navigation = await db.Projects.AsNoTracking().Include(p => p.AssignedQc).ThenInclude(q => q.Manager).SingleAsync(p => p.Id == project.Id);
Check(navigation.AssignedQc.Id == qc.Id && navigation.AssignedQc.Manager!.Id == manager.Id, "AssignedQc to Manager EF navigation uses existing foreign keys");
Check(Value<ManagerQcCounts>(await monitor.Dashboard(ct)).AwaitingQcReview == 1, "Pending counts exclude unstarted equipment");
db.SurveySessions.Single().DueDate = DateTimeOffset.UtcNow.AddDays(-1); await db.SaveChangesAsync(ct);
Check(Value<ManagerQcCounts>(await monitor.Dashboard(ct)).Overdue == 1, "Overdue counts include open submitted inspections");
await reviewController.StartReview(inspectionId, ct);
Check(Value<QcDashboardResponse>(await managerPending.GetDashboard(ct)).AwaitingQcReview == 0, "Sidebar pending count excludes in-review work");
Check(Value<IEnumerable<QcProjectSummaryResponse>>(await managerPending.GetProjects(ct, pendingOnly: true)).Any(p => p.ProjectId == project.Id), "In-review work remains in shared pending queue");
Check(Value<ManagerQcCounts>(await monitor.Dashboard(ct)).InReview == 1, "In-review count updates");
await reviewController.RequestChanges(inspectionId, new("Replace damaged seal", QcSeverity.High), ct);
Check(Value<QcDashboardResponse>(await managerPending.GetDashboard(ct)).AwaitingQcReview == 0, "Sidebar pending count excludes changes requested");
Check(Value<IEnumerable<QcProjectSummaryResponse>>(await managerPending.GetProjects(ct, pendingOnly: true)).Any(p => p.ProjectId == project.Id), "Changes-requested work remains in shared pending queue");
await reviewController.UpdateComments(inspectionId, new("Replace seal and retest"), ct);
Check(Value<ManagerQcCounts>(await monitor.Dashboard(ct)).ChangesRequested == 1, "Changes-requested count updates");
await survey.UpdateInspection(inspectionId, payload with {Comments = "Seal replaced"}, ct);
Check(Value<QcDashboardResponse>(await managerPending.GetDashboard(ct)).AwaitingQcReview == 1, "Sidebar count increases on Surveyor resubmission");
Check(Value<IEnumerable<QcProjectSummaryResponse>>(await managerPending.GetProjects(ct, pendingOnly: true)).Single().QcReviewStatus == QcReviewStatus.PendingReview, "Resubmitted work appears as pending for Manager");
Check(Value<ManagerQcCounts>(await monitor.Dashboard(ct)).AwaitingQcReview == 1, "Resubmission returns to pending review");
await reviewController.StartReview(inspectionId, ct);
await reviewController.Approve(inspectionId, new("Retest passed"), ct);
Check(Value<QcDashboardResponse>(await managerPending.GetDashboard(ct)).AwaitingQcReview == 0, "Sidebar count excludes approved inspections");
Check(!Value<IEnumerable<QcProjectSummaryResponse>>(await managerPending.GetProjects(ct, pendingOnly: true)).Any(), "Fully approved projects are removed from pending queue");
Check(Value<IEnumerable<QcProjectSummaryResponse>>(await reviewController.GetProjects(ct)).Any(p => p.ProjectId == project.Id), "Existing QC assigned-project history is preserved");
var dashboard = Value<ManagerQcCounts>(await monitor.Dashboard(ct));
Check(dashboard.Approved == 1 && dashboard.Overdue == 0, "Approval counts and overdue exclusion");
var detail = Value<ManagerQcInspection>(await monitor.Inspection(equipment.Id, ct));
Check(detail.Inspection!.ChecklistAnswers.Single().Value == "True", "Monitoring reuses checklist reader");
Check(detail.History.Select(h => h.Action).SequenceEqual(new[] {"Surveyor Submitted", "QC Started Review", "QC Requested Changes", "QC Comments Updated", "Surveyor Resubmitted", "QC Started Review", "QC Approved"}), "Complete multi-round timeline");
Check(detail.History.Single(h => h.Action == "QC Requested Changes").Comments == "Replace damaged seal", "Previous QC comments preserved");
Check(detail.History.Single(h => h.Action == "QC Requested Changes").Severity == QcSeverity.High, "Previous severity preserved");
// Monitoring must retain separate inspection versions for the same equipment.
var newerSession = new SurveySession {AreaEquipmentId = equipment.Id, SurveyorId = surveyor.Id,
    ChecklistTemplateId = template.Id, SubmittedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow.AddDays(1)};
db.SurveySessions.Add(newerSession); await db.SaveChangesAsync(ct);
var historicalDetail = Value<ManagerQcInspection>(await monitor.Inspection(equipment.Id, ct, inspectionId));
Check(historicalDetail.Inspection!.InspectionId == inspectionId && historicalDetail.History.Any(h => h.Action == "QC Approved"), "Inspection-specific history retains old approvals");
Check(Value<ManagerQcInspection>(await monitor.Inspection(equipment.Id, ct)).Inspection!.InspectionId == newerSession.Id, "Default equipment detail selects latest inspection without throwing");
Check((await monitor.Inspection(untouched.Id, ct, inspectionId)).Result is NotFoundResult, "Inspection ID cannot cross equipment boundaries");
Check(await otherMonitor.Records(project.Id, ct) is NotFoundResult, "Review records enforce existing project scope");
var recordsResult = (OkObjectResult)await monitor.Records(project.Id, ct);
var recordJson = System.Text.Json.JsonSerializer.SerializeToElement(recordsResult.Value);
var sameEquipment = recordJson.EnumerateArray().Where(r => r.GetProperty("Equipment").GetProperty("EquipmentId").GetGuid() == equipment.Id).ToArray();
Check(sameEquipment.Length == 2, "Records include distinct historical inspections for one equipment item");
Check(sameEquipment.Single(r => r.GetProperty("Equipment").GetProperty("InspectionId").GetGuid() == inspectionId).GetProperty("HasReview").GetBoolean(), "Approved history is classified as reviewed");
Check(!sameEquipment.Single(r => r.GetProperty("Equipment").GetProperty("InspectionId").GetGuid() == newerSession.Id).GetProperty("HasReview").GetBoolean(), "Submission alone is not classified as reviewed");

db.SurveySessions.Remove(newerSession); await db.SaveChangesAsync(ct);

Check((await survey.UpdateInspection(inspectionId, payload, ct)).Result is ConflictObjectResult, "Approved checklist remains locked");
Check((await reviewController.RequestChanges(inspectionId, new("Try edit", QcSeverity.Low), ct)).Result is ConflictObjectResult, "Approved QC remains locked");
var photo = new SurveyFile {SurveySessionId = inspectionId, FileName = "image.png", StorageKey = "image.png", ContentType = "image/png", UploadedById = surveyor.Id, Category = FileCategory.EquipmentPhoto};
db.Files.Add(photo); await db.SaveChangesAsync(ct);
Check(await monitor.Photo(inspectionId, photo.Id, ct) is FileStreamResult, "Relevant manager can view inspection photo");
Check(await otherMonitor.Photo(inspectionId, photo.Id, ct) is NotFoundResult, "Unrelated manager cannot view photo");
var managerReview = As(new QcController(db, storage), manager);
var unrelatedReview = As(new QcController(db, storage), unrelated);
var adminReview = As(new QcController(db, storage), admin);
var otherQc = MakeUser("Other QC", UserRole.Qc); db.Users.Add(otherQc); await db.SaveChangesAsync(ct);
var otherQcReview = As(new QcController(db, storage), otherQc);
Check(Value<IEnumerable<QcProjectSummaryResponse>>(await managerReview.GetProjects(ct)).Single(p => p.ProjectId == project.Id).AssignedQcName == qc.DisplayName, "Manager pending cards identify assigned QC");
Check(!Value<IEnumerable<QcProjectSummaryResponse>>(await unrelatedReview.GetProjects(ct)).Any(), "QC work excludes unrelated manager's projects");
Check(Value<QcDashboardResponse>(await managerReview.GetDashboard(ct)).Approved == 1, "Manager QC dashboard shares scoped aggregates");
Check((await managerReview.GetProject(project.Id, ct)).Result is OkObjectResult, "Manager can read existing QC project details");
Check((await managerReview.GetArea(project.Id, area.Id, ct)).Result is OkObjectResult, "Manager can read existing QC area details");
Check((await managerReview.GetEquipmentInspection(equipment.Id, ct)).Result is OkObjectResult, "Manager can read existing QC inspection");
Check(await managerReview.DownloadInspectionPhoto(inspectionId, photo.Id, ct) is FileStreamResult, "Manager uses existing QC photo endpoint");
Check(await adminReview.DownloadInspectionPhoto(inspectionId, photo.Id, ct) is FileStreamResult, "Admin can read QC photos");
foreach (var denied in new[] {unrelatedReview, otherQcReview})
{
    Check((await denied.GetProject(project.Id, ct)).Result is ObjectResult {StatusCode: 403}, "Out-of-scope QC project read denied");
    Check((await denied.GetArea(project.Id, area.Id, ct)).Result is ObjectResult {StatusCode: 403}, "Out-of-scope QC area read denied");
    Check((await denied.GetEquipmentInspection(equipment.Id, ct)).Result is ObjectResult {StatusCode: 403}, "Out-of-scope QC inspection read denied");
    Check(await denied.DownloadInspectionPhoto(inspectionId, photo.Id, ct) is ObjectResult {StatusCode: 403}, "Out-of-scope QC photo denied");
    Check((await denied.StartReview(inspectionId, ct)).Result is ObjectResult {StatusCode: 403}, "Out-of-scope review start denied");
    Check((await denied.Approve(inspectionId, new("Attempt"), ct)).Result is ObjectResult {StatusCode: 403}, "Out-of-scope approval denied");
    Check((await denied.RequestChanges(inspectionId, new("Attempt", QcSeverity.High), ct)).Result is ObjectResult {StatusCode: 403}, "Out-of-scope changes denied");
    Check((await denied.UpdateComments(inspectionId, new("Attempt"), ct)).Result is ObjectResult {StatusCode: 403}, "Out-of-scope comments denied");
}
var teamEdit = new UpsertProjectRequest(project.ProjectName, project.Sector, project.JobNo,
    project.Location, project.VesselOrSiteName, surveyor.Id, qc.Id, ClientName: "Test Client Company", TargetDate: DateOnly.FromDateTime(DateTime.Today),
    AdditionalQcId: qc.Id, UpdateAdditionalQcAssignment: true);
Check((await projectController.UpdateProject(project.Id, teamEdit, ct)).Result is BadRequestObjectResult,
    "Primary QC cannot also be the additional QC");
teamEdit = teamEdit with { AdditionalQcId = otherQc.Id };
Check((await projectController.UpdateProject(project.Id, teamEdit, ct)).Result is OkObjectResult,
    "Scoped Manager can assign additional QC");
Check((await projectController.UpdateProject(project.Id, teamEdit with { AdditionalQcId = null, UpdateAdditionalQcAssignment = false }, ct)).Result is OkObjectResult
    && project.AdditionalQcId == otherQc.Id, "Older project edits preserve the additional QC");
Check(db.AuditLogs.Count(a => a.EntityId == project.Id && a.Action == "QC Assignment") == 1,
    "Assignment change is recorded once");
await survey.CreateInspection(untouched.Id, payload, ct);
var sharedId = db.SurveySessions.Single(s => s.AreaEquipmentId == untouched.Id).Id;
Check(Value<IEnumerable<QcProjectSummaryResponse>>(await otherQcReview.GetProjects(ct, pendingOnly: true)).Single().ProjectId == project.Id,
    "Additional QC sees pending inspections for the assigned project");
Check((await otherQcReview.GetProject(project.Id, ct)).Result is OkObjectResult, "Additional QC can open the assigned project");
Check((await managerReview.StartReview(sharedId, ct)).Result is OkObjectResult, "Manager starts the same QC review");
Check((await managerReview.UpdateComments(sharedId, new("Manager initial comment"), ct)).Result is OkObjectResult, "Manager saves QC comments");
Check((await managerReview.RequestChanges(sharedId, new("Manager requests seal check", QcSeverity.Critical), ct)).Result is OkObjectResult, "Manager requests changes with severity");
await survey.UpdateInspection(sharedId, payload with {Comments = "Seal checked"}, ct);
Check(db.QcReviews.Single(q => q.SurveySessionId == sharedId).Status == QcReviewStatus.PendingReview, "Manager change request follows existing Surveyor resubmission");
Check((await otherQcReview.StartReview(sharedId, ct)).Result is OkObjectResult, "Additional QC continues the existing review after resubmission");
Check(db.QcReviews.Single(q => q.SurveySessionId == sharedId).ReviewedByUserId == otherQc.Id,
    "Existing review records the additional QC actor");
Check((await managerReview.Approve(sharedId, new("Manager final approval"), ct)).Result is OkObjectResult, "Manager approves the existing QC review");
var sharedReview = db.QcReviews.Single(q => q.SurveySessionId == sharedId);
Check(sharedReview.Status == QcReviewStatus.Approved && sharedReview.ReviewedByUserId == manager.Id, "Same review records actual Manager reviewer");
Check(db.SurveySessions.Single(s => s.Id == sharedId).Status == SurveyStatus.Approved, "Manager approval preserves Surveyor flow");
Check(db.AuditLogs.Any(a => a.EntityId == sharedId && a.Action == "QC Requested Changes" && a.ActorId == manager.Id && a.Comments == "Manager requests seal check"), "Manager actions retain existing QC history");
Check((await managerReview.RequestChanges(sharedId, new("Attempt reopen", QcSeverity.Low), ct)).Result is ConflictObjectResult, "Manager cannot bypass approved lock");
Check((await adminReview.StartReview(sharedId, ct)).Result is ConflictObjectResult, "Admin access preserves approved lock");
Check(Value<IEnumerable<QcProjectSummaryResponse>>(await adminReview.GetProjects(ct)).Count() == db.Projects.Count(), "Admin has all QC projects");
Check(typeof(QcController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles == "Qc,Manager,Admin", "QC endpoints permit QC, Manager and Admin only");
Check(await adminMonitor.SetManager(qc.Id, new(unrelated.Id), ct) is NoContentResult, "Admin can set reporting manager");
Check((await managerReview.GetProject(project.Id, ct)).Result is ObjectResult {StatusCode: 403}, "Changing reporting Manager immediately revokes QC work access");
Check((await unrelatedReview.GetProject(project.Id, ct)).Result is OkObjectResult, "New reporting Manager gains same QC work access");
Check(!Value<IEnumerable<ManagerQcProject>>(await monitor.Projects(ct)).Any(p => p.ProjectId == project.Id), "Reporting reassignment changes manager visibility");
Check(Value<IEnumerable<ManagerQcProject>>(await otherMonitor.Projects(ct)).Any(p => p.ProjectId == project.Id), "New reporting manager gains visibility");
Check(await adminMonitor.SetManager(qc.Id, new(surveyor.Id), ct) is BadRequestObjectResult, "Non-manager reporting assignment rejected");
Check(typeof(ManagerQcReviewController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles == "Admin,Manager", "Monitoring routes restrict roles");
Check(typeof(ManagerQcReviewController).GetMethod("SetManager")!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles == "Admin", "Reporting changes require Admin");
Check(db.QcReviews.Count() == db.SurveySessions.Count() && db.Projects.Single(p => p.Id == project.Id).AssignedQcId == qc.Id, "No duplicate review or changed QC assignment");
await using var sqlDb = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>()
    .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=QueryTranslationOnly;Trusted_Connection=True").Options);
var sqlController = As(new ManagerQcReviewController(sqlDb, storage), manager);
var queries = typeof(ManagerQcReviewController);
var scopeQuery = (IQueryable)queries.GetMethod("RelevantProjects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(sqlController, null)!;
Check(scopeQuery.ToQueryString().Contains("ManagerId"), "Manager scope translates to SQL Server");
var rowsQuery = (IQueryable)queries.GetMethod("ReviewRows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(sqlController, [new[] {project.Id}])!;
var sql = rowsQuery.ToQueryString();
Check(sql.Contains("LEFT JOIN") && sql.Contains("AuditLogs"), "Equipment, inspection and history query translates to SQL Server");
var writeProject = new UpsertProjectRequest("New project", "Marine", "JOB-3", "Dubai", "Site", surveyor.Id, qc.Id, client.Id, ClientName: "Test Client Company", TargetDate: ProjectScheduling.Today.AddDays(30));
var createdProject = await projectController.CreateProject(writeProject, ct);
Check(createdProject.Result is CreatedAtActionResult, "Project creation still succeeds with reporting managers");
var newProjectId = db.Projects.Single(p => p.JobNo == "JOB-3").Id;
Check((await projectController.UpdateProject(newProjectId, writeProject with {ProjectName = "Edited project"}, ct)).Result is OkObjectResult, "Project edit still succeeds");
Check(db.Projects.Single(p => p.Id == newProjectId).AssignedQcId == qc.Id, "Project create/edit stores AssignedQcId");
var projectDetail = Value<ManagerQcProjectDetails>(await otherMonitor.Project(project.Id, ct));
Check(projectDetail.Project.TotalEquipment == 2 && projectDetail.Project.Counts.Approved == 2, "Project aggregates distinguish total equipment and reviews");
Check(projectDetail.Areas.Single().TotalEquipment == 2 && projectDetail.Areas.Single().Counts.Approved == 2, "Area aggregates are returned by backend");
Check((await managerReview.GetProject(newProjectId, ct)).Result is ObjectResult {StatusCode: 403}, "Creating a project does not grant QC work outside reporting scope");
var qcScopeQuery = (IQueryable)typeof(QcController).GetMethod("AccessibleProjects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(As(new QcController(sqlDb, storage), manager), null)!;
Check(qcScopeQuery.ToQueryString().Contains("ManagerId"), "Shared QC Manager scope translates to SQL Server");
var openProjectIds = (IQueryable<Guid>)typeof(QcController).GetMethod("OpenInspectionProjectIds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(As(new QcController(sqlDb, storage), manager), null)!;
var pendingSql = ((IQueryable<Project>)qcScopeQuery).Where(p => openProjectIds.Contains(p.Id)).ToQueryString();
Check(pendingSql.Contains("ManagerId") && pendingSql.Contains("SubmittedAt") && pendingSql.Contains("QcReviews"), "Scoped pending query translates to SQL Server");
Console.WriteLine("All QC workflow checks passed.");
await HazardousAreaPlanChecks.Run();
await ProjectUploadHttpChecks.Run();
var withoutClient = writeProject with {JobNo = "NO-CLIENT", AssignedClientId = null};
var withoutClientCreated = await projectController.CreateProject(withoutClient, ct);
var withoutClientId = ((ProjectResponse)((CreatedAtActionResult)withoutClientCreated.Result!).Value!).Id;
Check(db.Projects.Single(p => p.Id == withoutClientId).AssignedClientId is null, "SQL model permits projects without Client");
Check(Value<SurveyorProjectDetailsResponse>(await survey.GetProject(withoutClientId, ct)).AssignedClient == "Not assigned", "Surveyor project details support missing Client");
Check(Value<QcProjectDetailsResponse>(await reviewController.GetProject(withoutClientId, ct)).AssignedClient == "Not assigned", "QC project details support missing Client");
await adminMonitor.SetManager(qc.Id, new(manager.Id), ct);
var defectsController = As(new DefectsController(db, storage), manager);
Check(Value<DefectsResponse>(await defectsController.GetDefects(ct)).Summary.OpenCount == 0, "QC reviews and comments do not create duplicate defects");
var defects = new[] {
    new Finding {SurveySessionId = inspectionId, Observation = "Damaged cable", Severity = " high "},
    new Finding {SurveySessionId = inspectionId, Observation = "Loose connection", Severity = "Medium", Status = FindingStatus.RectificationSubmitted},
    new Finding {SurveySessionId = inspectionId, Observation = "Missing label", Severity = "Low"},
    new Finding {SurveySessionId = inspectionId, Observation = "Unclassified finding"},
    new Finding {SurveySessionId = inspectionId, Observation = "Verified finding", Severity = "High", Status = FindingStatus.Verified},
    new Finding {SurveySessionId = inspectionId, Observation = "Closed finding", Severity = "Low", Status = FindingStatus.Closed}
};
db.Findings.AddRange(defects);
var unrelatedProject = new Project {ProjectName = "Unrelated defects", JobNo = "DEFECT-OTHER", Sector = "Marine", Location = "Dubai", VesselOrSiteName = "Site", CreatedByUserId = unrelated.Id, AssignedQcId = otherQc.Id, AssignedSurveyorId = surveyor.Id};
var unrelatedArea = new ProjectArea {ProjectId = unrelatedProject.Id, AreaName = "Other Deck"};
var unrelatedEquipment = new AreaEquipment {ProjectId = unrelatedProject.Id, AreaId = unrelatedArea.Id, SequenceNo = 1, TagNumber = "OTHER-TAG", Type = "Motor", CreatedByUserId = surveyor.Id};
var unrelatedInspection = new SurveySession {AreaEquipmentId = unrelatedEquipment.Id, SurveyorId = surveyor.Id};
var unrelatedFinding = new Finding {SurveySessionId = unrelatedInspection.Id, Observation = "Unrelated damaged cable", Severity = "High"};
db.Projects.Add(unrelatedProject); db.ProjectAreas.Add(unrelatedArea); db.AreaEquipment.Add(unrelatedEquipment); db.SurveySessions.Add(unrelatedInspection); db.Findings.Add(unrelatedFinding);
var defectPhoto = new SurveyFile {FindingId = defects[0].Id, SurveySessionId = inspectionId, FileName = "defect.jpg", StorageKey = "defect.jpg", ContentType = "image/jpeg", Category = FileCategory.FindingPhoto, UploadedById = surveyor.Id};
db.Files.Add(defectPhoto); await db.SaveChangesAsync(ct);
var defectResponse = Value<DefectsResponse>(await defectsController.GetDefects(ct));
Check(defectResponse.Summary == new DefectSummary(4, 1, 1, 1, 1) && defectResponse.Items.Count == 4, "Backend counts unresolved findings once and aggregates their severity");
Check(defectResponse.Items.All(x => x.ProjectId == project.Id) && defectResponse.Items.Single(x => x.Id == defects[0].Id).Photos.Count == 1, "Defect list scopes projects and exposes linked photo metadata");
Check(Value<DefectsResponse>(await defectsController.GetDefects(ct, false)).Items.Count == 6, "All defects retains verified and closed history");
Check(Value<DefectsResponse>(await As(new DefectsController(db, storage), admin).GetDefects(ct)).Summary.OpenCount == 5, "Admin can monitor all project defects");
Check(await defectsController.Photo(defects[0].Id, defectPhoto.Id, ct) is FileStreamResult, "Relevant Manager can view defect photo");
Check(await As(new DefectsController(db, storage), unrelated).Photo(defects[0].Id, defectPhoto.Id, ct) is NotFoundResult, "Unrelated Manager cannot view defect photo");
Check(await defectsController.Photo(defects[1].Id, defectPhoto.Id, ct) is NotFoundResult, "Photo must belong to the requested finding");
defects[0].Status = FindingStatus.Verified; await db.SaveChangesAsync(ct);
Check(Value<DefectsResponse>(await defectsController.GetDefects(ct)).Summary.OpenCount == 3, "Verifying a finding decreases open defect count");
var defectQuery = (IQueryable<DefectRow>)typeof(DefectsController).GetMethod("Rows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(As(new DefectsController(sqlDb, storage), manager), null)!;
Check(defectQuery.OrderByDescending(x => x.InspectionDate).ThenBy(x => x.Id).ToQueryString().Contains("ManagerId"), "Defect query and ordering translate to SQL Server");
Check(defectQuery.Where(x => x.Id == defects[0].Id).ToQueryString().Contains("Findings"), "Defect photo authorization translates to SQL Server");
var surveyorRegister = As(new DefectsController(db, storage), surveyor);
var adminRegister = As(new DefectsController(db, storage), admin);
var findingRequest = new RegisterFindingRequest(equipment.Id, new DateOnly(2026, 11, 4), InspectionType.Visual,
    "AOSD-1", "B", "F-01", "New equipment defect", "Inspect cable", "First remark", "Second remark");
var manualId = Guid.NewGuid();
var adminId = Guid.NewGuid();
Check(await surveyorRegister.SaveRegisterRow(project.Id, manualId, findingRequest, ct) is NoContentResult &&
    await adminRegister.SaveRegisterRow(project.Id, adminId, findingRequest, ct) is NoContentResult,
    "Surveyor and Admin can create separate defects against the same HAER equipment");
Check(await surveyorRegister.SaveRegisterRow(project.Id, manualId, findingRequest, ct) is NoContentResult &&
    await db.Findings.CountAsync(x => x.Id == manualId, ct) == 1, "Retrying a defect row preserves its identity");
Check(await surveyorRegister.SaveRegisterRow(project.Id, Guid.NewGuid(), findingRequest with {EquipmentId = unrelatedEquipment.Id}, ct) is BadRequestObjectResult &&
    await As(new DefectsController(db, storage), unrelated).SaveRegisterRow(project.Id, manualId, findingRequest, ct) is NotFoundResult,
    "Register blocks cross-project equipment and unauthorized writes");
Check(await surveyorRegister.SaveRegisterRow(project.Id, adminId, findingRequest, ct) is NotFoundResult &&
    await adminRegister.SaveRegisterRow(project.Id, manualId, findingRequest with {AdditionalRemarks = "Edited second remark"}, ct) is NoContentResult,
    "Surveyors can edit their own open records and Admin can edit scoped records");
var surveyorRows = Value<DefectsResponse>(await surveyorRegister.GetDefects(ct, false, project.Id)).Items;
var adminRows = Value<DefectsResponse>(await adminRegister.GetDefects(ct, false, project.Id)).Items;
Check(surveyorRows.Any(x => x.Id == adminId && !x.CanEdit) &&
    adminRows.Any(x => x.Id == manualId && x.CanEdit && x.EquipmentId == equipment.Id && x.Status == FindingStatus.Open &&
        x.Remarks == "First remark" && x.AdditionalRemarks == "Edited second remark"),
    "Both roles see shared records, independent remarks and unchanged status");
var registerQuery = (IQueryable<DefectRow>)typeof(DefectsController).GetMethod("RegisterRows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
    .Invoke(As(new DefectsController(sqlDb, storage), surveyor), null)!;
Check(registerQuery.Where(x => x.ProjectId == project.Id).ToQueryString().Contains("AssignedSurveyorId"),
    "Register project scope translates to SQL Server");
var photoUpload = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "photo", "evidence.jpg");
Check(await surveyorRegister.SetRegisterPhoto(manualId, 1, photoUpload, ct) is NoContentResult &&
    await surveyorRegister.SetRegisterPhoto(manualId, 2, photoUpload, ct) is NoContentResult,
    "Defect photo upload stores both independent slots");
var manualPhotos = Value<DefectsResponse>(await adminRegister.GetDefects(ct, false, project.Id)).Items.Single(x => x.Id == manualId).Photos;
Check(manualPhotos.Select(x => x.Slot).ToHashSet().SetEquals(["DefectPhoto1", "DefectPhoto2"]) &&
    await adminRegister.Photo(manualId, manualPhotos[0].Id, ct) is FileStreamResult,
    "Scoped reads include both defect photo slots and authenticated preview");
Check(await surveyorRegister.SetRegisterPhoto(manualId, 1, photoUpload, ct) is NoContentResult &&
    await surveyorRegister.RemoveRegisterPhoto(manualId, 2, ct) is NoContentResult &&
    Value<DefectsResponse>(await adminRegister.GetDefects(ct, false, project.Id)).Items.Single(x => x.Id == manualId).Photos
        .Single() is { Slot: "DefectPhoto1" } replacement && replacement.Id != manualPhotos.Single(x => x.Slot == "DefectPhoto1").Id,
    "Replacing and removing photos affect only the selected slot");
var deleteController = As(new ProjectController(db, storage, NullLogger<ProjectController>.Instance), manager);
var otherProjectCount = db.Projects.Count();
var originalUserCount = db.Users.Count();
Check(await deleteController.DeleteProject(project.Id, ct) is NoContentResult, "Manager deletes project with existing equipment and inspections");
Check(!db.ProjectAreas.Any(x => x.ProjectId == project.Id) && !db.AreaEquipment.Any(x => x.ProjectId == project.Id), "Project areas and equipment are removed together");
Check(!db.SurveySessions.Any(x => x.AreaEquipmentId == equipment.Id || x.AreaEquipmentId == untouched.Id), "Project inspections are removed");
Check(!db.QcReviews.Any(x => x.SurveySessionId == inspectionId || x.SurveySessionId == sharedId), "Linked QC reviews are removed");
Check(db.Projects.Count() == otherProjectCount - 1 && db.Users.Count() == originalUserCount, "Other projects and assigned users remain intact");
Check(db.AuditLogs.Any(x => x.EntityId == project.Id && x.Action == "Project Deleted" && x.ActorId == manager.Id), "Project deletion retains an audit event");

static User MakeUser(string name, UserRole role) => new() {Email = name.Replace(" ", "") + "@test.invalid", DisplayName = name, PasswordHash = "test", Role = role, ApprovalStatus = ApprovalStatus.Approved};
static T As<T>(T controller, User user) where T : ControllerBase {controller.ControllerContext = new ControllerContext {HttpContext = new DefaultHttpContext {User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Role, user.Role.ToString())], "Test"))}}; return controller;}
static T Value<T>(ActionResult<T> result) => (T)((OkObjectResult)result.Result!).Value!;
static void Check(bool passed, string message) {if (!passed) throw new Exception(message); Console.WriteLine("PASS: " + message);}
sealed class FakePhotos : IProjectPhotoStorage
{
    public Task<StoredProjectPhoto> SaveHazardousAreaPlanAsync(IFormFile file, Guid projectId, CancellationToken ct) => throw new NotSupportedException();
    public int MaxFilesPerRequest => 20;
    public Task<StoredProjectPhoto> SaveAsync(IFormFile file, Guid projectId, Guid areaId, CancellationToken ct) => throw new NotSupportedException();
    public Task<StoredProjectPhoto> SaveInspectionAsync(IFormFile file, Guid projectId, Guid inspectionId, CancellationToken ct) => Task.FromResult(new StoredProjectPhoto(file.FileName, $"test/{Guid.NewGuid():N}.jpg"));
    public Task<StoredProjectPhotoContent?> OpenReadAsync(string path, CancellationToken ct = default) => Task.FromResult<StoredProjectPhotoContent?>(new(new MemoryStream([1, 2, 3]), "image/png"));
    public Task DeleteAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
}
