using Ex_SurveyApp_api.Services;

internal static class TenantBackfillChecks
{
    public static async Task Run()
    {
        var tenant = Guid.NewGuid(); var contract = Guid.NewGuid(); var site = Guid.NewGuid(); var project = Guid.NewGuid();
        var area = Guid.NewGuid(); var equipment = Guid.NewGuid(); var mappingId = Guid.NewGuid();
        var baseInput = new TenantOwnershipInput(
            [new(project)], [new(area, project)], [new(equipment, project, area)],
            [Approved(mappingId, project, tenant, contract, site)],
            [new(contract, tenant)], [new(site, tenant, contract)], [], []);
        var valid = TenantOwnershipBackfillPlanner.Create(baseInput);
        Check(valid.CanApply && valid.ProposedBindings == 1 && valid.BindingsToApply.Single().ProjectId == project, "Approved project ownership produces one deterministic binding");
        Check(!TenantOwnershipBackfillPlanner.Create(baseInput with { Mappings = [] }).CanApply, "Missing project ownership is unresolved");
        Check(!TenantOwnershipBackfillPlanner.Create(baseInput with { Mappings = [.. baseInput.Mappings, Approved(Guid.NewGuid(), project, tenant, contract, site)] }).CanApply, "Duplicate ownership mappings are rejected");
        Check(!TenantOwnershipBackfillPlanner.Create(baseInput with { Mappings = [Approved(mappingId, project, Guid.NewGuid(), contract, site)] }).CanApply, "Contract and tenant conflicts are rejected");
        Check(!TenantOwnershipBackfillPlanner.Create(baseInput with { Areas = [new(area, null)] }).CanApply, "Orphan area is reported");
        Check(!TenantOwnershipBackfillPlanner.Create(baseInput with { Equipment = [new(equipment, Guid.NewGuid(), area)] }).CanApply, "Cross-project equipment is reported");
        Check(!TenantOwnershipBackfillPlanner.Create(baseInput with { UnlinkedLegacyRecords = [new(Guid.NewGuid(), "LegacyJob", "Needs approved Job-to-Project crosswalk.")] }).CanApply,
            "Unlinked legacy Job/Area/Equipment rows remain explicit exceptions");
        var existing = new ExistingProjectOwnership(project, tenant, contract, site);
        var repeated = TenantOwnershipBackfillPlanner.Create(baseInput with { ExistingOwnership = [existing] });
        Check(repeated.CanApply && repeated.ProposedBindings == 0 && repeated.AlreadyApplied == 1, "Repeated execution recognizes identical prior ownership and proposes no write");
        Check(!TenantOwnershipBackfillPlanner.Create(baseInput with { ExistingOwnership = [existing with { TenantId = Guid.NewGuid() }] }).CanApply, "Conflicting existing ownership is rejected");
        var noApproval = baseInput with { Mappings = [Approved(mappingId, project, tenant, contract, site) with { ApprovalStatus = "Proposed", ApprovedBy = null, ApprovalReference = null, ApprovedAt = null }] };
        Check(!TenantOwnershipBackfillPlanner.Create(noApproval).CanApply, "Unreviewed mapping cannot pass backfill preflight");

        var secondProject = Guid.NewGuid();
        var twoProjectInput = baseInput with
        {
            Projects = [new(project), new(secondProject)],
            Areas = [new(area, project)],
            Equipment = [new(equipment, project, area)],
            Mappings = [Approved(mappingId, project, tenant, contract, site), Approved(Guid.NewGuid(), secondProject, tenant, contract, site)]
        };
        var applyPlan = TenantOwnershipBackfillPlanner.Create(twoProjectInput);
        var transactionalStore = new FakeTransactionalStore(failAfterApply: 1);
        try
        {
            await TenantOwnershipBackfillExecutor.ExecuteApprovedPlanAsync(applyPlan, transactionalStore, Guid.NewGuid(), "APPROVAL-REF");
            throw new InvalidOperationException("Expected injected transactional failure.");
        }
        catch (IOException) { }
        Check(transactionalStore.Applied.Count == 0 && transactionalStore.Audits.Count == 0 && transactionalStore.RolledBack,
            "Partial apply failure rolls back ownership and audit writes atomically");
        var successfulStore = new FakeTransactionalStore();
        await TenantOwnershipBackfillExecutor.ExecuteApprovedPlanAsync(applyPlan, successfulStore, Guid.NewGuid(), "APPROVAL-REF");
        Check(successfulStore.Applied.Count == 2 && successfulStore.Audits.Count == 2 && successfulStore.Committed,
            "Approved execution writes ownership and audit records in one transaction");

        var temp = Path.Combine(Path.GetTempPath(), "tenant-reconcile-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await TenantOwnershipBackfillPlanner.WriteReportAsync(valid, temp);
            Check(File.Exists(temp) && (await File.ReadAllTextAsync(temp)).Contains("ProposedBindings", StringComparison.Ordinal), "Dry-run reconciliation report is written as a repeatable artifact");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Console.WriteLine("All tenant backfill preparation checks passed. No writes were performed.");
    }

    private static ApprovedProjectOwnership Approved(Guid map, Guid project, Guid tenant, Guid contract, Guid site) =>
        new(map, project, tenant, contract, site, "Approved", "reviewer@example.invalid", "ADR-APPROVAL-1", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Console.WriteLine("PASS: " + description);
    }

    private sealed class FakeTransactionalStore(int? failAfterApply = null) : ITenantBackfillStore
    {
        public List<Guid> Applied { get; } = [];
        public List<Guid> Audits { get; } = [];
        public bool RolledBack { get; private set; }
        public bool Committed { get; private set; }
        public Task<ITenantBackfillTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ITenantBackfillTransaction>(new FakeTransaction(this, failAfterApply));

        private sealed class FakeTransaction(FakeTransactionalStore owner, int? failAfterApply) : ITenantBackfillTransaction
        {
            private readonly Guid[] appliedAtStart = owner.Applied.ToArray();
            private readonly Guid[] auditsAtStart = owner.Audits.ToArray();
            public Task ApplyOwnershipAsync(ApprovedProjectOwnership ownership, CancellationToken cancellationToken)
            {
                owner.Applied.Add(ownership.ProjectId);
                if (failAfterApply is not null && owner.Applied.Count > failAfterApply.Value) throw new IOException("Injected mid-transaction failure");
                return Task.CompletedTask;
            }
            public Task WriteAuditAsync(ApprovedProjectOwnership ownership, Guid actorId, string approvalReference, CancellationToken cancellationToken)
            { owner.Audits.Add(ownership.ProjectId); return Task.CompletedTask; }
            public Task CommitAsync(CancellationToken cancellationToken) { owner.Committed = true; return Task.CompletedTask; }
            public Task RollbackAsync(CancellationToken cancellationToken)
            {
                owner.Applied.Clear(); owner.Applied.AddRange(appliedAtStart);
                owner.Audits.Clear(); owner.Audits.AddRange(auditsAtStart);
                owner.RolledBack = true;
                return Task.CompletedTask;
            }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
