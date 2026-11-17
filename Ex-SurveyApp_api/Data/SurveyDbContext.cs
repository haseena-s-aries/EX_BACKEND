using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Data;

public class SurveyDbContext(DbContextOptions<SurveyDbContext> options) : DbContext(options)
{
    public DbSet<RoleChangeRequest> RoleChangeRequests => Set<RoleChangeRequest>();
    public DbSet<UserAccessNotification> UserAccessNotifications => Set<UserAccessNotification>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AccountActionToken> AccountActionTokens => Set<AccountActionToken>();
    public DbSet<RegistrationOtp> RegistrationOtps => Set<RegistrationOtp>();
    public DbSet<ProjectReportWorkflow> ProjectReportWorkflows => Set<ProjectReportWorkflow>();
    public DbSet<ReportWorkflowEvent> ReportWorkflowEvents => Set<ReportWorkflowEvent>();
    public DbSet<ProjectClientAssignment> ProjectClientAssignments => Set<ProjectClientAssignment>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CompanyMaster> CompanyMasters => Set<CompanyMaster>();
    public DbSet<ProjectArea> ProjectAreas => Set<ProjectArea>();
    public DbSet<ProjectAreaPhoto> ProjectAreaPhotos => Set<ProjectAreaPhoto>();
    public DbSet<AreaEquipment> AreaEquipment => Set<AreaEquipment>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobAssignment> JobAssignments => Set<JobAssignment>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<ChecklistQuestion> ChecklistQuestions => Set<ChecklistQuestion>();
    public DbSet<ProjectScheduleEntry> ProjectScheduleEntries => Set<ProjectScheduleEntry>();
    public DbSet<ProjectDelayEmail> ProjectDelayEmails => Set<ProjectDelayEmail>();
    public DbSet<SurveySession> SurveySessions => Set<SurveySession>();
    public DbSet<SurveyAnswer> SurveyAnswers => Set<SurveyAnswer>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<SurveyFile> Files => Set<SurveyFile>();
    public DbSet<QcReview> QcReviews => Set<QcReview>();
    public DbSet<ReportRevision> ReportRevisions => Set<ReportRevision>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SyncBatch> SyncBatches => Set<SyncBatch>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Contract> TenantContracts => Set<Contract>();
    public DbSet<TenantProjectBinding> TenantProjectBindings => Set<TenantProjectBinding>();
    public DbSet<Site> TenantSites => Set<Site>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<TenantRoleGrant> TenantRoleGrants => Set<TenantRoleGrant>();
    public DbSet<LegacyEntityMap> LegacyEntityMaps => Set<LegacyEntityMap>();
    public DbSet<TenantBackfillException> TenantBackfillExceptions => Set<TenantBackfillException>();
    public DbSet<InspectionPlan> InspectionPlans => Set<InspectionPlan>();
    public DbSet<InspectionTask> InspectionTasks => Set<InspectionTask>();
    public DbSet<InspectionActivity> InspectionActivities => Set<InspectionActivity>();
    public DbSet<InspectionActivityRevision> InspectionActivityRevisions => Set<InspectionActivityRevision>();
    public DbSet<EquipmentChecklistCatalog> EquipmentChecklistCatalogs => Set<EquipmentChecklistCatalog>();
    public DbSet<ChecklistVersionSnapshot> ChecklistVersionSnapshots => Set<ChecklistVersionSnapshot>();
    public DbSet<CheckpointDefinitionSnapshot> CheckpointDefinitionSnapshots => Set<CheckpointDefinitionSnapshot>();
    public DbSet<CheckpointResponseRevision> CheckpointResponseRevisions => Set<CheckpointResponseRevision>();
    public DbSet<ActivityReviewEvent> ActivityReviewEvents => Set<ActivityReviewEvent>();
    public DbSet<ActivityFinding> ActivityFindings => Set<ActivityFinding>();
    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();
    public DbSet<CorrectiveActionEvent> CorrectiveActionEvents => Set<CorrectiveActionEvent>();
    public DbSet<ActivityEvidence> ActivityEvidence => Set<ActivityEvidence>();
    public DbSet<CheckpointReviewEvent> CheckpointReviewEvents => Set<CheckpointReviewEvent>();
    public DbSet<InspectionCampaign> InspectionCampaigns => Set<InspectionCampaign>();
    public DbSet<CampaignAnnualList> CampaignAnnualLists => Set<CampaignAnnualList>();
    public DbSet<CampaignEquipment> CampaignEquipment => Set<CampaignEquipment>();
    public DbSet<SamplingApproval> SamplingApprovals => Set<SamplingApproval>();
    public DbSet<InspectionWorkPackage> InspectionWorkPackages => Set<InspectionWorkPackage>();
    public DbSet<InspectionTaskAssignmentEvent> InspectionTaskAssignmentEvents => Set<InspectionTaskAssignmentEvent>();
    public DbSet<PlanningApprovalEvent> PlanningApprovalEvents => Set<PlanningApprovalEvent>();
    public DbSet<InspectionRecurrence> InspectionRecurrences => Set<InspectionRecurrence>();
    public DbSet<OfflineSyncReceipt> OfflineSyncReceipts => Set<OfflineSyncReceipt>();
    public DbSet<InspectionActivityDocumentUsage> InspectionActivityDocumentUsages => Set<InspectionActivityDocumentUsage>();
    public DbSet<OfflinePhotoSyncReceipt> OfflinePhotoSyncReceipts => Set<OfflinePhotoSyncReceipt>();
    public DbSet<EquipmentTagAssignment> EquipmentTagAssignments => Set<EquipmentTagAssignment>();
    public DbSet<EquipmentTagHistory> EquipmentTagHistory => Set<EquipmentTagHistory>();
    public DbSet<EquipmentScanEvent> EquipmentScanEvents => Set<EquipmentScanEvent>();
    public DbSet<GeneratedReportArtifact> GeneratedReportArtifacts => Set<GeneratedReportArtifact>();
    public DbSet<EquipmentReferenceDocument> EquipmentReferenceDocuments => Set<EquipmentReferenceDocument>();
    public DbSet<EquipmentDocumentRequirementPolicy> EquipmentDocumentRequirementPolicies => Set<EquipmentDocumentRequirementPolicy>();
    public DbSet<EquipmentDocumentUseEvent> EquipmentDocumentUseEvents => Set<EquipmentDocumentUseEvent>();
    public DbSet<EquipmentReferenceDocumentEvent> EquipmentReferenceDocumentEvents => Set<EquipmentReferenceDocumentEvent>();

    public DbSet<ReportClientRemark> ReportClientRemarks => Set<ReportClientRemark>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Record real updates for client activity without modifying technical workflow data.
        foreach (var entry in ChangeTracker.Entries<Finding>().Where(e => e.State == EntityState.Modified).ToArray())
            AuditLogs.Add(new AuditLog {EntityType = "Finding", EntityId = entry.Entity.Id, Action = entry.Entity.Status is FindingStatus.Closed or FindingStatus.Verified ? "Defect Closed" : "Defect Updated", Comments = entry.Entity.Status.ToString()});
        foreach (var entry in ChangeTracker.Entries<Project>().Where(e => e.State == EntityState.Modified && e.Property(p => p.Status).IsModified && e.Entity.Status == ProjectStatus.Completed).ToArray())
            AuditLogs.Add(new AuditLog {EntityType = "Project", EntityId = entry.Entity.Id, Action = "Survey Completed"});
        var added = ChangeTracker.Entries<AreaEquipment>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToArray();
        var deleted = ChangeTracker.Entries<AreaEquipment>().Where(e => e.State == EntityState.Deleted).Select(e => e.Entity).ToArray();
        await using var sequenceTransaction = Database.IsSqlServer() && (added.Length + deleted.Length) > 0 && Database.CurrentTransaction is null ? await Database.BeginTransactionAsync(cancellationToken) : null;
        {
            foreach (var group in added.Concat(deleted).GroupBy(e => e.ProjectId).OrderBy(g => g.Key))
            {
                var resource = "EquipmentSequence:" + group.Key;
                if (Database.IsSqlServer())
                {
                    await Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @result < 0 THROW 51000, 'Equipment numbering is busy. Please retry.', 1;", cancellationToken);
                }
                var maximum = await AreaEquipment.Where(e => e.ProjectId == group.Key).MaxAsync(e => (int?)e.SequenceNo, cancellationToken) ?? 0;
                var history = await AuditLogs.Where(a => a.EntityType == "EquipmentSequence" && a.EntityId == group.Key).Select(a => a.Comments).ToListAsync(cancellationToken);
                foreach (var value in history) if (int.TryParse(value, out var previous)) maximum = Math.Max(maximum, previous);
                foreach (var item in added.Where(e=>e.ProjectId==group.Key).OrderBy(e=>e.CreatedAt).ThenBy(e=>e.Id)) item.SequenceNo = checked(++maximum);
                AuditLogs.Add(new AuditLog {EntityType="EquipmentSequence", EntityId=group.Key, Action="Sequence Reserved", Comments=maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)});
            }
            foreach (var group in added.Concat(deleted).GroupBy(e => e.AreaId).OrderBy(g => g.Key))
            {
                var resource = "EquipmentMarking:" + group.Key;
                if (Database.IsSqlServer())
                    await Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @result < 0 THROW 51000, 'Area marking numbering is busy. Please retry.', 1;", cancellationToken);
                var maximum = await AreaEquipment.Where(e => e.AreaId == group.Key)
                    .MaxAsync(e => e.MarkingNo, cancellationToken) ?? 0;
                var history = await AuditLogs.Where(a => a.EntityType == "EquipmentMarking" && a.EntityId == group.Key)
                    .Select(a => a.Comments).ToListAsync(cancellationToken);
                foreach (var value in history)
                    if (int.TryParse(value, out var previous)) maximum = Math.Max(maximum, previous);
                foreach (var item in added.Where(e => e.AreaId == group.Key && !e.MarkingNo.HasValue)
                             .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id))
                    item.MarkingNo = checked(++maximum);
                AuditLogs.Add(new AuditLog {EntityType="EquipmentMarking", EntityId=group.Key,
                    Action="Marking Reserved", Comments=maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)});
            }
            var result = await base.SaveChangesAsync(cancellationToken);
            if (sequenceTransaction is not null) await sequenceTransaction.CommitAsync(cancellationToken);
            return result;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FieldworkReceipt>(e => { e.HasKey(x => new {x.UserId, x.OperationId}); e.Property(x => x.RequestHash).HasMaxLength(64); e.HasIndex(x => x.ProjectId); });
        var checklistCatalog = modelBuilder.Entity<EquipmentChecklistCatalog>();
        checklistCatalog.Property(x => x.Id).ValueGeneratedNever();
        checklistCatalog.Property(x => x.Version).IsConcurrencyToken();

        var referenceDocument = modelBuilder.Entity<EquipmentReferenceDocument>();
        referenceDocument.ToTable("EquipmentReferenceDocuments");
        referenceDocument.HasKey(x => x.Id);
        referenceDocument.Property(x => x.Title).HasMaxLength(255).IsRequired();
        referenceDocument.Property(x => x.CategoryCode).HasMaxLength(80).IsRequired();
        referenceDocument.Property(x => x.DocumentNumber).HasMaxLength(120);
        referenceDocument.Property(x => x.Revision).HasMaxLength(100);
        referenceDocument.Property(x => x.IssuingOrganization).HasMaxLength(255);
        referenceDocument.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        referenceDocument.Property(x => x.StoragePath).HasMaxLength(1000).IsRequired();
        referenceDocument.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        referenceDocument.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        referenceDocument.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        referenceDocument.Property(x => x.ApprovalComment).HasMaxLength(2000);
        referenceDocument.HasIndex(x => new { x.ProjectId, x.EquipmentId, x.CategoryCode, x.Status });
        referenceDocument.HasIndex(x => x.SupersedesDocumentId);
        referenceDocument.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        referenceDocument.HasOne<ProjectArea>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        referenceDocument.HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        referenceDocument.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        referenceDocument.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        referenceDocument.HasOne<EquipmentReferenceDocument>().WithMany().HasForeignKey(x => x.SupersedesDocumentId).OnDelete(DeleteBehavior.Restrict);

        var requirement = modelBuilder.Entity<EquipmentDocumentRequirementPolicy>();
        requirement.ToTable("EquipmentDocumentRequirementPolicies");
        requirement.HasKey(x => x.Id);
        requirement.Property(x => x.CategoryCode).HasMaxLength(80).IsRequired();
        requirement.Property(x => x.EquipmentType).HasMaxLength(100);
        requirement.HasIndex(x => new { x.ProjectId, x.EquipmentType, x.WorkPackageId, x.CategoryCode, x.IsApproved });
        requirement.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        requirement.HasOne<InspectionWorkPackage>().WithMany().HasForeignKey(x => x.WorkPackageId).OnDelete(DeleteBehavior.Restrict);

        var documentUse = modelBuilder.Entity<EquipmentDocumentUseEvent>();
        documentUse.ToTable("EquipmentDocumentUseEvents");
        documentUse.HasKey(x => x.Id);
        documentUse.Property(x => x.DocumentRevision).HasMaxLength(100).IsRequired();
        documentUse.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        documentUse.HasIndex(x => new { x.ActivityId, x.DocumentId, x.DocumentRevision, x.UserId, x.OpenedAt });
        documentUse.HasOne<EquipmentReferenceDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        documentUse.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        documentUse.HasOne<InspectionActivity>().WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Restrict);

        var documentEvent = modelBuilder.Entity<EquipmentReferenceDocumentEvent>();
        documentEvent.ToTable("EquipmentReferenceDocumentEvents");
        documentEvent.HasKey(x => x.Id);
        documentEvent.Property(x => x.EventCode).HasMaxLength(30).IsRequired();
        documentEvent.Property(x => x.Comments).HasMaxLength(2000);
        documentEvent.HasIndex(x => new { x.DocumentId, x.OccurredAt });
        documentEvent.HasOne<EquipmentReferenceDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        documentEvent.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        var remark = modelBuilder.Entity<ReportClientRemark>();
        remark.Property(r => r.Remark).HasMaxLength(4000);
        remark.HasIndex(r => new {r.ProjectId, r.CreatedAt});
        remark.HasOne<Project>().WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Cascade);
        remark.HasOne<User>().WithMany().HasForeignKey(r => r.ClientUserId).OnDelete(DeleteBehavior.Restrict);
        // Optional context IDs are validated against the assigned project by the remarks API.
        // No extra cascades are introduced into existing Equipment/Finding/Report relationships.
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<User>().Property(x => x.PhoneNumber).HasMaxLength(40);
        modelBuilder.Entity<User>().Property(x => x.Designation).HasMaxLength(150);
        modelBuilder.Entity<User>().Property(x => x.ProfilePhotoPath).HasMaxLength(500);
        modelBuilder.Entity<User>().Property(x => x.AuthVersion).HasDefaultValue(0).IsConcurrencyToken();
        modelBuilder.Entity<AccountActionToken>(b => {
            b.Property(x => x.Purpose).HasMaxLength(30);
            b.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
            b.Property(x => x.NewEmail).HasMaxLength(320);
            b.Property(x => x.ConsumedAt).IsConcurrencyToken();
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => new {x.UserId, x.Purpose, x.CreatedAt});
            b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<User>().HasOne(x => x.Manager).WithMany()
            .HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<User>().Property(u => u.ApprovalRejectionReason).HasMaxLength(2000);
        modelBuilder.Entity<User>().Property(u => u.ApprovalStatus).IsConcurrencyToken();
        modelBuilder.Entity<User>().Property(u => u.Role).IsConcurrencyToken();
        modelBuilder.Entity<RoleChangeRequest>().Property(r => r.Reason).HasMaxLength(2000);
        modelBuilder.Entity<RoleChangeRequest>().Property(r => r.RejectionReason).HasMaxLength(2000);
        modelBuilder.Entity<RoleChangeRequest>().Property(r => r.Status).IsConcurrencyToken();
        modelBuilder.Entity<RoleChangeRequest>().HasIndex(r => r.UserId).IsUnique().HasFilter("[Status] = 0");
        modelBuilder.Entity<RoleChangeRequest>().HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RoleChangeRequest>().HasOne<User>().WithMany().HasForeignKey(r => r.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserAccessNotification>().Property(n => n.EventKey).HasMaxLength(150);
        modelBuilder.Entity<UserAccessNotification>().Property(n => n.Kind).HasMaxLength(30);
        modelBuilder.Entity<UserAccessNotification>().Property(n => n.Title).HasMaxLength(200);
        modelBuilder.Entity<UserAccessNotification>().Property(n => n.Message).HasMaxLength(2500);
        modelBuilder.Entity<UserAccessNotification>().HasIndex(n => new {n.RecipientId, n.EventKey}).IsUnique();
        modelBuilder.Entity<UserAccessNotification>().HasOne<User>().WithMany().HasForeignKey(n => n.RecipientId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AuditLog>().Property(x => x.Comments).HasMaxLength(4000);
        modelBuilder.Entity<AuditLog>().HasIndex(x => new { x.EntityId, x.OccurredAt });
        var scanEvent = modelBuilder.Entity<EquipmentScanEvent>();
        scanEvent.Property(x => x.Identifier).HasMaxLength(200).IsRequired();
        scanEvent.Property(x => x.Technology).HasMaxLength(20).IsRequired();
        scanEvent.Property(x => x.MatchResult).HasMaxLength(20).IsRequired();
        scanEvent.Property(x => x.Discrepancy).HasMaxLength(1000);
        scanEvent.HasIndex(x => x.ClientEventId).IsUnique();
        scanEvent.HasIndex(x => new { x.ProjectId, x.AreaId, x.ScannedAt });
        scanEvent.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        scanEvent.HasOne<ProjectArea>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        scanEvent.HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        scanEvent.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<User>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ApprovalReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RegistrationOtp>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.Email).HasMaxLength(320);
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.DisplayName).HasMaxLength(200);
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.Otp).HasMaxLength(4).IsFixedLength();
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.VerificationAttempts).HasDefaultValue(0);
        modelBuilder.Entity<RegistrationOtp>().Property(x => x.ConcurrencyVersion).HasDefaultValue(0).IsConcurrencyToken();
        modelBuilder.Entity<Project>().Property(x => x.ProjectName).HasMaxLength(200);
        modelBuilder.Entity<Project>().Property(x => x.TargetDate).IsConcurrencyToken();
        modelBuilder.Entity<ProjectScheduleEntry>(b =>
        {
            b.Property(x => x.Kind).HasMaxLength(24);
            b.Property(x => x.EventKey).HasMaxLength(100);
            b.Property(x => x.DelayReason).HasMaxLength(2000);
            b.HasIndex(x => new { x.ProjectId, x.EventKey }).IsUnique();
            b.HasIndex(x => new { x.ProjectId, x.Kind, x.OccurredAt });
            b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ProjectDelayEmail>(b =>
        {
            b.Property(x => x.Status).HasMaxLength(24);
            b.HasIndex(x => new { x.ScheduleEntryId, x.RecipientId }).IsUnique();
            b.HasIndex(x => x.Status);
            b.HasOne<ProjectScheduleEntry>().WithMany().HasForeignKey(x => x.ScheduleEntryId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Project>().Property(x => x.Sector).HasMaxLength(150);
        modelBuilder.Entity<Project>().Property(x => x.JobNo).HasMaxLength(100);
        modelBuilder.Entity<Project>().Property(x => x.Location).HasMaxLength(300);
        modelBuilder.Entity<Project>().Property(x => x.VesselOrSiteName).HasMaxLength(200);
        modelBuilder.Entity<Project>().Property(x => x.HazardousAreaPlanPath).HasMaxLength(500);
        modelBuilder.Entity<Project>().Property(x => x.HazardousAreaPlanFileName).HasMaxLength(255);
        modelBuilder.Entity<Project>().Property(x => x.UidNumber).HasMaxLength(100);
        modelBuilder.Entity<Project>().Property(x => x.DocumentNumber).HasMaxLength(200);
        modelBuilder.Entity<Project>().Property(x => x.Revision).HasMaxLength(100);
        modelBuilder.Entity<Project>().Property(x => x.Description).HasMaxLength(4000);
        modelBuilder.Entity<Project>().Property(x => x.ClientName).HasMaxLength(200);
        modelBuilder.Entity<Project>().Property(x => x.ClientLogoPath).HasMaxLength(500);
        modelBuilder.Entity<Project>().HasIndex(x => x.JobNo).IsUnique();
        var company = modelBuilder.Entity<CompanyMaster>();
        company.Property(c => c.CompanyName).HasMaxLength(300); company.Property(c => c.DisplayName).HasMaxLength(300);
        company.Property(c => c.Address).HasMaxLength(2000); company.Property(c => c.Telephone).HasMaxLength(150);
        company.Property(c => c.Fax).HasMaxLength(150); company.Property(c => c.Website).HasMaxLength(300);
        company.Property(c => c.Email).HasMaxLength(500); company.Property(c => c.LogoPath).HasMaxLength(255);
        company.HasIndex(c => c.DisplayName).IsUnique();
        company.HasData(Ex_SurveyApp_api.Services.CompanyAssets.Seed());
        modelBuilder.Entity<Project>().HasOne(p => p.OurCompany).WithMany().HasForeignKey(p => p.OurCompanyId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Project>().HasOne<User>().WithMany().HasForeignKey(x => x.AdditionalSurveyorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Project>().Property(x => x.AssignedSurveyorId).IsConcurrencyToken();
        modelBuilder.Entity<Project>().Property(x => x.AdditionalSurveyorId).IsConcurrencyToken();
        modelBuilder.Entity<Project>().HasOne<User>().WithMany().HasForeignKey(x => x.AssignedSurveyorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Project>().HasOne<User>().WithMany().HasForeignKey(x => x.AdditionalQcId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Project>().Property(x => x.AdditionalQcId).IsConcurrencyToken();
        modelBuilder.Entity<Project>().Property(x => x.AssignedQcId).IsConcurrencyToken();
        modelBuilder.Entity<Project>().HasOne(x => x.AssignedQc).WithMany().HasForeignKey(x => x.AssignedQcId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Project>().HasOne<User>().WithMany().HasForeignKey(x => x.AssignedClientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProjectClientAssignment>().HasKey(x => new { x.ProjectId, x.ClientUserId });
        modelBuilder.Entity<ProjectClientAssignment>().HasOne(x => x.Project).WithMany(p => p.ClientAssignments).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectClientAssignment>().HasOne(x => x.ClientUser).WithMany().HasForeignKey(x => x.ClientUserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Project>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProjectArea>().Property(x => x.AreaName).HasMaxLength(200);
        modelBuilder.Entity<ProjectArea>().Property(x => x.LayoutImagePath).HasMaxLength(500);
        modelBuilder.Entity<ProjectArea>().HasIndex(x => new { x.ProjectId, x.AreaName }).IsUnique();
        modelBuilder.Entity<ProjectArea>().HasOne<Project>().WithMany(x => x.Areas).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectAreaPhoto>().Property(x => x.FileName).HasMaxLength(255);
        modelBuilder.Entity<ProjectAreaPhoto>().Property(x => x.FilePath).HasMaxLength(500);
        modelBuilder.Entity<ProjectAreaPhoto>().HasOne<ProjectArea>().WithMany(x => x.Photos).HasForeignKey(x => x.ProjectAreaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectAreaPhoto>().HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.XPercent).HasPrecision(5, 2);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.YPercent).HasPrecision(5, 2);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.MarkingNo);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.IsPlacedOnLayout).HasDefaultValue(true);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.LayoutImageKey).HasMaxLength(80);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.TagNumber).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.EquipmentName).HasMaxLength(250);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.EquipmentCode).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.RfidTagUid).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.Type).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.CircuitId).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.Location).HasMaxLength(300);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.Manufacturer).HasMaxLength(200);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.ProtectionDetails).HasMaxLength(500);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.CertificateNumber).HasMaxLength(150);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.Description).HasMaxLength(1000);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.Model).HasMaxLength(200);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.SerialNumber).HasMaxLength(150);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.ExProtectionType).HasMaxLength(150);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.GasGroup).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.TemperatureClass).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.HazardousAreaZone).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.AmbientTemperatureRange).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.IpRating).HasMaxLength(50);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.NotifiedBody).HasMaxLength(250);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.SpecialConditionsOfUse).HasMaxLength(2000);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.RfidIdentificationMethod).HasMaxLength(100);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.NotApplicableFieldsJson).HasMaxLength(2000);
        modelBuilder.Entity<AreaEquipment>().Property(x => x.ReconciliationStatus).HasMaxLength(50).HasDefaultValue("Unreviewed");
        modelBuilder.Entity<AreaEquipment>().Property(x => x.RegistrationStatus).HasMaxLength(30).HasDefaultValue("Verified");
        modelBuilder.Entity<ChecklistTemplate>().Property(x => x.DraftRevision).HasDefaultValue(1);
        modelBuilder.Entity<ChecklistTemplate>().Property(x => x.LifecycleStatus).HasDefaultValue(ChecklistLifecycleStatus.Draft);
        modelBuilder.Entity<ChecklistTemplate>().Property(x => x.StandardReference).HasMaxLength(100);
        modelBuilder.Entity<ChecklistTemplate>().Property(x => x.StandardEdition).HasMaxLength(50);
        modelBuilder.Entity<ChecklistTemplate>().Property(x => x.ControlledSourceReference).HasMaxLength(500);
        modelBuilder.Entity<ChecklistTemplate>().Property(x => x.ApplicableProtectionTypes).HasMaxLength(1000);
        modelBuilder.Entity<ChecklistTemplate>().HasIndex(x => new {x.FamilyId, x.Version}).IsUnique().HasFilter("[FamilyId] IS NOT NULL");
        modelBuilder.Entity<ChecklistQuestion>().Property(x => x.SectionCode).HasMaxLength(50);
        modelBuilder.Entity<ChecklistQuestion>().Property(x => x.ApplicableProtectionTypes).HasMaxLength(1000);
        modelBuilder.Entity<ChecklistQuestion>().Property(x => x.ResponseScaleCode).HasMaxLength(50);
        modelBuilder.Entity<ChecklistQuestion>().Property(x => x.SourceItemReference).HasMaxLength(200);
        modelBuilder.Entity<ChecklistQuestion>().Property(x => x.ApplicableGrades).HasMaxLength(100);
        modelBuilder.Entity<ChecklistQuestion>().Property(x => x.SubChecksJson).HasMaxLength(4000);
        modelBuilder.Entity<ChecklistQuestion>().HasIndex(x => new { x.ChecklistTemplateId, x.SourceItemReference })
            .IsUnique().HasFilter("[SourceItemReference] IS NOT NULL");
        modelBuilder.Entity<ChecklistTemplate>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChecklistTemplate>().HasOne<User>().WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChecklistTemplate>().HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AreaEquipment>().HasIndex(x => new { x.AreaId, x.SequenceNo }).IsUnique();
        modelBuilder.Entity<AreaEquipment>().HasIndex(x => new { x.AreaId, x.MarkingNo }).IsUnique()
            .HasFilter("[MarkingNo] IS NOT NULL");
        modelBuilder.Entity<AreaEquipment>().HasIndex(x => new { x.AreaId, x.TagNumber }).IsUnique();
        modelBuilder.Entity<AreaEquipment>().HasIndex(x => x.RfidTagUid).IsUnique()
            .HasFilter("[RfidTagUid] IS NOT NULL");
        modelBuilder.Entity<EquipmentTagAssignment>().Property(x => x.Technology).HasMaxLength(30);
        modelBuilder.Entity<EquipmentTagAssignment>().Property(x => x.NormalizedIdentifier).HasMaxLength(200);
        modelBuilder.Entity<EquipmentTagAssignment>().Property(x => x.AssignmentReason).HasMaxLength(1000);
        modelBuilder.Entity<EquipmentTagAssignment>().Property(x => x.DeactivationReason).HasMaxLength(1000);
        modelBuilder.Entity<EquipmentTagAssignment>().HasIndex(x => x.NormalizedIdentifier).IsUnique().HasFilter("[IsActive] = 1");
        modelBuilder.Entity<EquipmentTagAssignment>().HasIndex(x => new {x.EquipmentId, x.Technology}).IsUnique().HasFilter("[IsActive] = 1");
        modelBuilder.Entity<EquipmentTagAssignment>().HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EquipmentTagAssignment>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EquipmentTagAssignment>().HasOne<User>().WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EquipmentTagAssignment>().HasOne<User>().WithMany().HasForeignKey(x => x.DeactivatedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EquipmentTagHistory>().Property(x => x.Technology).HasMaxLength(30);
        modelBuilder.Entity<EquipmentTagHistory>().Property(x => x.NormalizedIdentifier).HasMaxLength(200);
        modelBuilder.Entity<EquipmentTagHistory>().Property(x => x.ActionCode).HasMaxLength(40);
        modelBuilder.Entity<EquipmentTagHistory>().Property(x => x.Reason).HasMaxLength(1000);
        modelBuilder.Entity<EquipmentTagHistory>().HasIndex(x => new {x.EquipmentId, x.OccurredAt});
        modelBuilder.Entity<EquipmentTagHistory>().HasIndex(x => x.AssignmentId);
        modelBuilder.Entity<EquipmentTagHistory>().HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EquipmentTagHistory>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EquipmentTagHistory>().HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AreaEquipment>().HasOne<Project>().WithMany(x => x.EquipmentPins).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AreaEquipment>().HasOne<ProjectArea>().WithMany(x => x.EquipmentPins).HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AreaEquipment>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().Property(x => x.EquipmentPhotoRole).HasMaxLength(40);
        modelBuilder.Entity<SurveyFile>().HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.AreaEquipmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasIndex(x => new { x.ProjectId, x.AreaEquipmentId, x.UploadedAt });
        modelBuilder.Entity<Job>().HasIndex(x => x.JobNumber).IsUnique();
        modelBuilder.Entity<Job>().HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JobAssignment>().HasIndex(x => new { x.JobId, x.UserId, x.Role }).IsUnique();
        modelBuilder.Entity<JobAssignment>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
        modelBuilder.Entity<JobAssignment>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Area>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
        modelBuilder.Entity<Equipment>().HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId);
        modelBuilder.Entity<Equipment>().HasIndex(x => new { x.AreaId, x.TagNumber }).IsUnique();
        modelBuilder.Entity<ChecklistQuestion>().HasOne<ChecklistTemplate>().WithMany().HasForeignKey(x => x.ChecklistTemplateId);
        modelBuilder.Entity<SurveySession>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.AreaEquipmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasOne<User>().WithMany().HasForeignKey(x => x.SurveyorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasOne<ChecklistTemplate>().WithMany().HasForeignKey(x => x.ChecklistTemplateId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveySession>().HasIndex(x => x.AreaEquipmentId).IsUnique().HasFilter("[AreaEquipmentId] IS NOT NULL");
        modelBuilder.Entity<SurveySession>().Property(x => x.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<SurveySession>().Property(x => x.InspectorName).HasMaxLength(200);
        modelBuilder.Entity<SurveySession>().Property(x => x.SurveyorRemarks).HasMaxLength(2000);
        modelBuilder.Entity<SurveyAnswer>().HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId);
        modelBuilder.Entity<SurveyAnswer>().HasOne<ChecklistQuestion>().WithMany().HasForeignKey(x => x.ChecklistQuestionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChecklistQuestion>().Property(x => x.MeasurementUnit).HasMaxLength(40);
        modelBuilder.Entity<ChecklistQuestion>().Property(x => x.MinimumReading).HasPrecision(18, 4);
        modelBuilder.Entity<ChecklistQuestion>().Property(x => x.MaximumReading).HasPrecision(18, 4);
        modelBuilder.Entity<SurveyAnswer>().HasIndex(x => new { x.SurveySessionId, x.ChecklistQuestionId }).IsUnique();
        var finding = modelBuilder.Entity<Finding>();
        finding.HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId).OnDelete(DeleteBehavior.Cascade);
        finding.HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.RegisterEquipmentId).OnDelete(DeleteBehavior.Restrict);
        finding.HasOne<User>().WithMany().HasForeignKey(x => x.RegisterAuthorId).OnDelete(DeleteBehavior.Restrict);
        finding.Property(x => x.AosdId).HasMaxLength(100);
        finding.Property(x => x.FaultCategory).HasMaxLength(1);
        finding.Property(x => x.FaultCode).HasMaxLength(100);
        finding.Property(x => x.Remarks).HasMaxLength(2000);
        finding.Property(x => x.AdditionalRemarks).HasMaxLength(2000);
        modelBuilder.Entity<QcReview>().HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId);
        modelBuilder.Entity<QcReview>().HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QcReview>().HasIndex(x => x.SurveySessionId).IsUnique();
        modelBuilder.Entity<QcReview>().Property(x => x.Comments).HasMaxLength(4000);
        modelBuilder.Entity<SurveyFile>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<SurveySession>().WithMany().HasForeignKey(x => x.SurveySessionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SurveyFile>().HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<Finding>().WithMany().HasForeignKey(x => x.FindingId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyFile>().HasOne<User>().WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReportRevision>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
        modelBuilder.Entity<ReportRevision>().HasOne<SurveyFile>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReportRevision>().HasIndex(x => new { x.JobId, x.Revision }).IsUnique();
        modelBuilder.Entity<AuditLog>().HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SyncBatch>().HasOne<User>().WithMany().HasForeignKey(x => x.SurveyorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SyncBatch>().HasIndex(x => new { x.SurveyorId, x.ClientBatchId }).IsUnique();

        // Additive Phase 2A metadata only. No existing row receives tenant ownership and no
        // existing Project/Area/Equipment column, key, or relationship is changed here.
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new ContractConfiguration());
        modelBuilder.ApplyConfiguration(new TenantProjectBindingConfiguration());
        modelBuilder.ApplyConfiguration(new SiteConfiguration());
        modelBuilder.ApplyConfiguration(new TenantMembershipConfiguration());
        modelBuilder.ApplyConfiguration(new TenantRoleGrantConfiguration());
        modelBuilder.ApplyConfiguration(new LegacyEntityMapConfiguration());
        modelBuilder.ApplyConfiguration(new TenantBackfillExceptionConfiguration());
        modelBuilder.ApplyConfiguration(new InspectionActivityConfiguration());
        modelBuilder.ApplyConfiguration(new InspectionTaskConfiguration());
        modelBuilder.ApplyConfiguration(new InspectionActivityEntityConfiguration());
        modelBuilder.ApplyConfiguration(new InspectionActivityRevisionConfiguration());
        modelBuilder.ApplyConfiguration(new ChecklistSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new CheckpointDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new CheckpointResponseConfiguration());
        modelBuilder.ApplyConfiguration(new ActivityReviewEventConfiguration());
        modelBuilder.ApplyConfiguration(new ActivityFindingConfiguration());
        modelBuilder.ApplyConfiguration(new CorrectiveActionConfiguration());
        modelBuilder.ApplyConfiguration(new CorrectiveActionEventConfiguration());
        modelBuilder.ApplyConfiguration(new ActivityEvidenceConfiguration());
        modelBuilder.ApplyConfiguration(new CheckpointReviewEventConfiguration());
        modelBuilder.ApplyConfiguration(new InspectionCampaignConfiguration());
        modelBuilder.ApplyConfiguration(new CampaignAnnualListConfiguration());
        modelBuilder.ApplyConfiguration(new CampaignEquipmentConfiguration());
        modelBuilder.ApplyConfiguration(new SamplingApprovalConfiguration());
        modelBuilder.ApplyConfiguration(new InspectionWorkPackageConfiguration());
        modelBuilder.ApplyConfiguration(new InspectionTaskAssignmentEventConfiguration());
        modelBuilder.ApplyConfiguration(new PlanningApprovalEventConfiguration());
        modelBuilder.Entity<InspectionRecurrence>().ToTable("InspectionRecurrences");
        modelBuilder.Entity<InspectionRecurrence>().HasKey(x=>x.Id);
        modelBuilder.Entity<InspectionRecurrence>().HasIndex(x=>new{x.CampaignId,x.AnnualListId,x.Grade,x.NextOccurrenceOn});
        modelBuilder.Entity<InspectionRecurrence>().HasOne<InspectionCampaign>().WithMany().HasForeignKey(x=>x.CampaignId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InspectionRecurrence>().HasOne<CampaignAnnualList>().WithMany().HasForeignKey(x=>x.AnnualListId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OfflineSyncReceipt>().ToTable("OfflineSyncReceipts");
        modelBuilder.Entity<OfflineSyncReceipt>().HasKey(x=>x.Id);
        modelBuilder.Entity<OfflineSyncReceipt>().Property(x=>x.IdempotencyKey).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<OfflineSyncReceipt>().Property(x=>x.PayloadHash).HasMaxLength(64).IsRequired();
        modelBuilder.Entity<OfflineSyncReceipt>().Property(x=>x.ResponseMapJson).HasMaxLength(200000).IsRequired();
        modelBuilder.Entity<OfflineSyncReceipt>().HasIndex(x=>new{x.UserId,x.IdempotencyKey}).IsUnique();
        modelBuilder.Entity<OfflineSyncReceipt>().HasIndex(x=>new{x.UserId,x.ClientActivityId}).IsUnique();
        modelBuilder.Entity<OfflineSyncReceipt>().HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OfflineSyncReceipt>().HasOne<InspectionActivity>().WithMany().HasForeignKey(x=>x.ServerActivityId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InspectionActivityDocumentUsage>().ToTable("InspectionActivityDocumentUsages");
        modelBuilder.Entity<InspectionActivityDocumentUsage>().HasKey(x=>x.Id);
        modelBuilder.Entity<InspectionActivityDocumentUsage>().Property(x=>x.DocumentIdentity).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<InspectionActivityDocumentUsage>().Property(x=>x.DocumentRevision).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<InspectionActivityDocumentUsage>().Property(x=>x.Sha256).HasMaxLength(64).IsRequired();
        modelBuilder.Entity<InspectionActivityDocumentUsage>().Property(x=>x.Title).HasMaxLength(255).IsRequired();
        modelBuilder.Entity<InspectionActivityDocumentUsage>().Property(x=>x.Scope).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<InspectionActivityDocumentUsage>().HasIndex(x=>new{x.InspectionActivityId,x.DocumentIdentity,x.DocumentRevision}).IsUnique();
        modelBuilder.Entity<InspectionActivityDocumentUsage>().HasOne<InspectionActivity>().WithMany().HasForeignKey(x=>x.InspectionActivityId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InspectionActivityDocumentUsage>().HasOne<InspectionTask>().WithMany().HasForeignKey(x=>x.InspectionTaskId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InspectionActivityDocumentUsage>().HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OfflinePhotoSyncReceipt>().ToTable("OfflinePhotoSyncReceipts");
        modelBuilder.Entity<OfflinePhotoSyncReceipt>().HasKey(x=>x.Id);
        modelBuilder.Entity<OfflinePhotoSyncReceipt>().Property(x=>x.IdempotencyKey).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<OfflinePhotoSyncReceipt>().Property(x=>x.ContentHash).HasMaxLength(64).IsRequired();
        modelBuilder.Entity<OfflinePhotoSyncReceipt>().HasIndex(x=>new{x.UserId,x.IdempotencyKey}).IsUnique();
        modelBuilder.Entity<OfflinePhotoSyncReceipt>().HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OfflinePhotoSyncReceipt>().HasOne<ActivityEvidence>().WithMany().HasForeignKey(x=>x.EvidenceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OfflinePhotoSyncReceipt>().HasOne<InspectionActivity>().WithMany().HasForeignKey(x=>x.ActivityId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OfflinePhotoSyncReceipt>().HasOne<CheckpointResponseRevision>().WithMany().HasForeignKey(x=>x.CheckpointResponseRevisionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InspectionTask>().HasOne<InspectionWorkPackage>().WithMany().HasForeignKey(x => x.WorkPackageId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InspectionTask>().HasOne<ChecklistVersionSnapshot>().WithMany().HasForeignKey(x => x.ChecklistVersionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InspectionTask>().Property(x => x.RequiredPermits).HasMaxLength(2000);
        modelBuilder.Entity<InspectionTask>().Property(x => x.IsolationPrerequisites).HasMaxLength(2000);
        modelBuilder.Entity<InspectionTask>().Property(x => x.CarryForwardReason).HasMaxLength(1000);
        modelBuilder.Entity<InspectionTask>().HasIndex(x => new { x.WorkPackageId, x.RouteOrder });
        var workflow = modelBuilder.Entity<ProjectReportWorkflow>();
        workflow.HasKey(x => x.ProjectId);
        workflow.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Ongoing").IsRequired();
        workflow.Property(x => x.HoldReason).HasMaxLength(2000);
        workflow.Property(x => x.Version).IsConcurrencyToken();
        workflow.HasOne<Project>().WithOne().HasForeignKey<ProjectReportWorkflow>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        workflow.HasOne<GeneratedReportArtifact>().WithMany().HasForeignKey(x => x.SubmittedArtifactId).OnDelete(DeleteBehavior.NoAction);
        var reportEvent = modelBuilder.Entity<ReportWorkflowEvent>();
        reportEvent.HasKey(x => x.Id);
        reportEvent.Property(x => x.FromStatus).HasMaxLength(20).IsRequired();
        reportEvent.Property(x => x.ToStatus).HasMaxLength(20).IsRequired();
        reportEvent.Property(x => x.Reason).HasMaxLength(2000);
        reportEvent.HasIndex(x => new { x.ProjectId, x.Version }).IsUnique();
        reportEvent.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        reportEvent.HasOne<GeneratedReportArtifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.NoAction);
        var artifact=modelBuilder.Entity<GeneratedReportArtifact>();
        artifact.ToTable("GeneratedReportArtifacts"); artifact.HasKey(x=>x.Id);
        artifact.Property(x=>x.ReportType).HasMaxLength(40).IsRequired();
        artifact.Property(x=>x.ReportNumber).HasMaxLength(160).IsRequired();
        artifact.Property(x => x.UidNumber).HasMaxLength(100);
        artifact.Property(x => x.DocumentNumber).HasMaxLength(200);
        artifact.Property(x => x.DocumentRevision).HasMaxLength(100);
        artifact.Property(x=>x.FileName).HasMaxLength(255).IsRequired();
        artifact.Property(x=>x.ContentType).HasMaxLength(150).IsRequired();
        artifact.Property(x=>x.StorageKey).HasMaxLength(1000).IsRequired();
        artifact.Property(x=>x.Sha256).HasMaxLength(64).IsRequired();
        artifact.Property(x=>x.ApprovalState).HasMaxLength(30).IsRequired();
        artifact.HasIndex(x=>new{x.ProjectId,x.ReportType,x.LinkedEntityId,x.Revision}).IsUnique();
        artifact.HasIndex(x=>new{x.ProjectId,x.GeneratedAt});
        artifact.HasOne<User>().WithMany().HasForeignKey(x=>x.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
