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
