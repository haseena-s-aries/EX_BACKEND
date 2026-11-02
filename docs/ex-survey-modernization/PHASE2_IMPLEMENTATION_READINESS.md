# Phase 2 Implementation Readiness

**Status:** Phase 2A additive foundation is implemented locally and its SQL review script is generated but unapplied. This is not approval of the proposed hierarchy. Tenant enforcement, backfill and production database changes remain inactive and gated on architecture approval and isolated migration rehearsal.

## Work packages by dependency

| Readiness bucket | Work | Entry condition / output |
|---|---|---|
| Safe to implement immediately | **Done:** passive endpoint scope inventory metadata on all 10 API controllers, a check that discovers route actions and fails if any lacks a classification, and a CI step running that check. The route-by-route tenant scope proposal is in `PHASE2_TENANCY_ARCHITECTURE_PROPOSAL.md`. | Schema-neutral; no authorization logic or response changed. Existing project, surveyor, QC, client and report behavior remains the acceptance baseline. |
| Requires Aries architecture approval | `Tenant`/`Contract`/`Site` semantics, membership/tenant context, role remapping, existing-route scope enforcement, business-table tenant columns/FKs/RLS, repeat-attempt and checklist schema, backfill mapping, cutoff and rollback. | Approve the target hierarchy, keys, authorization roles, data rules and design decision record first. Then review the additive Phase 2A migration and rehearse against an isolated SQL Server copy. |
| Requires KAS3 client inputs | Source IDs/schema/export, mappings from KAS3 Job/Equipment/Area and inspection/history/media, counts, annual-list rules, IEC library, exception handling, source ownership and client acceptance. | Client supplies a representative sanitized sample plus data dictionary and confirms reconciliation/sign-off owners. No import/backfill is safe before this. |
| Requires RFID/device hardware | Reader SDK/API, HF RFID/NFC/QR support, tag inventory/encoding, NDEF payload, tag lifecycle/pair history, Android/iOS native bridges, offline scan and acceptance criteria. | Hardware make/model, tag type/frequency/protocol, SDK, target devices/OS, sample tags and field test access. Web manual UID entry remains separate. |
| Requires ERP confirmation | ERP/CMMS target, work-order lifecycle and ownership, stable external IDs, API/auth, retry/idempotency, webhook direction, data residency and operational SLA. | Client IT selects system and confirms authoritative fields and test credentials/environment. Do not assume D365 or write an adapter yet. |

## Phase 2A foundation package completed locally

Added unseeded foundation models for Tenant, Contract, Site, tenant membership, separate scoped role grants, Project binding crosswalk, legacy entity mappings and backfill exceptions. The new `TenantProjectBinding` bridge keeps `Project` IDs and schema unchanged. Passive tenant authorization policies and a server-side scope resolver are registered; they fail closed and remain disabled by `TenantFoundation:Enabled=false`. No existing endpoint was opted into tenant enforcement, and authentication/global role behavior was not changed.

Generated migration `20261029123545_Phase2aTenantFoundation` and `PHASE2A_TENANT_FOUNDATION_REVIEW.sql`; neither has been applied. The migration only creates eight tables and indexes/constraints. Foundation checks cover tenant membership, scoped roles, project bindings, ambiguous ownership, missing context and disabled authorization. FR-01 and NFR-10 remain Missing until actual tenant ownership is mapped and enforcement is independently verified.

## Next implementation package

### Package 2A — Additive multi-tenant foundation (implemented locally)

1. **Implemented:** classify route-bearing API controllers with passive `EndpointScopeInventory` metadata and list current route templates/purpose in the design proposal.
2. **Implemented and locally passed:** `--endpoint-scope-checks` discovered and classified 92 API route actions across 10 controllers; backend CI runs it and fails if a route lacks a valid classification.
3. **Implemented:** unseeded tenant tables and bridge/crosswalk records, inactive fail-closed authorization policies, resolver checks and migration SQL safety test.
4. Review and approve open Aries decisions; rehearse the generated additive SQL on an isolated SQL Server copy before applying it anywhere.
5. Do not create implicit tenant IDs, new role claims, global query filters, SQL RLS, business-table tenant columns, backfill or active route enforcement until the corresponding decisions and test coverage are approved.
6. Preserve all current response shapes and screen behavior; Release build and existing backend regression suites pass locally.

The passive route inventory is classification metadata only; it is not an authorization policy. The new primitives also do not prove isolation because existing routes are not enrolled and tenant ownership is unpopulated.

### Package 2B — Tenant ownership, backfill and enforcement (after approval)

Implement `ITenantContext`, active membership resolution, a resource scope service, tenant-aware query helpers and endpoint policies. Add two-tenant fixtures and negative tests for each route family, nested resource IDs, files, aggregates and reports. Keep controller route contracts and existing role workflows intact. The feature flag defaults off in non-production until every endpoint has coverage. This package precedes data backfill and schema enforcement; the proposed migrations in the tenancy document are not part of Package 2B.

## Explicit gates

- Phase 2A metadata source exists locally, but do not apply the migration until Aries approves `Contract`/`Project`, `Site`, role, membership, tenant selection and legacy mapping decisions and isolated SQL rehearsal passes.
- No ownership backfill or tenant enforcement until the approval gates, KAS3 reconciliation inputs and per-route security tests pass.
- No KAS3 backfill until client data inputs and signed reconciliation rules arrive.
- No RFID/device integration before hardware/SDK details and test devices are supplied.
- No ERP integration until the client confirms ERP/CMMS and contract fields.
- No offshore synchronization or production deployment is in this plan.
