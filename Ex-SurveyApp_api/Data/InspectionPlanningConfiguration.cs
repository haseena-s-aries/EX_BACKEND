using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ex_SurveyApp_api.Data;

public sealed class InspectionCampaignConfiguration : IEntityTypeConfiguration<InspectionCampaign>
{
    public void Configure(EntityTypeBuilder<InspectionCampaign> b)
    {
        b.ToTable("InspectionCampaigns"); b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.Reference).HasMaxLength(100).IsRequired();
        b.Property(x => x.ClosePercent).HasPrecision(5, 2); b.Property(x => x.DetailedPercent).HasPrecision(5, 2); b.Property(x => x.MinimumDetailedCreditPercent).HasPrecision(5, 2);
        b.HasIndex(x => new { x.ProjectId, x.Reference }).IsUnique(); b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.InspectionLeadUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CampaignAnnualListConfiguration : IEntityTypeConfiguration<CampaignAnnualList>
{
    public void Configure(EntityTypeBuilder<CampaignAnnualList> b)
    {
        b.ToTable("CampaignAnnualLists"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.CampaignId, x.Year }).IsUnique();
        b.HasOne<InspectionCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CampaignEquipmentConfiguration : IEntityTypeConfiguration<CampaignEquipment>
{
    public void Configure(EntityTypeBuilder<CampaignEquipment> b)
    {
        b.ToTable("CampaignEquipment"); b.HasKey(x => x.Id); b.Property(x => x.RiskScore).HasPrecision(12, 4); b.HasIndex(x => new { x.CampaignId, x.AreaEquipmentId }).IsUnique();
        b.HasOne<InspectionCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CampaignAnnualList>().WithMany().HasForeignKey(x => x.AnnualListId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AreaEquipment>().WithMany().HasForeignKey(x => x.AreaEquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SamplingApprovalConfiguration : IEntityTypeConfiguration<SamplingApproval>
{
    public void Configure(EntityTypeBuilder<SamplingApproval> b)
    {
        b.ToTable("SamplingApprovals"); b.HasKey(x => x.Id); b.Property(x => x.SelectedEquipmentIdsJson).HasMaxLength(2000000).IsRequired(); b.Property(x => x.OverridesJson).HasMaxLength(2000000); b.HasIndex(x=>new{x.CampaignId,x.AnnualListId,x.CampaignRevision});
        b.HasOne<InspectionCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict); b.HasOne<CampaignAnnualList>().WithMany().HasForeignKey(x => x.AnnualListId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InspectionWorkPackageConfiguration : IEntityTypeConfiguration<InspectionWorkPackage>
{
    public void Configure(EntityTypeBuilder<InspectionWorkPackage> b)
    {
        b.ToTable("InspectionWorkPackages"); b.HasKey(x => x.Id); b.Property(x => x.Reference).HasMaxLength(100).IsRequired(); b.Property(x => x.Site).HasMaxLength(200); b.Property(x => x.Area).HasMaxLength(200); b.Property(x => x.Rotation).HasMaxLength(100); b.Property(x => x.Status).HasMaxLength(40);
        b.HasIndex(x => new { x.CampaignId, x.Reference }).IsUnique(); b.HasOne<InspectionCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InspectionTaskAssignmentEventConfiguration : IEntityTypeConfiguration<InspectionTaskAssignmentEvent>
{
    public void Configure(EntityTypeBuilder<InspectionTaskAssignmentEvent> b)
    {
        b.ToTable("InspectionTaskAssignmentEvents"); b.HasKey(x => x.Id); b.Property(x => x.Reason).HasMaxLength(1000); b.HasIndex(x => new { x.InspectionTaskId, x.ChangedAt }); b.HasOne<InspectionTask>().WithMany().HasForeignKey(x => x.InspectionTaskId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PlanningApprovalEventConfiguration : IEntityTypeConfiguration<PlanningApprovalEvent>
{
    public void Configure(EntityTypeBuilder<PlanningApprovalEvent> b)
    {
        b.ToTable("PlanningApprovalEvents"); b.HasKey(x => x.Id); b.Property(x => x.Decision).HasMaxLength(40).IsRequired(); b.Property(x => x.Comments).HasMaxLength(4000); b.HasIndex(x => new { x.CampaignId, x.Revision, x.OccurredAt }); b.HasOne<InspectionCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
    }
}
