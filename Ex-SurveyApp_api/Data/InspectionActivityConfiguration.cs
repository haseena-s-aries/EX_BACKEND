using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ex_SurveyApp_api.Data;

public sealed class InspectionActivityConfiguration : IEntityTypeConfiguration<InspectionPlan>
{
    public void Configure(EntityTypeBuilder<InspectionPlan> b)
    {
        b.ToTable("InspectionPlans"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.SourceReference).HasMaxLength(200); b.HasIndex(x => x.ProjectId);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany<InspectionTask>().WithOne().HasForeignKey(x => x.InspectionPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InspectionTaskConfiguration : IEntityTypeConfiguration<InspectionTask>
{
    public void Configure(EntityTypeBuilder<InspectionTask> b)
    {
        b.ToTable("InspectionTasks", t => t.HasCheckConstraint("CK_InspectionTasks_OneEquipment", "([AreaEquipmentId] IS NOT NULL AND [LegacyEquipmentId] IS NULL) OR ([AreaEquipmentId] IS NULL AND [LegacyEquipmentId] IS NOT NULL)"));
        b.HasKey(x => x.Id); b.Property(x => x.ScheduleReference).HasMaxLength(200); b.Property(x => x.WorkPackageReference).HasMaxLength(200); b.Property(x => x.StatusCode).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.AreaEquipmentId); b.HasIndex(x => x.LegacyEquipmentId); b.HasIndex(x => new { x.InspectionPlanId, x.ScheduledAt });
        b.HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.AreaEquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Equipment>().WithMany().HasForeignKey(x => x.LegacyEquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany<InspectionActivity>().WithOne().HasForeignKey(x => x.InspectionTaskId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InspectionActivityEntityConfiguration : IEntityTypeConfiguration<InspectionActivity>
{
    public void Configure(EntityTypeBuilder<InspectionActivity> b)
    {
        b.ToTable("InspectionActivities"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.InspectorNameSnapshot).HasMaxLength(200); b.HasIndex(x => x.InspectionTaskId); b.HasIndex(x => x.LegacySurveySessionId).IsUnique().HasFilter("[LegacySurveySessionId] IS NOT NULL");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.InspectorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SurveySession>().WithMany().HasForeignKey(x => x.LegacySurveySessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ChecklistVersionSnapshot>().WithMany().HasForeignKey(x => x.ChecklistVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Revisions).WithOne(x => x.Activity).HasForeignKey(x => x.InspectionActivityId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany<ActivityReviewEvent>().WithOne().HasForeignKey(x => x.InspectionActivityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InspectionActivityRevisionConfiguration : IEntityTypeConfiguration<InspectionActivityRevision>
{
    public void Configure(EntityTypeBuilder<InspectionActivityRevision> b)
    {
        b.ToTable("InspectionActivityRevisions"); b.HasKey(x => x.Id); b.Property(x => x.ContentHash).HasMaxLength(128).IsRequired(); b.Property(x => x.ChangeReason).HasMaxLength(1000);
        b.HasIndex(x => new { x.InspectionActivityId, x.RevisionNumber }).IsUnique(); b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany<CheckpointResponseRevision>().WithOne().HasForeignKey(x => x.ActivityRevisionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ChecklistSnapshotConfiguration : IEntityTypeConfiguration<ChecklistVersionSnapshot>
{
    public void Configure(EntityTypeBuilder<ChecklistVersionSnapshot> b)
    {
        b.ToTable("ChecklistVersionSnapshots"); b.HasKey(x => x.Id); b.Property(x => x.SnapshotHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.StandardReference).HasMaxLength(100); b.Property(x => x.StandardEdition).HasMaxLength(50); b.Property(x => x.ControlledSourceReference).HasMaxLength(500); b.Property(x => x.ProtectionTypeSnapshot).HasMaxLength(150);
        b.HasIndex(x => new { x.SourceChecklistTemplateId, x.VersionNumber, x.ProtectionTypeSnapshot }).IsUnique(); b.HasOne<ChecklistTemplate>().WithMany().HasForeignKey(x => x.SourceChecklistTemplateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany<CheckpointDefinitionSnapshot>().WithOne().HasForeignKey(x => x.ChecklistVersionSnapshotId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CheckpointDefinitionConfiguration : IEntityTypeConfiguration<CheckpointDefinitionSnapshot>
{
    public void Configure(EntityTypeBuilder<CheckpointDefinitionSnapshot> b)
    {
        b.ToTable("CheckpointDefinitionSnapshots"); b.HasKey(x => x.Id); b.Property(x => x.QuestionCode).HasMaxLength(100).IsRequired(); b.Property(x => x.QuestionText).HasMaxLength(4000).IsRequired();
        b.Property(x => x.ApplicabilityCode).HasMaxLength(100); b.Property(x => x.ResponseScaleCode).HasMaxLength(100);
        b.Property(x => x.SectionCode).HasMaxLength(50);
        b.Property(x => x.MeasurementUnit).HasMaxLength(40); b.Property(x => x.MinimumReading).HasPrecision(18, 4); b.Property(x => x.MaximumReading).HasPrecision(18, 4);
        b.HasIndex(x => new { x.ChecklistVersionSnapshotId, x.QuestionCode, x.QuestionVersion }).IsUnique();
    }
}

public sealed class CheckpointResponseConfiguration : IEntityTypeConfiguration<CheckpointResponseRevision>
{
    public void Configure(EntityTypeBuilder<CheckpointResponseRevision> b)
    {
        b.ToTable("CheckpointResponseRevisions"); b.HasKey(x => x.Id);
        b.Property(x => x.ApplicabilityResultCode).HasMaxLength(100); b.Property(x => x.ResponseCode).HasMaxLength(100); b.Property(x => x.TechnicalConditionCode).HasMaxLength(100); b.Property(x => x.Observation).HasMaxLength(4000); b.Property(x => x.ReviewComment).HasMaxLength(4000); b.Property(x => x.NumericValue).HasPrecision(18, 4); b.Property(x => x.MeasurementUnitSnapshot).HasMaxLength(40);
        b.HasIndex(x => new { x.ActivityRevisionId, x.CheckpointDefinitionSnapshotId, x.RevisionNumber }).IsUnique();
        b.HasOne<CheckpointDefinitionSnapshot>().WithMany().HasForeignKey(x => x.CheckpointDefinitionSnapshotId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany<ActivityFinding>().WithOne().HasForeignKey(x => x.CheckpointResponseRevisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany<ActivityEvidence>().WithOne().HasForeignKey(x => x.CheckpointResponseRevisionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ActivityReviewEventConfiguration : IEntityTypeConfiguration<ActivityReviewEvent>
{
    public void Configure(EntityTypeBuilder<ActivityReviewEvent> b)
    {
        b.ToTable("ActivityReviewEvents"); b.HasKey(x => x.Id); b.Property(x => x.Comments).HasMaxLength(4000); b.HasIndex(x => new { x.InspectionActivityId, x.DecidedAt });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ActivityFindingConfiguration : IEntityTypeConfiguration<ActivityFinding>
{
    public void Configure(EntityTypeBuilder<ActivityFinding> b)
    {
        b.ToTable("ActivityFindings"); b.HasKey(x => x.Id); b.Property(x => x.Observation).HasMaxLength(4000).IsRequired(); b.Property(x => x.ProposedAction).HasMaxLength(4000); b.Property(x => x.SeverityCode).HasMaxLength(100); b.Property(x => x.StatusCode).HasMaxLength(40).IsRequired(); b.Property(x => x.FaultCode).HasMaxLength(100); b.Property(x => x.RequiredMaterialsOrSupport).HasMaxLength(2000); b.Property(x => x.ResponsibleParty).HasMaxLength(200); b.Property(x => x.ReportedAt).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        b.HasIndex(x => x.LegacyFindingId).IsUnique().HasFilter("[LegacyFindingId] IS NOT NULL"); b.HasIndex(x => x.InspectionActivityId);
        b.HasOne<Finding>().WithMany().HasForeignKey(x => x.LegacyFindingId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany<CorrectiveAction>().WithOne().HasForeignKey(x => x.ActivityFindingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InspectionActivity>().WithMany().HasForeignKey(x => x.InspectionActivityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CorrectiveActionConfiguration : IEntityTypeConfiguration<CorrectiveAction>
{
    public void Configure(EntityTypeBuilder<CorrectiveAction> b)
    {
        b.ToTable("CorrectiveActions"); b.HasKey(x => x.Id); b.Property(x => x.ActionReference).HasMaxLength(100).IsRequired(); b.Property(x => x.SupplementaryInformation).HasMaxLength(4000); b.Property(x => x.ProposedAction).HasMaxLength(4000); b.Property(x => x.RectificationDescription).HasMaxLength(4000); b.Property(x => x.ExternalWorkOrderReference).HasMaxLength(200); b.Property(x => x.ResponsibleParty).HasMaxLength(200); b.Property(x => x.LastDecisionComment).HasMaxLength(4000); b.Property(x => x.IsOutstanding).HasDefaultValue(true);
        b.HasIndex(x => x.ActionReference).IsUnique(); b.HasIndex(x => new { x.ActivityFindingId, x.CreatedAt });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.AriesVerifierUserId).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.ClientDecisionUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany<CorrectiveActionEvent>().WithOne().HasForeignKey(x => x.CorrectiveActionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CorrectiveActionEventConfiguration : IEntityTypeConfiguration<CorrectiveActionEvent>
{
    public void Configure(EntityTypeBuilder<CorrectiveActionEvent> b)
    {
        b.ToTable("CorrectiveActionEvents"); b.HasKey(x => x.Id); b.Property(x => x.Comment).HasMaxLength(4000); b.Property(x => x.RectificationDescription).HasMaxLength(4000); b.Property(x => x.WorkOrderReference).HasMaxLength(200); b.Property(x => x.ResponsibleParty).HasMaxLength(200); b.HasIndex(x => new { x.CorrectiveActionId, x.OccurredAt });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany<ActivityEvidence>().WithOne().HasForeignKey(x => x.CorrectiveActionEventId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ActivityEvidenceConfiguration : IEntityTypeConfiguration<ActivityEvidence>
{
    public void Configure(EntityTypeBuilder<ActivityEvidence> b)
    {
        b.ToTable("ActivityEvidence"); b.HasKey(x => x.Id); b.Property(x => x.Caption).HasMaxLength(1000); b.Property(x => x.IntegrityHash).HasMaxLength(128); b.HasIndex(x => x.SurveyFileId); b.HasIndex(x => new { x.InspectionActivityId, x.AddedAt });
        b.HasOne<SurveyFile>().WithMany().HasForeignKey(x => x.SurveyFileId).OnDelete(DeleteBehavior.Restrict); b.HasOne<User>().WithMany().HasForeignKey(x => x.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InspectionActivity>().WithMany().HasForeignKey(x => x.InspectionActivityId).OnDelete(DeleteBehavior.Restrict); b.HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.AreaEquipmentId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Equipment>().WithMany().HasForeignKey(x => x.LegacyEquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CheckpointReviewEventConfiguration : IEntityTypeConfiguration<CheckpointReviewEvent>
{
    public void Configure(EntityTypeBuilder<CheckpointReviewEvent> b)
    {
        b.ToTable("CheckpointReviewEvents"); b.HasKey(x => x.Id); b.Property(x => x.Comments).HasMaxLength(4000);
        b.HasIndex(x => new { x.InspectionActivityId, x.ReviewedAt });
        b.HasOne<InspectionActivity>().WithMany().HasForeignKey(x => x.InspectionActivityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CheckpointResponseRevision>().WithMany().HasForeignKey(x => x.CheckpointResponseRevisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
