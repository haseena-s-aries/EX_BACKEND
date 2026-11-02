# Tenant Ownership Mapping Specification

**Status: preparation design; ownership decisions and data-entry authority are not approved.** No existing record has been assigned to a tenant.

## Mapping chain

`Aries Platform → Tenant → Contract → Site → existing Project → Area → Equipment`

The existing project key and equipment key are preserved. Tenant/contract/site ownership must come from explicit authoritative source keys and an approved crosswalk. `ProjectName`, `JobNo`, `VesselOrSiteName`, `Location`, equipment tags, customer names and other free text are search/display hints only and never ownership evidence.

## Source structures

- Active application hierarchy: `Project → ProjectArea(ProjectId) → AreaEquipment(ProjectId, AreaId)`. The duplicated ProjectId on equipment must agree with the referenced ProjectArea.ProjectId.
- Legacy hierarchy: `Job → Area(JobId) → Equipment(AreaId)`. Legacy Job currently has ClientId but no authoritative Project FK, and legacy Equipment has no ProjectId. Do not merge these trees or infer a Project from a similar job/name/tag. Unlinked legacy records remain exceptions until Aries/KAS3 supplies the crosswalk.
- `TenantProjectBinding` is the additive bridge from the existing Project ID to explicit Tenant and Contract. Site is explicit sidecar identity; `SiteId`/mapping must be approved. The existing Project row is not renamed or assigned a generated replacement key.
- `LegacyEntityMap` stores source-system/entity/key and target identity as a proposed crosswalk. A verified mapping requires a complete target. `TenantBackfillException` records missing, orphaned, ambiguous, conflicting and rejected records after persistence is separately approved.

## Review and approval

Every project needs exactly one authoritative tenant owner. A project may require more than one contract/site relationship; that cardinality is unresolved, so mapping review must distinguish the single owner from approved project bindings and must not rely on uniqueness assumptions. Each proposal includes source system, source record key, original project ID, TenantId, ContractId, SiteId, evidence reference, reviewer, approver, approval reference and UTC approval time. Proposals are not eligible for execution until status is `Approved` and all approval fields are present. Duplicate mapping rows or conflicting tenant assignments block the complete batch.

Review/approval should be append-only: retain proposal, reviewer findings, approval/rejection, approver identity, source evidence/checksum, batch ID and timestamps. Use the existing AuditLog for actor/action evidence only after an approved write-path design; do not overwrite the source mapping to erase prior decisions. Phase 2A's current `LegacyEntityMap` has no reviewer columns; a durable mapping-review ledger and schema/API decision are still required before productionizing approval capture.

## Validation and exceptions

- Missing project mapping, duplicate mapping, unknown project, missing contract, contract/tenant mismatch, site/contract/tenant mismatch, existing-binding conflict, orphan ProjectArea, orphan AreaEquipment and cross-project AreaEquipment are blocking issues.
- Legacy Area/Equipment without a deterministic approved project crosswalk remains unresolved. Preserve its original IDs and report it; never move or delete it as part of this package.
- Exported snapshots carry any legacy Job/Area/Equipment rows that lack a direct project crosswalk in `UnlinkedLegacyRecords`; each produces a `LegacyCrosswalkRequired` exception without inventing a relation.
- A mapping is idempotent only when an already-present binding exactly matches the approved tenant/contract/site tuple. Different existing ownership is a conflict, not an update.
- Apply is batch-atomic with audit writes in the same transaction. On any error, roll back the entire batch. Reconciliation output includes source counts, proposed/no-op counts, affected IDs, exceptions and checksums.

## Backfill preparation utility

`tools/TenantBackfillPreparation` consumes a JSON snapshot/export plus explicit mapping approvals and emits a deterministic JSON reconciliation report. It requires `--dry-run`; it has no database connection and exposes no write mode. Example:

```powershell
dotnet run --project tools/TenantBackfillPreparation/TenantBackfillPreparation.csproj -c Release -- --dry-run --input snapshot.json --report reconciliation.json
```

The transaction coordinator in `TenantBackfillReconciliation.cs` rejects unresolved plans or missing operator/approval reference and performs ownership plus audit operations in one transaction abstraction. No concrete EF writer, administrative endpoint or write command is registered. A future writer remains gated on architecture approval, isolated rehearsal, durable review-ledger design and an independently reviewed rollback/recovery plan. No mappings are applied now.
