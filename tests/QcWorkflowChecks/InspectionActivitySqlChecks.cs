using Ex_SurveyApp_api.Contracts.V1;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class InspectionActivitySqlChecks
{
    private const string Connection = "Server=(localdb)\\ExSurveySprint2;Database=ExSurveySprint2_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Connect Timeout=10;Application Name=ExSurvey-Sql-Workflow-Checks";
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException($"SQL activity check failed: {message}"); Console.WriteLine($"PASS_SQL: {message}"); }

    public static async Task Run()
    {
        var options = new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer(Connection).Options;
        await using var db = new SurveyDbContext(options);
        await db.Database.MigrateAsync();
        var migrationHistory = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Check(Array.IndexOf(migrationHistory, "20261029123545_Phase2aTenantFoundation") < Array.IndexOf(migrationHistory, "20261030104113_InspectionActivityFoundation") && migrationHistory.Contains("20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews"), "Phase 2A, Phase 2D and Sprint 2 migration applied in sequence");

        var requiredTables = new[] { "InspectionPlans", "InspectionTasks", "InspectionActivities", "InspectionActivityRevisions", "ChecklistVersionSnapshots", "CheckpointDefinitionSnapshots", "CheckpointResponseRevisions", "ActivityReviewEvents", "ActivityFindings", "CorrectiveActions", "CorrectiveActionEvents", "ActivityEvidence", "CheckpointReviewEvents" };
        await using (var connection = new SqlConnection(Connection))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sys.tables WHERE name IN (" + string.Join(',', requiredTables.Select((_, i) => $"@p{i}")) + ")";
            for (var i = 0; i < requiredTables.Length; i++) cmd.Parameters.AddWithValue($"@p{i}", requiredTables[i]);
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await cmd.ExecuteReaderAsync()) { while (await reader.ReadAsync()) found.Add(reader.GetString(0)); }
            Check(requiredTables.All(found.Contains), "all 13 inspection tables exist in the persisted test database");
            await using var integrity = connection.CreateCommand();
            integrity.CommandText = "SELECT (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN (SELECT object_id FROM sys.tables WHERE name LIKE 'Inspection%' OR name LIKE 'Checkpoint%' OR name LIKE 'Activity%' OR name LIKE 'Corrective%')), (SELECT COUNT(*) FROM sys.indexes WHERE object_id IN (SELECT object_id FROM sys.tables WHERE name LIKE 'Inspection%' OR name LIKE 'Checkpoint%' OR name LIKE 'Activity%' OR name LIKE 'Corrective%') AND index_id > 0), (SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_InspectionTasks_OneEquipment'), (SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id WHERE t.name='SurveySessions' AND i.is_unique=1 AND i.filter_definition LIKE '%AreaEquipmentId%')";
            await using var integrityReader = await integrity.ExecuteReaderAsync(); await integrityReader.ReadAsync();
            Console.WriteLine($"SQL_METADATA_CHECKS=foreign_keys:{integrityReader.GetInt32(0)};indexes:{integrityReader.GetInt32(1)};task_check:{integrityReader.GetInt32(2)};legacy_unique:{integrityReader.GetInt32(3)}");
            Check(integrityReader.GetInt32(0) >= 37 && integrityReader.GetInt32(1) >= 50 && integrityReader.GetInt32(2) == 1 && integrityReader.GetInt32(3) == 1, "37+ persisted FKs, 50+ indexes, task constraint, and legacy SurveySession unique index verified");
        }

        var suffix = Guid.NewGuid().ToString("N");
        var surveyor = NewUser($"surveyor-{suffix}@example.test", "Synthetic Surveyor", UserRole.Surveyor);
        var qc = NewUser($"qc-{suffix}@example.test", "Synthetic QC", UserRole.Qc);
        var manager = NewUser($"manager-{suffix}@example.test", "Synthetic Manager", UserRole.Manager);
        var client = NewUser($"client-{suffix}@example.test", "Synthetic Client", UserRole.Client);
        db.Users.AddRange(surveyor, qc, manager, client); await db.SaveChangesAsync();
        var project = new Project { ProjectName = "Synthetic activity project", JobNo = $"SYN-{suffix[..8]}", Sector = "Test", Location = "LocalDB", VesselOrSiteName = "Synthetic only", AssignedSurveyorId = surveyor.Id, AssignedQcId = qc.Id, AssignedClientId = client.Id, CreatedByUserId = manager.Id };
        var area = new ProjectArea { ProjectId = project.Id, AreaName = "Synthetic area" };
        db.Projects.Add(project); db.ProjectAreas.Add(area); await db.SaveChangesAsync();
        var equipment = new AreaEquipment { ProjectId = project.Id, AreaId = area.Id, SequenceNo = 1, XPercent = 10, YPercent = 10, TagNumber = $"SYN-EQ-{suffix[..8]}", Type = "Test equipment", CreatedByUserId = surveyor.Id };
        var closeTemplate = new ChecklistTemplate { Name = $"Synthetic Close {suffix}", InspectionType = InspectionType.Close, Version = 1 };
        var visualTemplate = new ChecklistTemplate { Name = $"Synthetic Visual {suffix}", InspectionType = InspectionType.Visual, Version = 1 };
        db.AreaEquipment.Add(equipment); db.ChecklistTemplates.AddRange(closeTemplate, visualTemplate); await db.SaveChangesAsync();
        var closeQuestion = new ChecklistQuestion { ChecklistTemplateId = closeTemplate.Id, Text = "Synthetic close checkpoint", SortOrder = 1, IsRequired = true, NumericReadingEnabled = true, MeasurementUnit = "mA", MinimumReading = 1m, MaximumReading = 5m, NumericReadingRequired = true };
        var visualQuestion = new ChecklistQuestion { ChecklistTemplateId = visualTemplate.Id, Text = "Synthetic visual checkpoint", SortOrder = 1, IsRequired = true };
        db.ChecklistQuestions.AddRange(closeQuestion, visualQuestion);
        var closeVersion = NewVersion(closeTemplate, manager.Id); var visualVersion = NewVersion(visualTemplate, manager.Id);
        db.ChecklistVersionSnapshots.AddRange(closeVersion, visualVersion); await db.SaveChangesAsync();
        var closeDefinition = NewDefinition(closeVersion, closeQuestion); var visualDefinition = NewDefinition(visualVersion, visualQuestion);
        db.CheckpointDefinitionSnapshots.AddRange(closeDefinition, visualDefinition); await db.SaveChangesAsync();

        var actors = new MutableActor(surveyor.Id);
        var service = CreateService(db, actors);
        actors.UserId = manager.Id;
        var plan = await service.CreatePlanAsync(new(project.Id, $"Synthetic plan {suffix}", null, null, null));
        var task = await service.CreateTaskAsync(new(plan.Id, equipment.Id, null, InspectionTaskKind.Regular, "SQL-TEST", null, surveyor.Id, null));
        actors.UserId = surveyor.Id;
        var closeActivity = await service.CreateActivityAsync(task.Id, new(InspectionType.Close, closeVersion.Id, DateTimeOffset.UtcNow, null));
        var savedActivity = await db.InspectionActivities.SingleAsync(x => x.Id == closeActivity.Id);
        var answer = await service.SaveCheckpointResponseAsync(closeActivity.Id, new(closeDefinition.Id, null, "Fail", null, "Synthetic failed checkpoint", Convert.ToBase64String(savedActivity.RowVersion), 8.5m));
        Check(answer.IsOutOfRange == true && answer.MeasurementUnit == "mA", "numeric value and out-of-range state use the checklist-configured unit/limits");
        var findingResponse = await service.AddFindingAsync(closeActivity.Id, new(answer.Id, "Synthetic finding", "Synthetic correction", "Proposed", "FC-1", "Special tools", null, DateTimeOffset.UtcNow.AddDays(-1), "Synthetic responsible party"));
        Check(await db.ActivityFindings.AnyAsync(x => x.Id == findingResponse.Id && x.FaultCode == "FC-1" && x.ReportedByUserId == surveyor.Id && x.DueAt != null && x.ResponsibleParty == "Synthetic responsible party"), "defect is linked to failed checkpoint with fault, reporting actor, due date and responsible party");
        var syntheticPhoto = new SurveyFile { Category = FileCategory.FindingPhoto, FileName = "synthetic.jpg", StorageKey = "synthetic/activity.jpg", ContentType = "image/jpeg", UploadedById = surveyor.Id };
        db.Files.Add(syntheticPhoto); await db.SaveChangesAsync();
        actors.UserId = surveyor.Id;
        await service.AddEvidenceAsync(closeActivity.Id, new(syntheticPhoto.Id, answer.Id, null, null, ActivityEvidenceRole.Checkpoint, "Synthetic evidence metadata", null));
        await service.SubmitForReviewAsync(closeActivity.Id);
        actors.UserId = qc.Id;
        await service.RecordCheckpointReviewAsync(closeActivity.Id, new(answer.Id, CheckpointReviewDecision.ChangesRequested, "Reading exceeds configured range"));
        await service.RecordReviewAsync(closeActivity.Id, new(InspectionActivityReviewStatus.ChangesRequested, "Synthetic QC return comment"));
        actors.UserId = surveyor.Id;
        savedActivity = await db.InspectionActivities.SingleAsync(x => x.Id == closeActivity.Id);
        var corrected = await service.SaveCheckpointResponseAsync(closeActivity.Id, new(closeDefinition.Id, null, "Pass", null, "Synthetic corrected after review", Convert.ToBase64String(savedActivity.RowVersion), 4.2m));
        Check(corrected.IsOutOfRange == false && corrected.MeasurementUnit == "mA", "corrected numeric checkpoint is persisted in range as a new response version");
        await service.SubmitForReviewAsync(closeActivity.Id);
        actors.UserId = qc.Id;
        await service.RecordCheckpointReviewAsync(closeActivity.Id, new(corrected.Id, CheckpointReviewDecision.Accepted, "Reading verified"));
        await service.RecordReviewAsync(closeActivity.Id, new(InspectionActivityReviewStatus.Accepted, "Synthetic approved"));
        Check(await db.ActivityReviewEvents.CountAsync(x => x.InspectionActivityId == closeActivity.Id) == 4, "submission, return, resubmission and approval history persisted");
        Check(await db.CheckpointReviewEvents.CountAsync(x => x.InspectionActivityId == closeActivity.Id) == 2, "checkpoint-level QC decisions and comments persist independently");
        Check(await db.CheckpointResponseRevisions.CountAsync(x => x.CheckpointDefinitionSnapshotId == closeDefinition.Id) == 2, "checkpoint correction is append-only across QC return");
        Check(await db.ActivityFindings.AnyAsync(x => x.InspectionActivityId == closeActivity.Id && x.StatusCode == "Open"), "inspection approval does not close linked findings");
        Check(await db.ActivityEvidence.AnyAsync(x => x.InspectionActivityId == closeActivity.Id && x.AreaEquipmentId == equipment.Id && x.AddedByUserId == surveyor.Id && x.CheckpointResponseRevisionId == answer.Id), "photo evidence is persistently associated to equipment/activity/checkpoint/uploader/time");
        var immutable = false; try { InspectionActivityRules.EnsureRevisionAllowed(InspectionActivityReviewStatus.Accepted); } catch (InvalidOperationException) { immutable = true; }
        Check(immutable, "accepted activity edit guard remains active after persisted approval");

        var corrective = await service.CreateCorrectiveActionAsync(new(findingResponse.Id, $"CA-{suffix[..12]}", "Supplementary details", "Repair seal", "WO-TEST", null, "Synthetic responsible party"));
        var actionPhoto = new SurveyFile { Category = FileCategory.FindingPhoto, FileName = "synthetic-after.jpg", StorageKey = "synthetic/after.jpg", ContentType = "image/jpeg", UploadedById = surveyor.Id }; db.Files.Add(actionPhoto); await db.SaveChangesAsync();
        await service.AddEvidenceAsync(closeActivity.Id, new(actionPhoto.Id, null, corrective.Id, null, ActivityEvidenceRole.After, "Synthetic after evidence", null));
        await service.SubmitCorrectiveActionForVerificationAsync(corrective.Id);
        actors.UserId = qc.Id;
        var rejected = await service.VerifyCorrectiveActionAsync(corrective.Id, new("Correction not demonstrated", false));
        Check(rejected.Status == CorrectiveActionStatus.Reopened && await db.CorrectiveActionEvents.AnyAsync(x => x.CorrectiveActionId == corrective.Id && x.Kind == CorrectiveActionEventKind.VerificationRejected), "Aries rejects rectification with reason while preserving rejection history");
        actors.UserId = surveyor.Id;
        await service.ReportRectificationAsync(corrective.Id, new("Seal replaced and tested", "WO-TEST-2", DateTimeOffset.UtcNow, null, "Correction completed"));
        var secondPhoto = new SurveyFile { Category = FileCategory.FindingPhoto, FileName = "synthetic-after-2.jpg", StorageKey = "synthetic/after-2.jpg", ContentType = "image/jpeg", UploadedById = surveyor.Id }; db.Files.Add(secondPhoto); await db.SaveChangesAsync();
        await service.AddEvidenceAsync(closeActivity.Id, new(secondPhoto.Id, null, corrective.Id, null, ActivityEvidenceRole.After, "Second synthetic after evidence", null));
        await service.SubmitCorrectiveActionForVerificationAsync(corrective.Id);
        actors.UserId = qc.Id;
        var verified = await service.VerifyCorrectiveActionAsync(corrective.Id, new("Verified by synthetic QC"));
        Check(verified.Status == CorrectiveActionStatus.Verified && await db.ActivityEvidence.CountAsync(x => x.CorrectiveActionEventId != null) >= 2, "rectification can be resubmitted and verified with preserved evidence history");
        var qcCannotClose = false; try { await service.DecideClientClosureAsync(corrective.Id, new(true, "QC cannot close")); } catch (UnauthorizedAccessException) { qcCannotClose = true; }
        Check(qcCannotClose, "QC role cannot formally close a KAS3-style defect");
        actors.UserId = client.Id;
        var closed = await service.DecideClientClosureAsync(corrective.Id, new(true, "Client accepts correction"));
        Check(closed.Status == CorrectiveActionStatus.ClientClosed && !await db.CorrectiveActions.Where(x => x.Id == corrective.Id).Select(x => x.IsOutstanding).SingleAsync(), "assigned client closes only after Aries verification and action is no longer outstanding");
        var reopened = await service.DecideClientClosureAsync(corrective.Id, new(false, "Client evidence review requests reopening"));
        Check(reopened.Status == CorrectiveActionStatus.Reopened && await db.CorrectiveActionEvents.CountAsync(x => x.CorrectiveActionId == corrective.Id) >= 8, "client reopening records a separate decision and full corrective-action history");

        actors.UserId = surveyor.Id;
        var visualActivity = await service.CreateActivityAsync(task.Id, new(InspectionType.Visual, visualVersion.Id, DateTimeOffset.UtcNow, null));
        Check(visualActivity.Id != closeActivity.Id && await db.InspectionActivities.CountAsync(x => x.InspectionTaskId == task.Id) == 2, "second Visual attempt coexists with approved Close activity on the same equipment");
        Check((await db.Database.GetAppliedMigrationsAsync()).Contains("20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews"), "migration history records Sprint 2 schema on SQL Server");

        await SqlBackupRestore.RoundTripAsync(Connection, "ExSurveySprint2_Test");
    }

    private static User NewUser(string email, string name, UserRole role) => new() { Email = email, DisplayName = name, PasswordHash = "synthetic-test-only", Role = role, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
    private static ChecklistVersionSnapshot NewVersion(ChecklistTemplate template, Guid publisher) => new() { SourceChecklistTemplateId = template.Id, VersionNumber = template.Version, InspectionType = template.InspectionType, SnapshotHash = Guid.NewGuid().ToString("N"), PublishedAt = DateTimeOffset.UtcNow, PublishedByUserId = publisher };
    private static CheckpointDefinitionSnapshot NewDefinition(ChecklistVersionSnapshot version, ChecklistQuestion question) => new() { ChecklistVersionSnapshotId = version.Id, QuestionCode = question.Id.ToString("N"), QuestionVersion = version.VersionNumber, QuestionText = question.Text, SortOrder = question.SortOrder, IsRequired = question.IsRequired, ResponseScaleCode = "PassFailNA", NumericReadingEnabled = question.NumericReadingEnabled, MeasurementUnit = question.MeasurementUnit, MinimumReading = question.MinimumReading, MaximumReading = question.MaximumReading, NumericReadingRequired = question.NumericReadingRequired };
    private static InspectionActivityApplicationService CreateService(SurveyDbContext db, MutableActor actor)
    {
        var owner = new InspectionTenantOwnershipResolver(db);
        var authorizer = new InspectionActivityAuthorizer(db, owner, new TenantScopeResolver(db), new EmptyTenantContext(), actor, Options.Create(new TenantFoundationOptions { Enabled = false }));
        return new InspectionActivityApplicationService(db, authorizer, actor, owner, new CorrectiveActionPolicy(), Options.Create(new InspectionActivityOptions { Enabled = true }), TimeProvider.System);
    }
    private sealed class MutableActor(Guid id) : IInspectionActivityActorContext { public Guid? UserId { get; set; } = id; }
    private sealed class EmptyTenantContext : ITenantContextAccessor { public AuthorizedTenantContext? Current => null; }
}
