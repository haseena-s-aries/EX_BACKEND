using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ex_SurveyApp_api.Data;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).HasDefaultValue(TenantLifecycleStatus.Provisioning);
        builder.HasIndex(x => x.TenantKey).IsUnique();
    }
}

internal sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("TenantContracts");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Property(x => x.ContractKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(40);
        builder.HasIndex(x => new { x.TenantId, x.ContractKey }).IsUnique();
        builder.HasOne(x => x.Tenant).WithMany(x => x.Contracts).HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TenantProjectBindingConfiguration : IEntityTypeConfiguration<TenantProjectBinding>
{
    public void Configure(EntityTypeBuilder<TenantProjectBinding> builder)
    {
        builder.ToTable("TenantProjectBindings");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.ContractId, x.ProjectId }).IsUnique();
        builder.HasIndex(x => x.ProjectId);
        builder.HasOne(x => x.Contract).WithMany(x => x.ProjectBindings)
            .HasForeignKey(x => new { x.TenantId, x.ContractId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        // This relationship is stored only on the new bridge table. Project's schema and its
        // existing ProjectArea/AreaEquipment relationships are intentionally unchanged.
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("TenantSites");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Property(x => x.SiteKey).HasMaxLength(120);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SiteType).HasMaxLength(40);
        builder.Property(x => x.Location).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).HasDefaultValue(TenantLifecycleStatus.Provisioning);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TenantProjectBinding>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.TenantProjectBindingId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("TenantMemberships");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).HasDefaultValue(TenantMembershipStatus.Invited);
        builder.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        builder.HasOne(x => x.Tenant).WithMany(x => x.Memberships).HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TenantRoleGrantConfiguration : IEntityTypeConfiguration<TenantRoleGrant>
{
    public void Configure(EntityTypeBuilder<TenantRoleGrant> builder)
    {
        builder.ToTable("TenantRoleGrants", table => table.HasCheckConstraint(
            "CK_TenantRoleGrants_ScopeShape",
            "([ScopeType] = N'Tenant' AND [ScopeId] IS NULL) OR ([ScopeType] <> N'Tenant' AND [ScopeId] IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RoleCode).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ScopeType).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.TenantMembershipId, x.RoleCode, x.ScopeType, x.ScopeId })
            .IsUnique().HasFilter("[ScopeId] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.TenantMembershipId, x.RoleCode, x.ScopeType })
            .IsUnique().HasFilter("[ScopeType] = N'Tenant'");
        builder.HasOne(x => x.Membership).WithMany(x => x.RoleGrants)
            .HasForeignKey(x => new { x.TenantId, x.TenantMembershipId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LegacyEntityMapConfiguration : IEntityTypeConfiguration<LegacyEntityMap>
{
    public void Configure(EntityTypeBuilder<LegacyEntityMap> builder)
    {
        builder.ToTable("LegacyEntityMaps", table => table.HasCheckConstraint(
            "CK_LegacyEntityMaps_VerifiedTarget",
            "[Status] <> N'Verified' OR ([TargetEntityType] IS NOT NULL AND [TargetId] IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceSystem).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceEntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceRecordKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TargetEntityType).HasMaxLength(100);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).HasDefaultValue(LegacyMappingStatus.Proposed);
        builder.Property(x => x.EvidenceReference).HasMaxLength(500);
        builder.HasIndex(x => new { x.SourceSystem, x.SourceEntityType, x.SourceRecordKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.TargetEntityType, x.TargetId });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TenantBackfillExceptionConfiguration : IEntityTypeConfiguration<TenantBackfillException>
{
    public void Configure(EntityTypeBuilder<TenantBackfillException> builder)
    {
        builder.ToTable("TenantBackfillExceptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceSystem).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceEntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceRecordKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ExceptionCode).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).HasDefaultValue(BackfillExceptionStatus.Open);
        builder.Property(x => x.EvidenceReference).HasMaxLength(500);
        builder.HasIndex(x => new { x.MigrationBatchId, x.SourceSystem, x.SourceEntityType, x.SourceRecordKey, x.ExceptionCode }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
