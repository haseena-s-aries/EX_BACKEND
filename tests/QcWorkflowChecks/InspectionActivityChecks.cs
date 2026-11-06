using System.Security.Claims;
using Ex_SurveyApp_api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Contracts.V1;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;

internal static class InspectionActivityChecks
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException($"Inspection activity check failed: {message}");
    }

    public static async Task Run()
    {
        var options = new DbContextOptionsBuilder<SurveyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new SurveyDbContext(options);
        var inspector = new User { Email = "activity-surveyor@example.invalid", DisplayName = "Field Inspector", PasswordHash = "test", Role = UserRole.Surveyor, ApprovalStatus = ApprovalStatus.Approved };
        var reviewer = new User { Email = "activity-qc@example.invalid", DisplayName = "QC Reviewer", PasswordHash = "test", Role = UserRole.Qc, ApprovalStatus = ApprovalStatus.Approved };
        var client = new User { Email = "activity-client@example.invalid", DisplayName = "Project Client", PasswordHash = "test", Role = UserRole.Client, ApprovalStatus = ApprovalStatus.Approved };
        db.Users.AddRange(inspector, reviewer, client);
        var template = new ChecklistTemplate { Name = "Fixture", InspectionType = InspectionType.Visual };
        db.ChecklistTemplates.Add(template);
        var version = new ChecklistVersionSnapshot { SourceChecklistTemplateId = template.Id, VersionNumber = 1, InspectionType = InspectionType.Visual, SnapshotHash = "fixture-v1", PublishedAt = DateTimeOffset.UtcNow, PublishedByUserId = reviewer.Id };
        db.ChecklistVersionSnapshots.Add(version);
        var project = new Project { ProjectName = "Activity scope fixture", JobNo = "P2D-SCOPE", Sector = "Test", Location = "Test", VesselOrSiteName = "Test", CreatedByUserId = inspector.Id, AssignedSurveyorId = inspector.Id, AssignedQcId = reviewer.Id, AssignedClientId = client.Id };
        var tenant = new Tenant { TenantKey = "p2d-fixture", DisplayName = "Fixture Tenant" };
        var contract = new Contract { TenantId = tenant.Id, ContractKey = "P2D-C", Name = "Fixture Contract" };
        db.Projects.Add(project); db.Tenants.Add(tenant); db.TenantContracts.Add(contract);
        db.TenantProjectBindings.Add(new TenantProjectBinding { TenantId = tenant.Id, ContractId = contract.Id, ProjectId = project.Id });
        var plan = new InspectionPlan { ProjectId = project.Id, Name = "Cycle 1" }; db.InspectionPlans.Add(plan);
        var equipment = new AreaEquipment { ProjectId = project.Id, TagNumber = "P2D-001", Type = "Test", CreatedByUserId = inspector.Id };
        db.AreaEquipment.Add(equipment);
        var planningCampaign = new InspectionCampaign {ProjectId=project.Id,Name="Synthetic campaign",Reference="ACT-CAMPAIGN",StartsOn=new DateOnly(2026,1,1),EndsOn=new DateOnly(2026,12,31),CreatedByUserId=inspector.Id};
        var workPackage = new InspectionWorkPackage {CampaignId=planningCampaign.Id,ProjectId=project.Id,Reference="WP-01",CreatedByUserId=inspector.Id};db.InspectionCampaigns.Add(planningCampaign);db.InspectionWorkPackages.Add(workPackage);
        var task = new InspectionTask { InspectionPlanId = plan.Id, AreaEquipmentId = equipment.Id, AssignedUserId = inspector.Id, WorkPackageReference = "WP-01", WorkPackageId=workPackage.Id, PlannedGrade=InspectionType.Visual, StatusCode="InProgress" };
        var closeTask = new InspectionTask { InspectionPlanId = plan.Id, AreaEquipmentId = equipment.Id, AssignedUserId = inspector.Id, Kind = InspectionTaskKind.Regular, WorkPackageReference = "WP-02" };
        db.InspectionTasks.AddRange(task, closeTask);
        await db.SaveChangesAsync();

        var when = DateTimeOffset.UtcNow;
        var visual = InspectionActivityRules.Create(new(task.Id, InspectionType.Visual, inspector.Id, when, when.AddMinutes(1), version.Id, inspector.DisplayName));
        var close = InspectionActivityRules.Create(new(closeTask.Id, InspectionType.Close, inspector.Id, when.AddDays(1), when.AddDays(1).AddMinutes(1), version.Id, inspector.DisplayName));
        Check(visual.Id != close.Id && visual.Id != Guid.Empty && close.Id != Guid.Empty, "activity IDs are independently generated and stable");
        db.InspectionActivities.AddRange(visual, close);
        var activityRevision = new InspectionActivityRevision { InspectionActivityId = visual.Id, RevisionNumber = 1, ContentHash = "content-v1", CreatedByUserId = inspector.Id };
        db.InspectionActivityRevisions.Add(activityRevision);
        var definition = new CheckpointDefinitionSnapshot { ChecklistVersionSnapshotId = version.Id, QuestionCode = "Q-1", QuestionVersion = 1, QuestionText = "Check fixture only", SortOrder = 1 };
        db.CheckpointDefinitionSnapshots.Add(definition);
        var response = new CheckpointResponseRevision { ActivityRevisionId = activityRevision.Id, CheckpointDefinitionSnapshotId = definition.Id, RevisionNumber = 1, ResponseCode = "APPROVED-FIXTURE-CODE", TechnicalConditionCode = "FIXTURE-CONDITION", WorkflowState = CheckpointWorkflowState.Open, RecordedByUserId = inspector.Id };
        var responseRevision = new CheckpointResponseRevision { ActivityRevisionId = activityRevision.Id, CheckpointDefinitionSnapshotId = definition.Id, RevisionNumber = 2, ResponseCode = "APPROVED-FIXTURE-CODE-REV2", TechnicalConditionCode = "FIXTURE-CONDITION-REV2", WorkflowState = CheckpointWorkflowState.Reviewed, Observation = "Updated checkpoint entry", RecordedByUserId = inspector.Id, ReviewedByUserId = reviewer.Id, ReviewedAt = when.AddMinutes(2), ReviewComment = "Reviewed fixture" };
        db.CheckpointResponseRevisions.AddRange(response, responseRevision);
        var findingA = new ActivityFinding { InspectionActivityId = visual.Id, CheckpointResponseRevisionId = response.Id, Observation = "Fixture finding A" };
        var findingB = new ActivityFinding { InspectionActivityId = visual.Id, CheckpointResponseRevisionId = response.Id, Observation = "Fixture finding B" };
        db.ActivityFindings.AddRange(findingA, findingB);
        var action = new CorrectiveAction { ActivityFindingId = findingA.Id, ActionReference = "ACT-FIX-1", ProposedAction = "Fixture correction" };
        db.CorrectiveActions.Add(action);
        var firstEvent = new CorrectiveActionEvent { CorrectiveActionId = action.Id, Kind = CorrectiveActionEventKind.Created, ResultingStatus = CorrectiveActionStatus.Proposed, ActorUserId = inspector.Id };
        var proofEvent = new CorrectiveActionEvent { CorrectiveActionId = action.Id, Kind = CorrectiveActionEventKind.EvidenceAdded, ResultingStatus = CorrectiveActionStatus.InProgress, ActorUserId = inspector.Id, Comment = "After photo added" };
        db.CorrectiveActionEvents.AddRange(firstEvent, proofEvent);
        db.ActivityEvidence.Add(new ActivityEvidence { SurveyFileId = Guid.NewGuid(), CorrectiveActionEventId = proofEvent.Id, Role = ActivityEvidenceRole.After, AddedByUserId = inspector.Id });
        await db.SaveChangesAsync();

        var failedCheckpointAccepted = false; try { InspectionActivityRules.EnsureFindingUsesFailedCheckpoint(response); } catch (ArgumentException) { failedCheckpointAccepted = true; }
        Check(failedCheckpointAccepted, "defect creation rejects a checkpoint that is not marked Fail");
        var failedResponse = new CheckpointResponseRevision { ResponseCode = "Fail" };
        InspectionActivityRules.EnsureFindingUsesFailedCheckpoint(failedResponse);
        Check(true, "defect creation accepts an explicitly failed checkpoint without assigning automatic severity");

        Check(await db.InspectionActivities.CountAsync(x => x.InspectionTaskId == task.Id || x.InspectionTaskId == closeTask.Id) == 2, "Visual and Close activities coexist for the same equipment");
        Check(db.ActivityFindings.Count(x => x.InspectionActivityId == visual.Id) == 2, "multiple findings can link to one activity/checkpoint");
        Check(db.CheckpointResponseRevisions.Count(x => x.ActivityRevisionId == activityRevision.Id && x.CheckpointDefinitionSnapshotId == definition.Id) == 2 && response.RevisionNumber != responseRevision.RevisionNumber, "checkpoint corrections append a new response revision with reviewer metadata");
        Check(db.CorrectiveActionEvents.Count(x => x.CorrectiveActionId == action.Id) == 2 && db.ActivityEvidence.Any(x => x.Role == ActivityEvidenceRole.After), "corrective-action event/evidence history is retained");

        var mutableActor = new MutableActor(inspector.Id);
        var ownershipResolver = new InspectionTenantOwnershipResolver(db);
        var authorizer = new InspectionActivityAuthorizer(db, ownershipResolver, new TenantScopeResolver(db), new EmptyTenantContext(), mutableActor, Options.Create(new TenantFoundationOptions { Enabled = false }));
        var workflow = new InspectionActivityApplicationService(db, authorizer, mutableActor, ownershipResolver, new CorrectiveActionPolicy(), Options.Create(new InspectionActivityOptions { Enabled = true }), TimeProvider.System);
        var invalidFindingRejected = false; try { await workflow.AddFindingAsync(visual.Id, new(response.Id, "Must be failed", null, null)); } catch (ArgumentException) { invalidFindingRejected = true; }
        Check(invalidFindingRejected, "application service rejects a finding attached to a non-failed checkpoint");
        var failedCheckpoint = new CheckpointResponseRevision { ActivityRevisionId = activityRevision.Id, CheckpointDefinitionSnapshotId = definition.Id, RevisionNumber = 3, ResponseCode = "Fail", Observation = "Seal defect", RecordedByUserId = inspector.Id };
        db.CheckpointResponseRevisions.Add(failedCheckpoint); await db.SaveChangesAsync();
        var serviceFinding = await workflow.AddFindingAsync(visual.Id, new(failedCheckpoint.Id, "Seal damaged", "Replace seal", "Proposed", "FC-01", "Seal kit", null, DateTimeOffset.UtcNow.AddDays(-2), "Maintenance team"));
        Check(await db.ActivityFindings.AnyAsync(x => x.Id == serviceFinding.Id && x.ReportedByUserId == inspector.Id && x.ResponsibleParty == "Maintenance team"), "surveyor can report a defect with fault code, severity proposal, responsible party and reporting actor");
        var serviceAction = await workflow.CreateCorrectiveActionAsync(new(serviceFinding.Id, "ACT-SVC-1", "Access note", "Replace seal", "WO-SVC-1", null, "Maintenance team"));
        var afterOne = new SurveyFile { Category = FileCategory.FindingPhoto, FileName = "after-1.jpg", StorageKey = "test/after-1.jpg", ContentType = "image/jpeg", UploadedById = inspector.Id }; db.Files.Add(afterOne); await db.SaveChangesAsync();
        await workflow.AddEvidenceAsync(visual.Id, new(afterOne.Id, null, serviceAction.Id, null, ActivityEvidenceRole.After, "First correction evidence", null));
        await workflow.SubmitCorrectiveActionForVerificationAsync(serviceAction.Id);
        mutableActor.UserId = reviewer.Id;
        var rejectedCorrection = await workflow.VerifyCorrectiveActionAsync(serviceAction.Id, new("Evidence does not show full seal seating", false));
        Check(rejectedCorrection.Status == CorrectiveActionStatus.Reopened, "QC can reject rectification only with a recorded reason");
        mutableActor.UserId = inspector.Id;
        await workflow.ReportRectificationAsync(serviceAction.Id, new("Seal replaced and seating checked", "WO-SVC-2", DateTimeOffset.UtcNow, null, "Retest passed"));
        var recordedRectification = await db.CorrectiveActions.SingleAsync(x => x.Id == serviceAction.Id);
        Check(recordedRectification.ProposedAction == "Replace seal" && recordedRectification.RectificationDescription == "Seal replaced and seating checked", "reported repair preserves the original recommended action in a separate field");
        Check(await db.CorrectiveActionEvents.AnyAsync(x => x.CorrectiveActionId == serviceAction.Id && x.RectificationDescription == "Seal replaced and seating checked" && x.WorkOrderReference == "WO-SVC-2" && x.RectificationAt != null), "each rectification attempt retains its date, work-order and description in immutable event history");
        var afterTwo = new SurveyFile { Category = FileCategory.FindingPhoto, FileName = "after-2.jpg", StorageKey = "test/after-2.jpg", ContentType = "image/jpeg", UploadedById = inspector.Id }; db.Files.Add(afterTwo); await db.SaveChangesAsync();
        await workflow.AddEvidenceAsync(visual.Id, new(afterTwo.Id, null, serviceAction.Id, null, ActivityEvidenceRole.After, "Retest evidence", null));
        await workflow.SubmitCorrectiveActionForVerificationAsync(serviceAction.Id);
        mutableActor.UserId = reviewer.Id;
        var verifiedCorrection = await workflow.VerifyCorrectiveActionAsync(serviceAction.Id, new("Retest passed", true));
        Check(verifiedCorrection.Status == CorrectiveActionStatus.Verified && await db.CorrectiveActionEvents.CountAsync(x => x.CorrectiveActionId == serviceAction.Id) >= 6, "rectification resubmission and Aries verification append complete action history");
        var reviewerCannotClose = false; try { await workflow.DecideClientClosureAsync(serviceAction.Id, new(true, "QC close attempt")); } catch (UnauthorizedAccessException) { reviewerCannotClose = true; }
        Check(reviewerCannotClose, "QC verification cannot formally close the defect");
        mutableActor.UserId = client.Id;
        var clientClosed = await workflow.DecideClientClosureAsync(serviceAction.Id, new(true, "Accepted by project client"));
        Check(clientClosed.Status == CorrectiveActionStatus.ClientClosed, "assigned client can formally close a verified defect");
        var clientReopened = await workflow.DecideClientClosureAsync(serviceAction.Id, new(false, "Please reopen; post-repair evidence incomplete"));
        Check(clientReopened.Status == CorrectiveActionStatus.Reopened, "assigned client can reopen a closed defect with a reason");

        var policy = new CorrectiveActionPolicy();
        action.Status = CorrectiveActionStatus.Verified;
        mutableActor.UserId=inspector.Id;await workflow.SubmitForReviewAsync(visual.Id);
        mutableActor.UserId=reviewer.Id;await workflow.RecordReviewAsync(visual.Id,new ActivityReviewDecisionRequest(InspectionActivityReviewStatus.Accepted,"Accepted fixture"));
        Check(action.Status == CorrectiveActionStatus.Verified && db.ActivityFindings.All(x => x.StatusCode == "Open"), "activity acceptance does not close or mutate its findings/actions");
        Check(task.StatusCode=="Completed","QC acceptance completes only its planned work-package task");
        Check(visual.ReviewStatus == InspectionActivityReviewStatus.Accepted && db.ActivityReviewEvents.Count(x=>x.InspectionActivityId==visual.Id) == 2, "submission and review decision timestamps are retained");
        var immutableBlocked = false;
        try { InspectionActivityRules.EnsureRevisionAllowed(visual.ReviewStatus); } catch (InvalidOperationException) { immutableBlocked = true; }
        Check(immutableBlocked, "accepted activity cannot be edited as a new content revision");
        Check(ExpectUnauthorized(() => policy.EnsureClientMayClose(UserRole.Qc, true, action.Status)), "QC cannot formally close a defect");
        Check(ExpectUnauthorized(() => policy.EnsureClientMayClose(UserRole.Client, false, action.Status)), "unassigned client cannot formally close a defect");
        var reopenCannotSkipVerification = false; try { policy.EnsureClientMayClose(UserRole.Client, true, CorrectiveActionStatus.Reopened); } catch (InvalidOperationException) { reopenCannotSkipVerification = true; }
        Check(reopenCannotSkipVerification, "client cannot re-close a reopened defect before fresh Aries verification");
        policy.EnsureClientMayClose(UserRole.Client, true, action.Status);
        action.Status = CorrectiveActionStatus.ClientClosed; action.ClientDecisionUserId = client.Id; action.ClientDecisionAt = DateTimeOffset.UtcNow;
        db.CorrectiveActionEvents.Add(new CorrectiveActionEvent { CorrectiveActionId = action.Id, Kind = CorrectiveActionEventKind.ClientAccepted, ResultingStatus = action.Status, ActorUserId = client.Id });
        await db.SaveChangesAsync();
        Check(action.Status == CorrectiveActionStatus.ClientClosed, "assigned client can close only after Aries verification");

        var model = db.GetService<IDesignTimeModel>().Model;
        Check(model.FindEntityType(typeof(InspectionActivity))!.FindPrimaryKey()!.Properties.Single().Name == nameof(InspectionActivity.Id), "activity has immutable GUID primary key");
        Check(model.FindEntityType(typeof(CheckpointResponseRevision))!.GetIndexes().Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(CheckpointResponseRevision.ActivityRevisionId), nameof(CheckpointResponseRevision.CheckpointDefinitionSnapshotId), nameof(CheckpointResponseRevision.RevisionNumber) })), "checkpoint revisions have unique activity/question/revision key");
        Check(model.FindEntityType(typeof(InspectionTask))!.GetCheckConstraints().Any(c => c.Name == "CK_InspectionTasks_OneEquipment"), "each task targets exactly one modern or legacy equipment row");
        Check(db.Model.FindEntityType(typeof(SurveySession))!.GetIndexes().Any(i => i.IsUnique && i.Properties.Any(p => p.Name == "AreaEquipmentId")), "legacy one-session index remains unchanged");
        Check(model.FindEntityType(typeof(InspectionActivity))!.FindProperty(nameof(InspectionActivity.RowVersion))!.IsConcurrencyToken, "activity has an optimistic concurrency token");
        var ownership = await new InspectionTenantOwnershipResolver(db).ResolveTaskAsync(task.Id);
        Check(ownership.IsResolved && ownership.ProjectId == project.Id && ownership.TenantId == tenant.Id, "tenant scope follows stored equipment-to-project binding, without a request-supplied tenant ID");

        var invalidGradeRejected = false;
        try { InspectionActivityContractValidation.Validate(new CreateInspectionActivityRequest((InspectionType)99, version.Id, when, when)); } catch (ArgumentException) { invalidGradeRejected = true; }
        Check(invalidGradeRejected, "unknown grade is rejected by the versioned request contract");
        var mismatchRejected = false;
        try { InspectionActivityRules.ValidateGradeChecklist(InspectionType.Close, InspectionType.Visual); } catch (ArgumentException) { mismatchRejected = true; }
        Check(mismatchRejected, "grade and checklist version must match");
        var staleTokenRejected = false;
        try { InspectionActivityRules.EnsureExpectedRowVersion([1, 2, 3], Convert.ToBase64String([1, 2, 4])); } catch (InspectionRequestConflictException) { staleTokenRejected = true; }
        Check(staleTokenRejected, "stale concurrent update token is rejected");
        var requestTypes = typeof(CreateInspectionPlanRequest).Assembly.GetTypes().Where(t => t.Namespace == typeof(CreateInspectionPlanRequest).Namespace && t.Name.EndsWith("Request", StringComparison.Ordinal));
        Check(requestTypes.SelectMany(t => t.GetProperties()).All(p => p.Name is not "TenantId" and not "TenantKey"), "versioned client request DTOs expose no tenant ownership field");
        var inactiveAuthorizer = new InspectionActivityAuthorizer(db, new InspectionTenantOwnershipResolver(db), new TenantScopeResolver(db), new EmptyTenantContext(), new FixedActorContext(inspector.Id), Options.Create(new TenantFoundationOptions { Enabled = false }));
        var inactiveService = new InspectionActivityApplicationService(db, inactiveAuthorizer, new FixedActorContext(inspector.Id), new InspectionTenantOwnershipResolver(db), new CorrectiveActionPolicy(), Options.Create(new InspectionActivityOptions()), TimeProvider.System);
        var inactiveRejected = false;
        try { await inactiveService.CreatePlanAsync(new CreateInspectionPlanRequest(project.Id, "Inactive test", null, null, null)); } catch (InspectionFeatureInactiveException) { inactiveRejected = true; }
        Check(inactiveRejected, "application service refuses to write while the inspection feature flag is disabled");

        var categoryAdmin = new User {Email = "category-admin@example.invalid", DisplayName = "Category Admin",
            PasswordHash = "test", Role = UserRole.Admin, ApprovalStatus = ApprovalStatus.Approved};
        db.Users.Add(categoryAdmin);
        mutableActor.UserId = categoryAdmin.Id;
        equipment.RegistrationStatus = "Submitted";
        await db.SaveChangesAsync();
        var categoryController = new InspectionActivitiesController(db, workflow, Options.Create(new InspectionActivityOptions {Enabled = true}), null!)
        {
            ControllerContext = new ControllerContext {HttpContext = new DefaultHttpContext {User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, categoryAdmin.Id.ToString()),
                    new Claim(ClaimTypes.Role, "Admin")], "test"))}}
        };
        var date = new CategoryChecklistOpen(new DateOnly(2026, 11, 5));
        var preview = (OkObjectResult)await categoryController.CategoryChecklist(project.Id, equipment.Id, "All Checkpoints", null, authorizer, default);
        var previewPoints = System.Text.Json.JsonSerializer.SerializeToElement(preview.Value);
        var counts = previewPoints.EnumerateArray().GroupBy(p => p.GetProperty("Section").GetString())
            .ToDictionary(g => g.Key!, g => g.Count());
        Check(previewPoints.GetArrayLength() == 59 && counts["Apparatus"] == 23 && counts["Installation"] == 33 &&
            counts["Environment"] == 3, "full PDF list is available before an inspection can be opened");
        Check(await categoryController.OpenCategoryChecklist(project.Id, equipment.Id, date, authorizer, default) is ConflictObjectResult,
            "category inspection requires approved sample versions");
        var categorySource = SampleChecklistImporter.ReadSource();
        foreach (var grade in Enum.GetValues<InspectionType>())
            db.ChecklistTemplates.Add(new ChecklistTemplate {Id = SampleChecklistImporter.StableId($"{categorySource.SourceRevision}/{grade}"),
                Name = $"Approved sample {grade}", InspectionType = grade, LifecycleStatus = ChecklistLifecycleStatus.Published,
                IsActive = true, ApprovedByUserId = reviewer.Id, ApprovedAt = when});
        await db.SaveChangesAsync();
        Check(await categoryController.OpenCategoryChecklist(Guid.NewGuid(), equipment.Id, date, authorizer, default) is NotFoundResult,
            "category inspection rejects equipment outside the selected project");
        var opened = (OkObjectResult)await categoryController.OpenCategoryChecklist(project.Id, equipment.Id, date, authorizer, default);
        var categoryActivity = await db.InspectionActivities.SingleAsync(a => a.Grade == null);
        var repeated = (OkObjectResult)await categoryController.OpenCategoryChecklist(project.Id, equipment.Id, date, authorizer, default);
        Check(Equals(opened.Value, repeated.Value) && categoryActivity.Grade is null, "category draft starts with unverified equipment and resumes without a grade");
        var sourcePoints = await db.CheckpointDefinitionSnapshots.Where(p => p.ChecklistVersionSnapshotId == categoryActivity.ChecklistVersionId).ToListAsync();
        Check(sourcePoints.Count == 59 && sourcePoints.Select(p => p.QuestionCode).Distinct().Count() == 59,
            "snapshot preserves all original PDF references without grade duplicates");
        var categoryResult = (OkObjectResult)await categoryController.CategoryChecklist(project.Id, equipment.Id, "Ex 'n' only", categoryActivity.Id, authorizer, default);
        var categoryPoints = System.Text.Json.JsonSerializer.SerializeToElement(categoryResult.Value);
        Check(categoryPoints.GetArrayLength() == 2 &&
            categoryPoints.EnumerateArray().Select(p => p.GetProperty("ItemNumber").GetInt32()).SequenceEqual([19,20]),
            "category lookup includes all Ex n checkpoints and retains original numbering");
        categoryActivity.RowVersion = [1];
        await db.SaveChangesAsync();
        var categoryDefinitions = sourcePoints.Where(p => p.ApplicabilityCode == "Ex 'n' only").OrderBy(p => p.SortOrder).ToArray();
        var firstCategoryAnswer = await workflow.SaveCheckpointResponseAsync(categoryActivity.Id,
            new(categoryDefinitions[0].Id, null, "Fail", null, "Seal damage", Convert.ToBase64String(categoryActivity.RowVersion)));
        var findingRequest = new AddActivityFindingRequest(firstCategoryAnswer.Id, "Seal damage", null, null);
        var firstDefect = await workflow.AddFindingAsync(categoryActivity.Id, findingRequest);
        Check((await workflow.AddFindingAsync(categoryActivity.Id, findingRequest)).Id == firstDefect.Id,
            "raising a defect again preserves the original defect");
        var incomplete = false;
        try { await workflow.SubmitForReviewAsync(categoryActivity.Id); } catch (InvalidOperationException) { incomplete = true; }
        Check(incomplete, "a partially answered category cannot be submitted");
        await workflow.SaveCheckpointResponseAsync(categoryActivity.Id,
            new(categoryDefinitions[1].Id, null, "Pass", null, null, Convert.ToBase64String(categoryActivity.RowVersion)));
        var cleared = await workflow.SaveCheckpointResponseAsync(categoryActivity.Id,
            new(categoryDefinitions[1].Id, null, null, null, "Result cleared", Convert.ToBase64String(categoryActivity.RowVersion)));
        var blankRejected = false;
        try { await workflow.SubmitForReviewAsync(categoryActivity.Id); } catch (InvalidOperationException) { blankRejected = true; }
        Check(cleared.ResponseCode is null && blankRejected, "clearing the latest result persists null and blocks submission even after a prior Pass");
        await workflow.SaveCheckpointResponseAsync(categoryActivity.Id,
            new(categoryDefinitions[1].Id, null, "Pass", null, null, Convert.ToBase64String(categoryActivity.RowVersion)));
        Check((await workflow.SubmitForReviewAsync(categoryActivity.Id)).ReviewStatus == InspectionActivityReviewStatus.Submitted,
            "completed grade-free category submits to existing review workflow");

        var selection = new CategoryCheckpointSelection(["Installation/1", "Apparatus/19"], 0);
        Check(await categoryController.SaveCategorySelection(project.Id, selection, authorizer, default) is OkObjectResult &&
            await categoryController.SaveCategorySelection(project.Id, selection, authorizer, default) is ConflictObjectResult,
            "project selection is saved and stale edits are rejected");
        var savedList = (OkObjectResult)await categoryController.CategoryChecklist(project.Id, equipment.Id, "All Checkpoints", null, authorizer, default);
        Check(System.Text.Json.JsonSerializer.SerializeToElement(savedList.Value).GetArrayLength() == 2,
            "project checklist shows only the saved checkpoints");
        Check(await categoryController.OpenCategoryChecklist(project.Id, equipment.Id, new(new DateOnly(2026, 11, 6)), authorizer, default) is OkObjectResult,
            "future inspection opens using the saved project selection");
        var nextCategory = await db.InspectionActivities.SingleAsync(a => a.Grade == null && a.Id != categoryActivity.Id);
        Check(await db.CheckpointDefinitionSnapshots.CountAsync(p => p.ChecklistVersionSnapshotId == nextCategory.ChecklistVersionId) == 2 &&
            await db.CheckpointDefinitionSnapshots.CountAsync(p => p.ChecklistVersionSnapshotId == categoryActivity.ChecklistVersionId) == 59,
            "saved selection applies to future inspections while historical checkpoints are preserved");
        var equipmentSelection = new CategoryCheckpointSelection(["Environment/1"], 0);
        Check(await categoryController.SaveCategorySelection(project.Id, equipmentSelection, authorizer, default, Guid.NewGuid()) is NotFoundResult &&
            await categoryController.SaveCategorySelection(project.Id, equipmentSelection, authorizer, default, equipment.Id) is OkObjectResult,
            "equipment selection requires equipment belonging to the selected project");
        var equipmentList = (OkObjectResult)await categoryController.CategoryChecklist(project.Id, equipment.Id, "All Checkpoints", null, authorizer, default);
        Check(System.Text.Json.JsonSerializer.SerializeToElement(equipmentList.Value).EnumerateArray().Single().GetProperty("Code").GetString() == "Environment/1",
            "equipment checklist overrides the project fallback");
        var surveyorCategory = new InspectionActivitiesController(db, workflow, Options.Create(new InspectionActivityOptions {Enabled = true}), null!)
        {
            ControllerContext = new ControllerContext {HttpContext = new DefaultHttpContext {User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, inspector.Id.ToString()), new Claim(ClaimTypes.Role, "Surveyor")], "test"))}}
        };
        mutableActor.UserId = inspector.Id;
        Check(await surveyorCategory.OpenCategoryChecklist(project.Id, equipment.Id, new(new DateOnly(2026, 11, 7)), authorizer, default) is OkObjectResult,
            "assigned surveyor can open a category inspection from the equipment selection");
        var surveyorActivity = await db.InspectionActivities.SingleAsync(a => a.Grade == null && a.InspectorUserId == inspector.Id);
        Check(await db.CheckpointDefinitionSnapshots.CountAsync(p => p.ChecklistVersionSnapshotId == surveyorActivity.ChecklistVersionId) == 1,
            "surveyor inspection contains only the equipment's selected checkpoints");

        var categoryChange = equipmentSelection with {Version = 1, CategoryChanges = new() {["Environment/1"] = "Ex 'e' 'n' only"}};
        Check(await surveyorCategory.SaveCategorySelection(project.Id, categoryChange, authorizer, default) is ForbidResult &&
            await surveyorCategory.SaveCategorySelection(Guid.NewGuid(), categoryChange, authorizer, default, equipment.Id) is NotFoundResult &&
            await surveyorCategory.SaveCategorySelection(project.Id, categoryChange, authorizer, default, equipment.Id) is OkObjectResult,
            "surveyors can change checkpoint categories only for equipment in an assigned project");
        mutableActor.UserId = categoryAdmin.Id;
        var moved = (OkObjectResult)await categoryController.CategoryChecklist(project.Id, equipment.Id, "Ex 'e' 'n' only", null, authorizer, default);
        var oldCategory = (OkObjectResult)await categoryController.CategoryChecklist(project.Id, equipment.Id, "Common Checkpoint", null, authorizer, default);
        Check(System.Text.Json.JsonSerializer.SerializeToElement(moved.Value).EnumerateArray().Single().GetProperty("Code").GetString() == "Environment/1" &&
            System.Text.Json.JsonSerializer.SerializeToElement(oldCategory.Value).GetArrayLength() == 0 &&
            await db.CheckpointDefinitionSnapshots.AnyAsync(x => x.ChecklistVersionSnapshotId == surveyorActivity.ChecklistVersionId &&
                x.QuestionCode == "Environment/1" && x.ApplicabilityCode == "Common check point"),
            "category change moves the filter result while preserving the existing inspection snapshot");

        Check(await categoryController.SaveChecklistCategory(new(null, "Ex 't' only", "Test category"), default) is NoContentResult &&
            await categoryController.SaveChecklistCategory(new(null, "EX 'T' ONLY", null), default) is BadRequestObjectResult,
            "catalog saves categories and rejects case-insensitive duplicates");
        var dynamicCatalog = (OkObjectResult)await categoryController.GetChecklistCatalog(default);
        var categoryId = System.Text.Json.JsonSerializer.SerializeToElement(dynamicCatalog.Value).GetProperty("Categories").EnumerateArray()
            .Single(c => c.GetProperty("Name").GetString() == "Ex 't' only").GetProperty("Id").GetString()!;
        Check(await categoryController.SaveChecklistCheckpoint(new(null, "Environment", 1, "Duplicate", categoryId), default) is BadRequestObjectResult &&
            await categoryController.SaveChecklistCheckpoint(new(null, "Environment", 4, "Custom checkpoint", categoryId), default) is NoContentResult &&
            await categoryController.SaveChecklistCheckpoint(new("Environment/1", "Environment", 1, "Change source", categoryId), default) is BadRequestObjectResult,
            "custom checkpoints reserve original PDF numbers and source descriptions");
        Check(await categoryController.SaveCategorySelection(project.Id, new(["Environment/4"], 2), authorizer, default, equipment.Id) is OkObjectResult &&
            await categoryController.SaveChecklistCheckpoint(new("Environment/4", "Environment", 4, "Revised custom checkpoint", categoryId), default) is NoContentResult &&
            await categoryController.SaveChecklistCategory(new(categoryId, "Ex 't' custom", null), default) is NoContentResult,
            "custom checkpoints can be selected and categories can be renamed without changing identity");
        Check(await categoryController.OpenCategoryChecklist(project.Id, equipment.Id, new(new DateOnly(2026, 11, 8)), authorizer, default) is OkObjectResult,
            "future inspection opens with a custom catalog checkpoint");
        var customActivity = await db.InspectionActivities.SingleAsync(a => a.Grade == null && a.StartedAt.Day == 8);
        Check(await db.CheckpointDefinitionSnapshots.AnyAsync(p => p.ChecklistVersionSnapshotId == customActivity.ChecklistVersionId &&
            p.QuestionCode == "Environment/4" && p.QuestionText == "Revised custom checkpoint" && p.ApplicabilityCode == categoryId),
            "future inspection snapshots the latest custom definition and stable category ID");

        await categoryController.SaveChecklistCheckpoint(new("Environment/4", "Environment", 4, "Updated before entry", categoryId), default);
        var reopened = (OkObjectResult)await categoryController.OpenCategoryChecklist(project.Id, equipment.Id, new(new DateOnly(2026, 11, 8)), authorizer, default);
        var reopenedId = System.Text.Json.JsonSerializer.SerializeToElement(reopened.Value).GetProperty("id").GetGuid();
        var refreshed = await db.InspectionActivities.SingleAsync(a => a.Id == reopenedId);
        Check(reopenedId == customActivity.Id && await db.CheckpointDefinitionSnapshots.AnyAsync(p =>
            p.ChecklistVersionSnapshotId == refreshed.ChecklistVersionId && p.QuestionText == "Updated before entry"),
            "an untouched draft adopts the updated checklist without creating another inspection");

        var emptySelection = new CategoryCheckpointSelection([], 3, new() {["Environment/1"] = "Ex 'e' 'n' only"});
        Check(await categoryController.SaveCategorySelection(project.Id, emptySelection, authorizer, default, equipment.Id) is OkObjectResult,
            "an empty selection can retain category mappings");
        mutableActor.UserId = inspector.Id;
        var full = (OkObjectResult)await surveyorCategory.CategoryChecklist(project.Id, equipment.Id, "All Checkpoints", null, authorizer, default);
        Check(System.Text.Json.JsonSerializer.SerializeToElement(full.Value).GetArrayLength() == 60 &&
            await surveyorCategory.OpenCategoryChecklist(project.Id, equipment.Id, new(new DateOnly(2026, 11, 9)), authorizer, default) is OkObjectResult,
            "surveyors can preview and start the full catalog when the equipment selection is empty");

        var gradeActivity = await db.InspectionActivities.SingleAsync(a => a.Grade == null && a.StartedAt.Day == 9);
        gradeActivity.RowVersion = [1];
        await db.SaveChangesAsync();
        var gradeDefinition = await db.CheckpointDefinitionSnapshots.FirstAsync(p => p.ChecklistVersionSnapshotId == gradeActivity.ChecklistVersionId);
        var gradeRequest = new SaveCheckpointResponseRequest(gradeDefinition.Id, null, null, null, "Grade remarks",
            Convert.ToBase64String(gradeActivity.RowVersion), GradeResults: new() {["D"] = "Pass", ["V"] = "Fail"}, InspectionComment: "Checkpoint comment");
        var graded = await workflow.SaveCheckpointResponseAsync(gradeActivity.Id, gradeRequest);
        var clearedGrade = await workflow.SaveCheckpointResponseAsync(gradeActivity.Id, gradeRequest with
            {GradeResults = new() {["D"] = null}, InspectionComment = null});
        var storedGrades = await db.CheckpointResponseRevisions.AsNoTracking().SingleAsync(r => r.Id == clearedGrade.Id);
        var gradeValues = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(storedGrades.GradeResultsJson!)!;
        Check(graded.ResponseCode == "Fail" && clearedGrade.ResponseCode == "Fail" && gradeValues["D"] is null &&
            gradeValues["V"] == "Fail" && !gradeValues.ContainsKey("C") && storedGrades.InspectionComment == "Checkpoint comment",
            "clearing Detailed leaves Visual unchanged and retains checkpoint comments");
        var allBlank = await workflow.SaveCheckpointResponseAsync(gradeActivity.Id, gradeRequest with {GradeResults = new() {["V"] = null}});
        Check(allBlank.ResponseCode is null, "clearing the remaining grade removes the aggregate result");

        var commentsRequest = new EquipmentCommentsRequest("Equipment-level remark", Convert.ToBase64String(gradeActivity.RowVersion));
        Check(await surveyorCategory.SaveEquipmentComments(gradeActivity.Id, commentsRequest, authorizer, default) is NoContentResult &&
            await db.InspectionActivities.AnyAsync(a => a.Id == gradeActivity.Id && a.EquipmentComments == "Equipment-level remark" &&
                a.ReviewStatus == InspectionActivityReviewStatus.Draft), "equipment comments save separately without changing review status");
        gradeActivity.ReviewStatus = InspectionActivityReviewStatus.Submitted;
        await db.SaveChangesAsync();
        Check(await surveyorCategory.SaveEquipmentComments(gradeActivity.Id, commentsRequest with {Comments = "Should be blocked"}, authorizer, default) is ConflictObjectResult,
            "submitted inspection equipment comments are locked");

        var autoDefect = await db.ActivityFindings.SingleAsync(f => f.InspectionActivityId == gradeActivity.Id);
        Check(autoDefect.Observation == "Grade remarks" && autoDefect.FailedGrades == "V" &&
            autoDefect.CheckpointResponseRevisionId == graded.Id, "saved Fail generates one source-linked defect that survives clearing the result");
        Check(await surveyorCategory.SaveInspectionDefectDetails(autoDefect.Id,
            new("AOSD-7", "B", "F-7", "Check seal", "First", "Second"), authorizer, default) is NoContentResult,
            "assigned surveyor can save defect register details");
        var latestPassRevision = await db.CheckpointResponseRevisions.SingleAsync(r => r.Id == allBlank.Id);
        latestPassRevision.ResponseCode = "Fail";
        latestPassRevision.GradeResultsJson = "{\"D\":\"Fail\"}";
        await db.SaveChangesAsync();
        Check(await surveyorCategory.ReconcileFailedCheckpoints(project.Id, authorizer, default) is NoContentResult &&
            await surveyorCategory.ReconcileFailedCheckpoints(project.Id, authorizer, default) is NoContentResult &&
            await db.ActivityFindings.CountAsync(f => f.InspectionActivityId == gradeActivity.Id) == 1 &&
            autoDefect.FailedGrades == "D,V" && autoDefect.FaultCode == "F-7" && autoDefect.FollowUpRemarks == "Second",
            "reconciliation adds failed grade associations without duplicating defects or replacing register edits");
        var photo = new SurveyFile {Category = FileCategory.FindingPhoto, FileName = "source.jpg", StorageKey = "source.jpg",
            ContentType = "image/jpeg", UploadedById = inspector.Id};
        db.Files.Add(photo);
        db.ActivityEvidence.Add(new ActivityEvidence {InspectionActivityId = gradeActivity.Id, CheckpointResponseRevisionId = clearedGrade.Id,
            SurveyFileId = photo.Id, AddedByUserId = inspector.Id});
        await db.SaveChangesAsync();
        var registerResult = (OkObjectResult)await surveyorCategory.DefectRegister(null, projectId: project.Id);
        var row = System.Text.Json.JsonSerializer.SerializeToElement(registerResult.Value).EnumerateArray()
            .Single(x => x.GetProperty("Id").GetGuid() == autoDefect.Id);
        Check(row.GetProperty("Photos").GetArrayLength() == 1 && row.GetProperty("InspectionDate").GetDateTimeOffset() == gradeActivity.StartedAt &&
            row.GetProperty("AosdId").GetString() == "AOSD-7", "register returns inspection date, editable metadata and the linked checkpoint photo");

        Console.WriteLine($"All {_checks} inspection activity foundation checks passed (synthetic EF InMemory only).");
    }

    private static bool ExpectUnauthorized(Action action)
    {
        try { action(); return false; } catch (UnauthorizedAccessException) { return true; }
    }

    private sealed class EmptyTenantContext : ITenantContextAccessor { public AuthorizedTenantContext? Current => null; }
    private sealed class MutableActor(Guid userId) : IInspectionActivityActorContext { public Guid? UserId { get; set; } = userId; }
    private sealed class FixedActorContext(Guid userId) : IInspectionActivityActorContext { public Guid? UserId => userId; }
}
