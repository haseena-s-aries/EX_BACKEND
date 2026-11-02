using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ex_SurveyApp_api.Services;

public sealed record BackfillProject(Guid ProjectId);
public sealed record BackfillArea(Guid AreaId, Guid? ProjectId, string SourceTree = "Project");
public sealed record BackfillEquipment(Guid EquipmentId, Guid? ProjectId, Guid? AreaId, string SourceTree = "Project");
public sealed record ApprovedProjectOwnership(
    Guid MappingId,
    Guid ProjectId,
    Guid TenantId,
    Guid ContractId,
    Guid SiteId,
    string ApprovalStatus,
    string? ApprovedBy,
    string? ApprovalReference,
    DateTimeOffset? ApprovedAt);
public sealed record ExistingProjectOwnership(Guid ProjectId, Guid TenantId, Guid ContractId, Guid SiteId);
public sealed record ContractOwner(Guid ContractId, Guid TenantId);
public sealed record SiteOwner(Guid SiteId, Guid TenantId, Guid ContractId);
public sealed record BackfillLegacyRecord(Guid RecordId, string EntityType, string Detail);

public sealed record TenantOwnershipInput(
    IReadOnlyList<BackfillProject> Projects,
    IReadOnlyList<BackfillArea> Areas,
    IReadOnlyList<BackfillEquipment> Equipment,
    IReadOnlyList<ApprovedProjectOwnership> Mappings,
    IReadOnlyList<ContractOwner> Contracts,
    IReadOnlyList<SiteOwner> Sites,
    IReadOnlyList<ExistingProjectOwnership> ExistingOwnership,
    IReadOnlyList<BackfillLegacyRecord> UnlinkedLegacyRecords);

public sealed record BackfillIssue(string Code, string EntityType, Guid EntityId, string Detail);
public sealed record TenantOwnershipPlan(
    bool CanApply,
    int ProjectsRead,
    int AreasRead,
    int EquipmentRead,
    int ProposedBindings,
    int AlreadyApplied,
    IReadOnlyList<ApprovedProjectOwnership> BindingsToApply,
    IReadOnlyList<BackfillIssue> Issues);

/// <summary>Transactional write boundary for an approved, separately reviewed future adapter.</summary>
public interface ITenantBackfillTransaction : IAsyncDisposable
{
    Task ApplyOwnershipAsync(ApprovedProjectOwnership ownership, CancellationToken cancellationToken);
    Task WriteAuditAsync(ApprovedProjectOwnership ownership, Guid actorId, string approvalReference, CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}

public interface ITenantBackfillStore
{
    Task<ITenantBackfillTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

public static class TenantOwnershipBackfillExecutor
{
    /// <summary>
    /// Transactional coordinator only. No production adapter or application endpoint is registered;
    /// callers must supply an explicitly authorized isolated-store implementation.
    /// </summary>
    public static async Task ExecuteApprovedPlanAsync(TenantOwnershipPlan plan, ITenantBackfillStore store,
        Guid actorId, string authorizationReference, CancellationToken cancellationToken = default)
    {
        if (!plan.CanApply || plan.Issues.Count != 0) throw new InvalidOperationException("Backfill preflight has unresolved ownership issues.");
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(authorizationReference))
            throw new UnauthorizedAccessException("A named operator and external approval reference are required.");

        await using var transaction = await store.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var ownership in plan.BindingsToApply)
            {
                if (!string.Equals(ownership.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
                    ownership.ApprovedAt is null || string.IsNullOrWhiteSpace(ownership.ApprovedBy) ||
                    string.IsNullOrWhiteSpace(ownership.ApprovalReference))
                    throw new InvalidOperationException($"Project {ownership.ProjectId} lacks a complete ownership approval.");
                await transaction.ApplyOwnershipAsync(ownership, cancellationToken);
                await transaction.WriteAuditAsync(ownership, actorId, authorizationReference, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

/// <summary>
/// Pure, read-only planner. It consumes explicit mapping approvals and relationship snapshots;
/// it never guesses from names and performs no database writes.
/// </summary>
public static class TenantOwnershipBackfillPlanner
{
    public static TenantOwnershipPlan Create(TenantOwnershipInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Projects is null || input.Areas is null || input.Equipment is null || input.Mappings is null ||
            input.Contracts is null || input.Sites is null || input.ExistingOwnership is null)
            throw new InvalidDataException("Snapshot must include Projects, Areas, Equipment, Mappings, Contracts, Sites and ExistingOwnership arrays.");
        var issues = new List<BackfillIssue>();
        foreach (var legacy in input.UnlinkedLegacyRecords ?? [])
            issues.Add(Issue("LegacyCrosswalkRequired", legacy.EntityType, legacy.RecordId, legacy.Detail));
        var projectIds = input.Projects.Select(x => x.ProjectId).ToHashSet();
        foreach (var duplicate in input.Projects.GroupBy(x => x.ProjectId).Where(x => x.Count() > 1))
            issues.Add(Issue("DuplicateProjectRecord", "Project", duplicate.Key, "Project ID occurs more than once in the source snapshot."));

        var mappingsByProject = input.Mappings.GroupBy(x => x.ProjectId).ToDictionary(x => x.Key, x => x.ToArray());
        foreach (var unknown in input.Mappings.Where(x => !projectIds.Contains(x.ProjectId)))
            issues.Add(Issue("MappingForUnknownProject", "Project", unknown.ProjectId, "Approved mapping references no source project."));
        foreach (var project in input.Projects)
        {
            if (!mappingsByProject.TryGetValue(project.ProjectId, out var mappings) || mappings.Length == 0)
            {
                issues.Add(Issue("MissingOwnership", "Project", project.ProjectId, "No explicit ownership mapping exists."));
                continue;
            }
            if (mappings.Length > 1)
            {
                issues.Add(Issue("DuplicateOwnershipMapping", "Project", project.ProjectId, $"Project has {mappings.Length} mapping records; exactly one authoritative approved mapping is required."));
                continue;
            }

            var mapping = mappings[0];
            if (!string.Equals(mapping.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(mapping.ApprovedBy) || string.IsNullOrWhiteSpace(mapping.ApprovalReference) || mapping.ApprovedAt is null)
                issues.Add(Issue("MappingNotApproved", "Project", project.ProjectId, "Mapping requires Approved status, approver, approval reference and timestamp."));
            if (!input.Contracts.Any(x => x.ContractId == mapping.ContractId && x.TenantId == mapping.TenantId))
                issues.Add(Issue("ContractTenantMismatch", "Project", project.ProjectId, "Contract does not belong to the explicitly mapped tenant."));
            if (!input.Sites.Any(x => x.SiteId == mapping.SiteId && x.TenantId == mapping.TenantId && x.ContractId == mapping.ContractId))
                issues.Add(Issue("SiteContractTenantMismatch", "Project", project.ProjectId, "Site does not belong to the mapped contract and tenant."));
        }

        foreach (var duplicate in input.Contracts.GroupBy(x => x.ContractId).Where(x => x.Select(y => y.TenantId).Distinct().Count() > 1))
            issues.Add(Issue("ConflictingContractOwner", "Contract", duplicate.Key, "Contract has conflicting tenant ownership."));
        foreach (var duplicate in input.Sites.GroupBy(x => x.SiteId).Where(x => x.Select(y => (y.TenantId, y.ContractId)).Distinct().Count() > 1))
            issues.Add(Issue("ConflictingSiteOwner", "Site", duplicate.Key, "Site has conflicting tenant or contract ownership."));

        var areaIds = input.Areas.Select(x => x.AreaId).ToHashSet();
        foreach (var duplicate in input.Areas.GroupBy(x => x.AreaId).Where(x => x.Count() > 1))
            issues.Add(Issue("DuplicateAreaRecord", duplicate.First().SourceTree + "Area", duplicate.Key, "Area ID occurs more than once in the source snapshot."));
        foreach (var duplicate in input.Equipment.GroupBy(x => x.EquipmentId).Where(x => x.Count() > 1))
            issues.Add(Issue("DuplicateEquipmentRecord", duplicate.First().SourceTree + "Equipment", duplicate.Key, "Equipment ID occurs more than once in the source snapshot."));
        foreach (var area in input.Areas)
        {
            if (area.ProjectId is null || !projectIds.Contains(area.ProjectId.Value))
                issues.Add(Issue("OrphanArea", area.SourceTree + "Area", area.AreaId, "Area has no valid parent project."));
        }
        foreach (var equipment in input.Equipment)
        {
            var area = input.Areas.FirstOrDefault(x => x.AreaId == equipment.AreaId);
            if (equipment.AreaId is null || !areaIds.Contains(equipment.AreaId.Value) || area is null)
            {
                issues.Add(Issue("OrphanEquipment", equipment.SourceTree + "Equipment", equipment.EquipmentId, "Equipment has no valid parent area."));
                continue;
            }
            if (equipment.ProjectId is not null && area.ProjectId != equipment.ProjectId)
                issues.Add(Issue("CrossProjectEquipment", equipment.SourceTree + "Equipment", equipment.EquipmentId, "Equipment project differs from its area's project."));
        }

        var already = new HashSet<Guid>();
        var existingByProject = input.ExistingOwnership.GroupBy(x => x.ProjectId).ToArray();
        foreach (var group in existingByProject)
        {
            var tenants = group.Select(x => x.TenantId).Distinct().ToArray();
            if (tenants.Length != 1)
            {
                issues.Add(Issue("ConflictingExistingOwnership", "Project", group.Key, "Existing bindings point to more than one tenant."));
                continue;
            }
            if (!mappingsByProject.TryGetValue(group.Key, out var map) || map.Length != 1) continue;
            var approved = map[0];
            if (group.Any(x => x.TenantId != approved.TenantId || x.ContractId != approved.ContractId || x.SiteId != approved.SiteId))
                issues.Add(Issue("ExistingOwnershipConflict", "Project", group.Key, "Existing binding differs from the approved mapping."));
            else already.Add(group.Key);
        }

        var invalidProjects = issues.Where(x => x.EntityType == "Project").Select(x => x.EntityId).ToHashSet();
        var bindings = input.Projects.Where(x => !invalidProjects.Contains(x.ProjectId) && mappingsByProject.TryGetValue(x.ProjectId, out var map) && map.Length == 1 &&
            !already.Contains(x.ProjectId) && !issues.Any(i => i.EntityId == x.ProjectId && i.Code is "ContractTenantMismatch" or "SiteContractTenantMismatch" or "MappingNotApproved"))
            .Select(x => mappingsByProject[x.ProjectId][0]).ToArray();
        return new TenantOwnershipPlan(issues.Count == 0, input.Projects.Count, input.Areas.Count, input.Equipment.Count,
            bindings.Length, already.Count, bindings, issues.OrderBy(x => x.EntityType).ThenBy(x => x.EntityId).ThenBy(x => x.Code).ToArray());
    }

    public static async Task<TenantOwnershipInput> ReadJsonAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<TenantOwnershipInput>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Backfill input document is empty.");
    }

    public static async Task WriteReportAsync(TenantOwnershipPlan plan, string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, plan, JsonOptions, cancellationToken);
    }

    private static BackfillIssue Issue(string code, string type, Guid id, string detail) => new(code, type, id, detail);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
}
