using System.Security.Claims;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public sealed class TenantFoundationOptions
{
    public bool Enabled { get; set; }
}

public static class TenantAuthorizationPolicyNames
{
    public const string TenantMember = "TenantMember";
    public const string TenantResource = "TenantResource";
    public const string TenantSelectorHeader = "X-Tenant-Id";
}

public enum TenantResourceKind
{
    Contract, Project, Site, ProjectArea, AreaEquipment, SurveySession, ChecklistAnswer,
    Finding, Defect, ProjectAreaPhoto, SurveyFile, QcReview, Report, Export, ClientDashboard
}

/// <summary>Passive endpoint metadata; no existing endpoint is decorated by Phase 2A.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class TenantResourceScopeAttribute(
    TenantResourceKind resourceKind,
    string routeParameter,
    string? requiredRoleCode = null) : Attribute
{
    public TenantResourceKind ResourceKind { get; } = resourceKind;
    public string RouteParameter { get; } = routeParameter;
    public string? RequiredRoleCode { get; } = requiredRoleCode;
}

public sealed record TenantScopeResolution(
    bool Succeeded,
    Guid? TenantId,
    Guid? MembershipId,
    string FailureCode)
{
    public static TenantScopeResolution Denied(string code) => new(false, null, null, code);
    public static TenantScopeResolution Allowed(Guid tenantId, Guid membershipId) => new(true, tenantId, membershipId, "Allowed");
}

/// <summary>Created only after the server validates the authenticated user's active membership.</summary>
public sealed record AuthorizedTenantContext
{
    public Guid UserId { get; }
    public Guid TenantId { get; }
    public Guid MembershipId { get; }
    internal AuthorizedTenantContext(Guid userId, Guid tenantId, Guid membershipId) =>
        (UserId, TenantId, MembershipId) = (userId, tenantId, membershipId);
}

public interface ITenantContextAccessor
{
    AuthorizedTenantContext? Current { get; }
}

public sealed class HttpTenantContextAccessor(IHttpContextAccessor accessor) : ITenantContextAccessor
{
    public AuthorizedTenantContext? Current => accessor.HttpContext?.Items.TryGetValue(typeof(AuthorizedTenantContext), out var value) == true
        ? value as AuthorizedTenantContext : null;
}

public sealed record TenantResourceOwnership(
    Guid TenantId,
    TenantResourceKind ResourceKind,
    Guid ResourceId,
    Guid? ProjectId,
    IReadOnlySet<Guid> ContractIds,
    Guid? SiteId);

public interface ITenantScopeResolver
{
    Task<TenantScopeResolution> ResolveMembershipAsync(Guid userId, Guid? requestedTenantId, CancellationToken cancellationToken);
    Task<(TenantResourceOwnership? Ownership, string FailureCode)> ResolveResourceAsync(TenantResourceKind kind, Guid resourceId, CancellationToken cancellationToken);
    Task<bool> HasRoleGrantAsync(Guid membershipId, string roleCode, TenantResourceOwnership ownership, CancellationToken cancellationToken);
}

/// <summary>
/// All tenant and resource ownership values are re-read from the database. The request header is
/// only a selector and never proves tenant ownership by itself.
/// </summary>
public sealed class TenantScopeResolver(SurveyDbContext db) : ITenantScopeResolver
{
    public async Task<TenantScopeResolution> ResolveMembershipAsync(Guid userId, Guid? requestedTenantId, CancellationToken cancellationToken)
    {
        if (requestedTenantId is null || requestedTenantId == Guid.Empty)
            return TenantScopeResolution.Denied("MissingTenantContext");

        var membership = await db.TenantMemberships.AsNoTracking()
            .Where(x => x.UserId == userId && x.TenantId == requestedTenantId.Value && x.Status == TenantMembershipStatus.Active)
            .Join(db.Tenants.AsNoTracking().Where(x => x.Status == TenantLifecycleStatus.Active),
                x => x.TenantId, tenant => tenant.Id, (x, _) => x)
            .Select(x => new { x.TenantId, x.Id })
            .SingleOrDefaultAsync(cancellationToken);
        return membership is null
            ? TenantScopeResolution.Denied("NoActiveTenantMembership")
            : TenantScopeResolution.Allowed(membership.TenantId, membership.Id);
    }

    public async Task<(TenantResourceOwnership? Ownership, string FailureCode)> ResolveResourceAsync(
        TenantResourceKind kind, Guid resourceId, CancellationToken cancellationToken)
    {
        if (resourceId == Guid.Empty) return (null, "InvalidResourceId");

        if (kind == TenantResourceKind.Contract)
        {
            var contract = await db.TenantContracts.AsNoTracking().Where(x => x.Id == resourceId)
                .Select(x => new { x.TenantId, x.Id }).SingleOrDefaultAsync(cancellationToken);
            return contract is null
                ? (null, "UnmappedResource")
                : (new TenantResourceOwnership(contract.TenantId, kind, contract.Id, null,
                    new HashSet<Guid> { contract.Id }, null), "Allowed");
        }

        if (kind == TenantResourceKind.Site)
        {
            var site = await (from s in db.TenantSites.AsNoTracking()
                              join b in db.TenantProjectBindings.AsNoTracking() on
                                  new { s.TenantId, s.TenantProjectBindingId } equals
                                  new { b.TenantId, TenantProjectBindingId = b.Id }
                              where s.Id == resourceId
                              select new { s.TenantId, SiteId = s.Id, b.ProjectId, b.ContractId })
                .SingleOrDefaultAsync(cancellationToken);
            return site is null
                ? (null, "UnmappedResource")
                : (new TenantResourceOwnership(site.TenantId, kind, site.SiteId, site.ProjectId,
                    new HashSet<Guid> { site.ContractId }, site.SiteId), "Allowed");
        }

        if (kind == TenantResourceKind.SurveySession)
        {
            var session = await db.SurveySessions.AsNoTracking().Where(x => x.Id == resourceId)
                .Select(x => new { x.AreaEquipmentId, x.EquipmentId, x.JobId }).SingleOrDefaultAsync(cancellationToken);
            if (session is null) return (null, "UnmappedResource");
            var parents = new List<(TenantResourceOwnership? Ownership, string FailureCode)>();
            if (session.AreaEquipmentId is not null) parents.Add(await ResolveResourceAsync(TenantResourceKind.AreaEquipment, session.AreaEquipmentId.Value, cancellationToken));
            if (session.EquipmentId is not null) parents.Add(await ResolveLegacyOwnershipAsync("Equipment", session.EquipmentId.Value, kind, resourceId, cancellationToken));
            if (session.JobId is not null) parents.Add(await ResolveLegacyOwnershipAsync("Job", session.JobId.Value, kind, resourceId, cancellationToken));
            return CombineParentOwnership(kind, resourceId, parents);
        }

        if (kind == TenantResourceKind.ChecklistAnswer)
        {
            var sessionId = await db.SurveyAnswers.AsNoTracking().Where(x => x.Id == resourceId)
                .Select(x => (Guid?)x.SurveySessionId).SingleOrDefaultAsync(cancellationToken);
            return sessionId is null ? (null, "UnmappedResource")
                : RebindOwnership(await ResolveResourceAsync(TenantResourceKind.SurveySession, sessionId.Value, cancellationToken), kind, resourceId);
        }

        if (kind is TenantResourceKind.Finding or TenantResourceKind.Defect)
        {
            var sessionId = await db.Findings.AsNoTracking().Where(x => x.Id == resourceId)
                .Select(x => (Guid?)x.SurveySessionId).SingleOrDefaultAsync(cancellationToken);
            return sessionId is null ? (null, "UnmappedResource")
                : RebindOwnership(await ResolveResourceAsync(TenantResourceKind.SurveySession, sessionId.Value, cancellationToken), kind, resourceId);
        }

        if (kind == TenantResourceKind.QcReview)
        {
            var sessionId = await db.QcReviews.AsNoTracking().Where(x => x.Id == resourceId)
                .Select(x => (Guid?)x.SurveySessionId).SingleOrDefaultAsync(cancellationToken);
            return sessionId is null ? (null, "UnmappedResource")
                : RebindOwnership(await ResolveResourceAsync(TenantResourceKind.SurveySession, sessionId.Value, cancellationToken), kind, resourceId);
        }

        if (kind == TenantResourceKind.ProjectAreaPhoto)
        {
            var areaId = await db.ProjectAreaPhotos.AsNoTracking().Where(x => x.Id == resourceId)
                .Select(x => (Guid?)x.ProjectAreaId).SingleOrDefaultAsync(cancellationToken);
            return areaId is null ? (null, "UnmappedResource")
                : RebindOwnership(await ResolveResourceAsync(TenantResourceKind.ProjectArea, areaId.Value, cancellationToken), kind, resourceId);
        }

        if (kind == TenantResourceKind.SurveyFile)
        {
            var file = await db.Files.AsNoTracking().Where(x => x.Id == resourceId)
                .Select(x => new { x.SurveySessionId, x.FindingId, x.JobId, x.EquipmentId, x.AreaId }).SingleOrDefaultAsync(cancellationToken);
            if (file is null) return (null, "UnmappedResource");
            var parents = new List<(TenantResourceOwnership? Ownership, string FailureCode)>();
            if (file.SurveySessionId is not null) parents.Add(await ResolveResourceAsync(TenantResourceKind.SurveySession, file.SurveySessionId.Value, cancellationToken));
            if (file.FindingId is not null) parents.Add(await ResolveResourceAsync(TenantResourceKind.Finding, file.FindingId.Value, cancellationToken));
            if (file.EquipmentId is not null) parents.Add(await ResolveLegacyOwnershipAsync("Equipment", file.EquipmentId.Value, kind, resourceId, cancellationToken));
            if (file.AreaId is not null) parents.Add(await ResolveLegacyOwnershipAsync("Area", file.AreaId.Value, kind, resourceId, cancellationToken));
            if (file.JobId is not null) parents.Add(await ResolveLegacyOwnershipAsync("Job", file.JobId.Value, kind, resourceId, cancellationToken));
            return CombineParentOwnership(kind, resourceId, parents);
        }

        // A report/export resource ID is the server-side Project ID. Collection routes should
        // instead require TenantMember and apply TenantQueryScope to their EF query.
        if (kind is TenantResourceKind.Report or TenantResourceKind.Export)
            return RebindOwnership(await ResolveResourceAsync(TenantResourceKind.Project, resourceId, cancellationToken), kind, resourceId);

        if (kind == TenantResourceKind.ClientDashboard)
        {
            var projects = await db.Projects.AsNoTracking().Where(x => x.AssignedClientId == resourceId)
                .Select(x => x.Id).ToListAsync(cancellationToken);
            if (projects.Count == 0) return (null, "UnmappedResource");
            var dashboardBindings = await db.TenantProjectBindings.AsNoTracking().Where(x => projects.Contains(x.ProjectId))
                .Select(x => new { x.TenantId, x.ContractId }).ToListAsync(cancellationToken);
            var dashboardTenantIds = dashboardBindings.Select(x => x.TenantId).Distinct().ToArray();
            if (dashboardTenantIds.Length == 0) return (null, "UnmappedResource");
            if (dashboardTenantIds.Length != 1) return (null, "ConflictingResourceOwnership");
            return (new TenantResourceOwnership(dashboardTenantIds[0], kind, resourceId, null,
                dashboardBindings.Select(x => x.ContractId).ToHashSet(), null), "Allowed");
        }

        Guid projectId;
        if (kind == TenantResourceKind.Project)
        {
            var exists = await db.Projects.AsNoTracking().AnyAsync(x => x.Id == resourceId, cancellationToken);
            if (!exists) return (null, "UnmappedResource");
            projectId = resourceId;
        }
        else if (kind == TenantResourceKind.ProjectArea)
        {
            var parentProjectId = await db.ProjectAreas.AsNoTracking().Where(x => x.Id == resourceId)
                .Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(cancellationToken);
            if (parentProjectId is null) return (null, "UnmappedResource");
            projectId = parentProjectId.Value;
        }
        else if (kind == TenantResourceKind.AreaEquipment)
        {
            var equipment = await db.AreaEquipment.AsNoTracking().Where(x => x.Id == resourceId)
                .Select(x => new { x.ProjectId, x.AreaId }).SingleOrDefaultAsync(cancellationToken);
            if (equipment is null) return (null, "UnmappedResource");
            var areaProjectId = await db.ProjectAreas.AsNoTracking().Where(x => x.Id == equipment.AreaId)
                .Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(cancellationToken);
            if (areaProjectId is null || areaProjectId.Value != equipment.ProjectId)
                return (null, "ConflictingResourceOwnership");
            projectId = equipment.ProjectId;
        }
        else
        {
            return (null, "UnsupportedResourceKind");
        }

        var bindings = await db.TenantProjectBindings.AsNoTracking().Where(x => x.ProjectId == projectId)
            .Select(x => new { x.TenantId, x.ContractId }).ToListAsync(cancellationToken);
        var tenantIds = bindings.Select(x => x.TenantId).Distinct().ToArray();
        if (tenantIds.Length == 0) return (null, "UnmappedResource");
        if (tenantIds.Length != 1) return (null, "ConflictingResourceOwnership");

        return (new TenantResourceOwnership(tenantIds[0], kind, resourceId, projectId,
            bindings.Select(x => x.ContractId).ToHashSet(), null), "Allowed");
    }

    private async Task<(TenantResourceOwnership? Ownership, string FailureCode)> ResolveLegacyOwnershipAsync(
        string sourceEntityType, Guid sourceId, TenantResourceKind requestedKind, Guid requestedId, CancellationToken cancellationToken)
    {
        var maps = await db.LegacyEntityMaps.AsNoTracking().Where(x => x.SourceEntityType == sourceEntityType &&
            x.SourceRecordKey == sourceId.ToString("D") && x.Status == LegacyMappingStatus.Verified &&
            x.TargetEntityType == "Project" && x.TargetId != null && x.TenantId != null)
            .Select(x => new { x.TargetId, x.TenantId }).ToListAsync(cancellationToken);
        if (maps.Count == 0) return (null, "UnmappedResource");
        if (maps.Select(x => (x.TargetId, x.TenantId)).Distinct().Count() != 1)
            return (null, "ConflictingResourceOwnership");
        var map = maps[0];
        var bindings = await db.TenantProjectBindings.AsNoTracking().Where(x => x.ProjectId == map.TargetId)
            .Select(x => new { x.TenantId, x.ContractId }).ToListAsync(cancellationToken);
        if (bindings.Count == 0 || bindings.Any(x => x.TenantId != map.TenantId))
            return (null, "ConflictingResourceOwnership");
        return (new TenantResourceOwnership(map.TenantId!.Value, requestedKind, requestedId, map.TargetId,
            bindings.Select(x => x.ContractId).ToHashSet(), null), "Allowed");
    }

    private static (TenantResourceOwnership? Ownership, string FailureCode) RebindOwnership(
        (TenantResourceOwnership? Ownership, string FailureCode) parent, TenantResourceKind kind, Guid id) =>
        parent.Ownership is null ? parent : (parent.Ownership with { ResourceKind = kind, ResourceId = id }, parent.FailureCode);

    private static (TenantResourceOwnership? Ownership, string FailureCode) CombineParentOwnership(
        TenantResourceKind kind, Guid resourceId, IReadOnlyList<(TenantResourceOwnership? Ownership, string FailureCode)> parents)
    {
        if (parents.Count == 0) return (null, "UnmappedResource");
        if (parents.Any(x => x.Ownership is null))
            return (null, parents.First(x => x.Ownership is null).FailureCode);
        var owners = parents.Select(x => x.Ownership!).ToArray();
        if (owners.Select(x => x.TenantId).Distinct().Count() != 1 ||
            owners.Where(x => x.ProjectId is not null).Select(x => x.ProjectId).Distinct().Count() > 1)
            return (null, "ConflictingResourceOwnership");
        return (new TenantResourceOwnership(owners[0].TenantId, kind, resourceId,
            owners.Select(x => x.ProjectId).FirstOrDefault(x => x is not null),
            owners.SelectMany(x => x.ContractIds).ToHashSet(), owners.Select(x => x.SiteId).FirstOrDefault(x => x is not null)), "Allowed");
    }

    public async Task<bool> HasRoleGrantAsync(Guid membershipId, string roleCode, TenantResourceOwnership ownership,
        CancellationToken cancellationToken)
    {
        var grantQuery = db.TenantRoleGrants.AsNoTracking().Where(x =>
            x.TenantMembershipId == membershipId && x.TenantId == ownership.TenantId && x.RoleCode == roleCode);

        if (await grantQuery.AnyAsync(x => x.ScopeType == TenantRoleScopeType.Tenant && x.ScopeId == null, cancellationToken))
            return true;
        if (ownership.ContractIds.Count > 0 && await grantQuery.AnyAsync(x =>
                x.ScopeType == TenantRoleScopeType.Contract && x.ScopeId != null && ownership.ContractIds.Contains(x.ScopeId.Value), cancellationToken))
            return true;
        if (ownership.ProjectId is not null && await grantQuery.AnyAsync(x =>
                x.ScopeType == TenantRoleScopeType.Project && x.ScopeId == ownership.ProjectId, cancellationToken))
            return true;
        return ownership.SiteId is not null && await grantQuery.AnyAsync(x =>
            x.ScopeType == TenantRoleScopeType.Site && x.ScopeId == ownership.SiteId, cancellationToken);
    }
}

public sealed class TenantScopeRequirement(bool requiresResource) : IAuthorizationRequirement
{
    public bool RequiresResource { get; } = requiresResource;
}

public sealed class TenantScopeAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ITenantScopeResolver resolver,
    IOptions<TenantFoundationOptions> options) : AuthorizationHandler<TenantScopeRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantScopeRequirement requirement)
    {
        // Policies are fail-closed until architecture approval and explicit activation.
        if (!options.Value.Enabled || httpContextAccessor.HttpContext is not { } httpContext) return;
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;

        var requestedTenantId = Guid.TryParse(httpContext.Request.Headers[TenantAuthorizationPolicyNames.TenantSelectorHeader].ToString(), out var parsedTenantId)
            ? parsedTenantId : (Guid?)null;
        var membership = await resolver.ResolveMembershipAsync(userId, requestedTenantId, httpContext.RequestAborted);
        if (!membership.Succeeded || membership.TenantId is null || membership.MembershipId is null) return;
        httpContext.Items[typeof(AuthorizedTenantContext)] = new AuthorizedTenantContext(userId, membership.TenantId.Value, membership.MembershipId.Value);

        if (!requirement.RequiresResource)
        {
            context.Succeed(requirement);
            return;
        }

        var metadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<TenantResourceScopeAttribute>();
        if (metadata is null || !Guid.TryParse(httpContext.Request.RouteValues[metadata.RouteParameter]?.ToString(), out var resourceId)) return;

        var (ownership, failureCode) = await resolver.ResolveResourceAsync(metadata.ResourceKind, resourceId, httpContext.RequestAborted);
        if (ownership is null || ownership.TenantId != membership.TenantId.Value || failureCode != "Allowed") return;
        if (!string.IsNullOrWhiteSpace(metadata.RequiredRoleCode) &&
            !await resolver.HasRoleGrantAsync(membership.MembershipId.Value, metadata.RequiredRoleCode, ownership, httpContext.RequestAborted)) return;

        context.Succeed(requirement);
    }
}

public static class TenantFoundationServiceCollectionExtensions
{
    public static IServiceCollection AddTenantFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TenantFoundationOptions>(configuration.GetSection("TenantFoundation"));
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContextAccessor, HttpTenantContextAccessor>();
        services.AddScoped<ITenantQueryScope, TenantQueryScope>();
        services.AddScoped<ITenantScopeResolver, TenantScopeResolver>();
        services.AddScoped<IAuthorizationHandler, TenantScopeAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(TenantAuthorizationPolicyNames.TenantMember, policy =>
                policy.Requirements.Add(new TenantScopeRequirement(requiresResource: false)));
            options.AddPolicy(TenantAuthorizationPolicyNames.TenantResource, policy =>
                policy.Requirements.Add(new TenantScopeRequirement(requiresResource: true)));
        });
        return services;
    }
}
