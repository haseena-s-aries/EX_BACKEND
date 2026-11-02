using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

internal static class LocalDbUiSeed
{
    private const string RequiredDatabase = "ExSurveyIntegration_Test";
    public static async Task Run()
    {
        var connection = Environment.GetEnvironmentVariable("EXSURVEY_INTEGRATION_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set EXSURVEY_INTEGRATION_TEST_CONNECTION to the isolated ExSurveyIntegration_Test connection. The prior ExSurveySprint2_Test database is intentionally not a seed target.");
        var parsedConnection = new DbConnectionStringBuilder { ConnectionString = connection };
        string Value(params string[] names)
        {
            foreach (var name in names)
                if (parsedConnection.TryGetValue(name, out var value)) return Convert.ToString(value) ?? "";
            return "";
        }
        var database = Value("Initial Catalog", "Database");
        if (!string.Equals(database, RequiredDatabase, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to seed database '{database}'. The only permitted seed target is {RequiredDatabase}.");
        if (!bool.TryParse(Value("Integrated Security", "Trusted_Connection"), out var integratedSecurity) || !integratedSecurity ||
            !bool.TryParse(Value("Encrypt"), out var encrypt) || !encrypt ||
            !bool.TryParse(Value("TrustServerCertificate"), out var trustCertificate) || trustCertificate)
            throw new InvalidOperationException("The isolated test connection must use integrated authentication, Encrypt=True, and TrustServerCertificate=False.");
        var password = Environment.GetEnvironmentVariable("EXSURVEY_E2E_PASSWORD");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 16) throw new InvalidOperationException("Supply a temporary EXSURVEY_E2E_PASSWORD of at least 16 characters; it is never printed or persisted in clear text.");
        var options = new DbContextOptionsBuilder<SurveyDbContext>().UseSqlServer(connection).Options;
        await using var db = new SurveyDbContext(options);
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
            throw new InvalidOperationException("Refusing to seed before all reviewed migrations are applied to ExSurveyIntegration_Test.");
        var allowedEmails = new[] { "integration-admin@example.test", "integration-manager@example.test", "sprint2-surveyor@example.test", "sprint2-qc@example.test", "integration-client@example.test" };
        var allowedTemplates = new[] { "Synthetic Integration Close", "Synthetic Integration Visual" };
        if (await db.Projects.AnyAsync(x => x.JobNo != "SPRINT2-E2E") ||
            await db.Users.AnyAsync(x => !allowedEmails.Contains(x.Email)) ||
            await db.ChecklistTemplates.AnyAsync(x => !allowedTemplates.Contains(x.Name)) ||
            await db.InspectionCampaigns.AnyAsync(x => x.Reference != "SYN-INT-2026"))
            throw new InvalidOperationException("Refusing to seed a database containing records outside this synthetic integration fixture. Use a newly created empty ExSurveyIntegration_Test database.");
        var hasher = new PasswordHasher<User>();
        var admin = await EnsureUser("integration-admin@example.test", "Synthetic Integration Admin", UserRole.Admin);
        var manager = await EnsureUser("integration-manager@example.test", "Synthetic Integration Lead", UserRole.Manager);
        var surveyor = await EnsureUser("sprint2-surveyor@example.test", "Sprint 2 Synthetic Surveyor", UserRole.Surveyor);
        var qc = await EnsureUser("sprint2-qc@example.test", "Sprint 2 Synthetic QC", UserRole.Qc);
        var client = await EnsureUser("integration-client@example.test", "Synthetic Integration Client", UserRole.Client);
        qc.ManagerId = manager.Id;
        await db.SaveChangesAsync();
        var project = await db.Projects.SingleOrDefaultAsync(x => x.JobNo == "SPRINT2-E2E") ?? new Project { ProjectName = "SPRINT 2 E2E TEST ONLY", JobNo = "SPRINT2-E2E", Sector = "Synthetic", Location = "LocalDB", VesselOrSiteName = "Synthetic Site", AssignedSurveyorId = surveyor.Id, AssignedQcId = qc.Id, CreatedByUserId = surveyor.Id };
        project.AssignedSurveyorId = surveyor.Id; project.AssignedQcId = qc.Id; project.AssignedClientId = client.Id; project.CreatedByUserId = admin.Id;
        if (db.Entry(project).State == EntityState.Detached) db.Projects.Add(project);
        var area = await db.ProjectAreas.SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.AreaName == "Sprint 2 Test Area") ?? new ProjectArea { ProjectId = project.Id, AreaName = "Sprint 2 Test Area" };
        if (db.Entry(area).State == EntityState.Detached) db.ProjectAreas.Add(area);
        var equipment = await db.AreaEquipment.SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.TagNumber == "SPRINT2-E2E-1") ?? new AreaEquipment { ProjectId = project.Id, AreaId = area.Id, SequenceNo = 1, XPercent = 50, YPercent = 50, TagNumber = "SPRINT2-E2E-1", Type = "Synthetic test equipment", CreatedByUserId = surveyor.Id };
        equipment.ExProtectionType = "Ex d"; equipment.Location = "Synthetic test location"; equipment.Description = "Synthetic integration fixture; no IEC conformity claimed";
        if (db.Entry(equipment).State == EntityState.Detached) db.AreaEquipment.Add(equipment);
        await db.SaveChangesAsync();
        await SeedTemplate(InspectionType.Close, "Synthetic Integration Close", admin.Id, qc.Id, [
            new("Confirm the test enclosure is intact", null, null, null, false),
            new("Record the synthetic current reading", "mA", 1m, 5m, true),
            new("Check the synthetic cable entry", null, null, null, false)
        ]);
        await SeedTemplate(InspectionType.Visual, "Synthetic Integration Visual", admin.Id, qc.Id, [
            new("Confirm the test nameplate is legible", null, null, null, false),
            new("Check the synthetic enclosure exterior", null, null, null, false)
        ]);
        var closeTemplate = await db.ChecklistTemplates.SingleAsync(x => x.Name == "Synthetic Integration Close" && x.InspectionType == InspectionType.Close);
        var snapshot = await db.ChecklistVersionSnapshots.SingleOrDefaultAsync(x => x.SourceChecklistTemplateId == closeTemplate.Id && x.VersionNumber == closeTemplate.Version && x.ProtectionTypeSnapshot == "Ex d");
        if (snapshot is null)
        {
            var questions = await db.ChecklistQuestions.Where(x => x.ChecklistTemplateId == closeTemplate.Id).OrderBy(x => x.SortOrder).ToListAsync();
            var hashInput = $"{closeTemplate.Id:D}|{closeTemplate.Version}|{closeTemplate.StandardReference}|{closeTemplate.StandardEdition}|Ex d\n" +
                string.Join("\n", questions.Select(x => $"{x.Id:D}|{x.SectionCode}|{x.SortOrder}|{x.Text}|{x.IsRequired}|{x.ResponseScaleCode}|{x.RequireRemarkOnFail}|{x.NumericReadingEnabled}|{x.MeasurementUnit}|{x.MinimumReading}|{x.MaximumReading}"));
            snapshot = new ChecklistVersionSnapshot { SourceChecklistTemplateId = closeTemplate.Id, VersionNumber = closeTemplate.Version, InspectionType = closeTemplate.InspectionType,
                SnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))), PublishedAt = closeTemplate.ApprovedAt!.Value,
                PublishedByUserId = closeTemplate.ApprovedByUserId!.Value, StandardReference = closeTemplate.StandardReference, StandardEdition = closeTemplate.StandardEdition,
                ControlledSourceReference = closeTemplate.ControlledSourceReference, ProtectionTypeSnapshot = "Ex d" };
            db.ChecklistVersionSnapshots.Add(snapshot);
            foreach (var question in questions)
                db.CheckpointDefinitionSnapshots.Add(new CheckpointDefinitionSnapshot { ChecklistVersionSnapshotId = snapshot.Id, QuestionCode = question.Id.ToString("N"),
                    QuestionVersion = closeTemplate.Version, QuestionText = question.Text, SortOrder = question.SortOrder, SectionCode = question.SectionCode,
                    ApplicabilityCode = question.ApplicableProtectionTypes, IsRequired = question.IsRequired, ResponseScaleCode = question.ResponseScaleCode,
                    RequireRemarkOnFail = question.RequireRemarkOnFail, NumericReadingEnabled = question.NumericReadingEnabled, MeasurementUnit = question.MeasurementUnit,
                    MinimumReading = question.MinimumReading, MaximumReading = question.MaximumReading, NumericReadingRequired = question.NumericReadingRequired });
            await db.SaveChangesAsync();
        }
        var plan = await db.InspectionPlans.SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Name == "Synthetic Integration Plan")
            ?? new InspectionPlan { ProjectId = project.Id, Name = "Synthetic Integration Plan", SourceReference = "TEST FIXTURE ONLY - NOT AN IEC SOURCE", StartsAt = DateTimeOffset.UtcNow.AddDays(-1), EndsAt = DateTimeOffset.UtcNow.AddDays(30) };
        if (db.Entry(plan).State == EntityState.Detached) db.InspectionPlans.Add(plan);
        var campaign = await db.InspectionCampaigns.SingleOrDefaultAsync(x => x.Reference == "SYN-INT-2026")
            ?? new InspectionCampaign { ProjectId = project.Id, Name = "Synthetic Integration Campaign", Reference = "SYN-INT-2026", StartsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date), EndsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(30)), CreatedByUserId = admin.Id };
        campaign.ProjectId = project.Id; campaign.InspectionLeadUserId = manager.Id; campaign.Status = CampaignStatus.InProgress;
        campaign.ClosePercent = 100m; campaign.DetailedPercent = 0m; campaign.MinimumDetailedCreditPercent = 0m; campaign.RoundingRule = AllocationRoundingRule.Nearest;
        campaign.AllocationApproved = true; campaign.AllocationApprovedByUserId = manager.Id; campaign.AllocationApprovedAt ??= DateTimeOffset.UtcNow;
        if (db.Entry(campaign).State == EntityState.Detached) db.InspectionCampaigns.Add(campaign);
        await db.SaveChangesAsync();
        var annual = await db.CampaignAnnualLists.SingleOrDefaultAsync(x => x.CampaignId == campaign.Id && x.Year == DateTime.UtcNow.Year)
            ?? new CampaignAnnualList { CampaignId = campaign.Id, Year = DateTime.UtcNow.Year, EquipmentCount = 1, PlannedCloseCount = 1, PlannedDetailedCount = 0 };
        annual.EquipmentCount = 1; annual.PlannedCloseCount = 1; annual.PlannedDetailedCount = 0;
        if (db.Entry(annual).State == EntityState.Detached) db.CampaignAnnualLists.Add(annual);
        var campaignEquipment = await db.CampaignEquipment.SingleOrDefaultAsync(x => x.AnnualListId == annual.Id && x.AreaEquipmentId == equipment.Id)
            ?? new CampaignEquipment { CampaignId = campaign.Id, AnnualListId = annual.Id, AreaEquipmentId = equipment.Id, EquipmentTypeSnapshot = equipment.Type };
        if (db.Entry(campaignEquipment).State == EntityState.Detached) db.CampaignEquipment.Add(campaignEquipment);
        var package = await db.InspectionWorkPackages.SingleOrDefaultAsync(x => x.CampaignId == campaign.Id && x.Reference == "SYN-INT-WP-01")
            ?? new InspectionWorkPackage { CampaignId = campaign.Id, ProjectId = project.Id, Reference = "SYN-INT-WP-01", Site = project.VesselOrSiteName, Area = area.AreaName, CreatedByUserId = manager.Id };
        if (db.Entry(package).State == EntityState.Detached) db.InspectionWorkPackages.Add(package);
        await db.SaveChangesAsync();
        var task = await db.InspectionTasks.SingleOrDefaultAsync(x => x.InspectionPlanId == plan.Id && x.AreaEquipmentId == equipment.Id && x.WorkPackageId == package.Id)
            ?? new InspectionTask { InspectionPlanId = plan.Id, AreaEquipmentId = equipment.Id, WorkPackageId = package.Id, WorkPackageReference = package.Reference, AssignedUserId = surveyor.Id, PlannedGrade = InspectionType.Close, RouteOrder = 1, ChecklistVersionId = snapshot.Id, DueAt = DateTimeOffset.UtcNow.AddDays(7) };
        task.AssignedUserId = surveyor.Id; task.PlannedGrade = InspectionType.Close; task.ChecklistVersionId = snapshot.Id;
        if (db.Entry(task).State == EntityState.Detached) db.InspectionTasks.Add(task);
        await db.SaveChangesAsync();
        Console.WriteLine("UI_TEST_SEED=ready; target=ExSurveyIntegration_Test; synthetic project SPRINT2-E2E; equipment SPRINT2-E2E-1; campaign SYN-INT-2026; work package SYN-INT-WP-01; roles Admin, Manager/Lead, Surveyor, QC and Client seeded; password omitted; checklist content is test-only and not IEC approved.");

        async Task<User> EnsureUser(string email, string display, UserRole role)
        {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email) ?? new User { Email = email, DisplayName = display, PasswordHash = "pending", Role = role };
            user.DisplayName = display; user.Role = role; user.ApprovalStatus = ApprovalStatus.Approved; user.IsActive = true;
            user.PasswordHash = hasher.HashPassword(user, password);
            if (db.Entry(user).State == EntityState.Detached) db.Users.Add(user);
            await db.SaveChangesAsync(); return user;
        }
        async Task SeedTemplate(InspectionType grade, string name, Guid creatorId, Guid approverId, (string Text, string? Unit, decimal? Min, decimal? Max, bool Required)[] rows)
        {
            var template = await db.ChecklistTemplates.SingleOrDefaultAsync(x => x.Name == name && x.InspectionType == grade);
            if (template is null) { template = new ChecklistTemplate { Name = name, InspectionType = grade, Version = 1, IsActive = true }; db.ChecklistTemplates.Add(template); await db.SaveChangesAsync(); }
            template.FamilyId ??= template.Id; template.LifecycleStatus = ChecklistLifecycleStatus.Published; template.ApplicableProtectionTypes = "Ex d";
            template.StandardReference = "TEST FIXTURE ONLY"; template.StandardEdition = "Synthetic"; template.ControlledSourceReference = "Synthetic integration test checklist; not IEC-approved";
            template.CreatedByUserId = creatorId; template.ApprovedByUserId = approverId; template.ApprovedAt ??= DateTimeOffset.UtcNow;
            template.IsActive = true;
            var existing = await db.ChecklistQuestions.Where(x => x.ChecklistTemplateId == template.Id).OrderBy(x => x.SortOrder).ToListAsync();
            for (var i = 0; i < rows.Length; i++)
            {
                var question = i < existing.Count ? existing[i] : new ChecklistQuestion { ChecklistTemplateId = template.Id, Text = rows[i].Text, SortOrder = i + 1 };
                question.Text = rows[i].Text; question.SortOrder = i + 1; question.IsRequired = true;
                question.NumericReadingEnabled = rows[i].Unit is not null; question.MeasurementUnit = rows[i].Unit; question.MinimumReading = rows[i].Min; question.MaximumReading = rows[i].Max; question.NumericReadingRequired = rows[i].Required;
                question.SectionCode = i == 0 ? "Apparatus" : i == 1 ? "Installation" : "Environment";
                question.ApplicableProtectionTypes = "Ex d"; question.RequireRemarkOnFail = true; question.ResponseScaleCode = "PassFailNA";
                if (i >= existing.Count) db.ChecklistQuestions.Add(question);
            }
            await db.SaveChangesAsync();
        }
    }
}
