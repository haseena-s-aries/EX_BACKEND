namespace Ex_SurveyApp_api.Models;

// These types are Phase 2A foundation metadata. They do not replace the existing UserRole enum,
// change authentication, or assign current projects to tenants.
public enum TenantLifecycleStatus { Provisioning, Active, Suspended, Retired }
public enum TenantMembershipStatus { Invited, Active, Suspended, Revoked }
public enum TenantRoleScopeType { Tenant, Contract, Project, Site }
public enum LegacyMappingStatus { Proposed, Verified, Rejected }
public enum BackfillExceptionStatus { Open, UnderReview, Resolved, Accepted }

public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string TenantKey { get; set; }
    public required string DisplayName { get; set; }
    public TenantLifecycleStatus Status { get; set; } = TenantLifecycleStatus.Provisioning;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<TenantMembership> Memberships { get; set; } = new List<TenantMembership>();
}

public sealed class Contract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string ContractKey { get; set; }
    public required string Name { get; set; }
    public string? StatusCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<TenantProjectBinding> ProjectBindings { get; set; } = new List<TenantProjectBinding>();
}

/// <summary>
/// Explicit future ownership bridge. It preserves Project's current key and schema, and no rows
/// are created automatically. Multiple mappings are representable so unresolved Aries cardinality
/// is not silently assumed; the scope resolver denies ambiguous project ownership.
/// </summary>
public sealed class TenantProjectBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ContractId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Contract Contract { get; set; } = null!;
}

public sealed class Site
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid TenantProjectBindingId { get; set; }
    public string? SiteKey { get; set; }
    public required string Name { get; set; }
    public string? SiteType { get; set; }
    public string? Location { get; set; }
    public TenantLifecycleStatus Status { get; set; } = TenantLifecycleStatus.Provisioning;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TenantMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public TenantMembershipStatus Status { get; set; } = TenantMembershipStatus.Invited;
    public Guid? InvitedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public ICollection<TenantRoleGrant> RoleGrants { get; set; } = new List<TenantRoleGrant>();
}

/// <summary>Role codes are tenant-scoped metadata and are intentionally separate from UserRole.</summary>
public sealed class TenantRoleGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid TenantMembershipId { get; set; }
    public required string RoleCode { get; set; }
    public TenantRoleScopeType ScopeType { get; set; }
    // Null means tenant-wide grant; non-null scopes are validated against server-side records.
    public Guid? ScopeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public TenantMembership Membership { get; set; } = null!;
}

/// <summary>Crosswalk only; polymorphic target IDs are not treated as ownership proof.</summary>
public sealed class LegacyEntityMap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string SourceSystem { get; set; }
    public required string SourceEntityType { get; set; }
    public required string SourceRecordKey { get; set; }
    public string? TargetEntityType { get; set; }
    public Guid? TargetId { get; set; }
    public Guid? TenantId { get; set; }
    public LegacyMappingStatus Status { get; set; } = LegacyMappingStatus.Proposed;
    public string? EvidenceReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Stores migration/backfill exceptions without guessing ownership or copying secrets.</summary>
public sealed class TenantBackfillException
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MigrationBatchId { get; set; }
    public Guid? TenantId { get; set; }
    public required string SourceSystem { get; set; }
    public required string SourceEntityType { get; set; }
    public required string SourceRecordKey { get; set; }
    public required string ExceptionCode { get; set; }
    public required string Description { get; set; }
    public BackfillExceptionStatus Status { get; set; } = BackfillExceptionStatus.Open;
    public string? EvidenceReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}
