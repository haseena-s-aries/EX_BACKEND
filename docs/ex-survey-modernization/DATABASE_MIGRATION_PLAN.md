# Database and Data Migration Plan

**Current state:** 17 EF migrations through `20261029110000_AddOtpVerificationSecurity`. The new migration additively adds `VerificationAttempts` (default 0), `LockedUntil` (nullable) and `ConcurrencyVersion` (default 0) to `RegistrationOtps`. SQL Server script generation was tested without opening a connection. A guarded integration runner now creates a random, explicitly named local test database, applies migrations, checks persisted lockout and SQL rowversion concurrency, then drops only the database it created. The run was attempted but the local SQL Server connection failed before database creation (SSPI principal failure and loopback access denied); therefore actual migration apply, SQL locking and cleanup remain unverified. No database was created, queried or modified by that run. Production was not accessed. Applied production migration state, row counts, data quality, backup health and source export remain unknown.

## Current model families

| Family | Core entities | Concern |
|---|---|---|
| Current project workflow | Project, ProjectArea, AreaEquipment, photos, SurveySession/Answer, Finding, QC review, files, audit and reports | No tenant key; duplicate project/area parent references; one session per equipment. |
| Legacy job workflow | Client, Job, JobAssignment, Area, Equipment, legacy report revision | Overlaps the current project model and may be used by older endpoints/reports. |
| Reference/security | User, CompanyMaster, checklist templates/questions, OTP | Tenant ownership and checklist versioning absent. |

## Source data and acceptance assumptions

Bumi states 5,577 KAS3 items (3,223 for 2026; 2,354 for 2027), 90% Close / 10% Detailed, and 5% Detailed credit per annual list. It requires migration of items, histories, photos and open defects with client acceptance. No KAS3 export/schema or agreed source-of-truth mapping was supplied. The AD50 example is not KAS3 data. Reconfirm counts and rules with the client before loading.

## Safe staged migration

1. **Inventory and protect:** obtain approved source export and schema, code lists, media manifest, database backup, restore proof, data residency and retention decision. Record counts and checksums without changing source.
2. **Map:** approve source→target mapping for tenant/contract/project/site/area/equipment, identifiers, Ex properties, inspection grade, IEC answers, users, photos, open findings, status, dates and audit history. Preserve source IDs and original timestamps.
3. **Target design:** add tenant/ownership keys and constraints additively. Profile current data for cross-project area links and duplicates before enforcing new constraints. Design immutable repeat inspection records and checklist versions before backfill.
4. **Transform:** build repeatable, versioned, idempotent import tooling. Normalize only under signed mapping rules; preserve raw source values and rejected rows for review. Hash every file and retain original name/type/metadata.
5. **Trial:** run in an isolated environment; reconcile item, history, photo, defect, tag and per-status counts. Review sampled records and every rejected/ambiguous row with client/technical authority.
6. **Delta/cutover:** define source freeze or delta capture, rerun safely, verify checksums/counts, obtain acceptance sign-off, and switch read/write path according to approved plan.
7. **Rollback/retention:** retain a tested pre-cutover backup and documented rollback window. Do not delete/overwrite source data until acceptance and retention obligations are met.

## Mandatory reconciliation gates

- 5,577 expected item total reconciles by annual list (3,223 + 2,354), with signed explanation for any approved variance.
- 90/10 inspection allocation and 5% credit calculation independently recalculated and approved.
- Every migrated inspection/history, open defect, photo and source key accounted for; missing/duplicate/orphan counts explicitly signed.
- Spot-check by area, Ex protection, inspection grade, status, date, photo, and defect severity.
- Import rerun produces no duplicates and yields the same hashes/counts.
- Client acceptance pack captures counts, exceptions, signatures, migration version, timestamps and evidence.

## Rollback and caution

Do not add destructive migrations or run migrations against production until source, schema, backup/restore, ownership and acceptance are known. Existing unique `SurveySession.AreaEquipmentId` must be reviewed before repeat inspections are introduced. The current RFID UID index is globally unique; tenant-scoped uniqueness rules need an explicit decision. Migration state is not inferred from source files.

## Phase 2 proposed tenancy migration sequence — not approved or applied

See the full design, compatibility impact, endpoint policy and exception/test plan in `PHASE2_TENANCY_ARCHITECTURE_PROPOSAL.md`. The proposed database sequence is:

1. Baseline schema/routes and restore rehearsal on an isolated copy; approve stable tenant/contract/project/site keys and migration/rollback window.
2. Create `Tenant`, `Contract`, `Site`, `TenantMembership`, `TenantRoleGrant`, `LegacyEntityMap` and `TenantBackfillException`; do not create an implicit catch-all tenant.
3. Add nullable ownership keys (`TenantId`, `ContractId`, `SiteId`) to project and current child/business rows. Preserve all current primary keys, API fields and legacy entities.
4. Backfill crosswalk parent-first; resolve `Project`→`Contract`→`Site`, then area/equipment/session/answer/finding/QC/file/audit. Preserve IDs/history. Quarantine ambiguous/orphan/cross-parent cases; no guessed tenant.
5. Reconcile counts, IDs, FK ancestry, media hashes, survey/QC state and audit records; add composite `(TenantId, Id)` keys and tenant-consistent FKs/indexes only after zero unresolved violations for each scope.
6. Add immutable `InspectionAttempt`, `InspectionAttemptRevision`, answer snapshots, and checklist-version tables. Map existing sessions to attempt 1 while keeping SurveySession and current QC resubmission behavior.
7. Deploy tenant-aware authorization behind route feature flags, complete controller matrix and two-tenant negative tests, then enable scoped reads/writes in a non-production rehearsal.
8. Enforce non-null tenant ownership and optional SQL Server RLS only after signed data reconciliation, clean security tests and a demonstrated rollback/restore. Retire Job/Equipment legacy paths only in a later approved release.

### Phase 2A additive foundation status (generated, not applied)

Migration `20261029123545_Phase2aTenantFoundation` creates eight additive tables: `Tenants`, `TenantContracts`, `TenantProjectBindings`, `TenantSites`, `TenantMemberships`, `TenantRoleGrants`, `LegacyEntityMaps`, and `TenantBackfillExceptions`. It preserves all existing primary keys and project relationships; the new project-binding bridge references `Projects` without adding columns to current business tables. No tenant rows, membership rows, bindings, data updates or automatic ownership assignments are included. The only reference to existing tables is foreign keys from new tables to `Projects` and `Users`.

Review script: `PHASE2A_TENANT_FOUNDATION_REVIEW.sql`. It was generated from the migration and inspected; it contains eight `CREATE TABLE` statements, indexes/constraints, and EF migration-history bookkeeping. It contains no `DROP TABLE`, `DROP COLUMN`, business-table `ALTER`, or ownership `INSERT`/`UPDATE`. The migration `Down` is intentionally forward-only and throws; rollback requires a separately reviewed recovery plan. The script has not been executed against any database.

The schema remains a proposal pending Aries decisions. Do not apply it until the entity semantics, role model, legacy mapping, security review and isolated SQL Server rehearsal are approved. Production execution requires separate authorization.

### Phase 2B rehearsal update

The local `MSSQLSERVER` service is running, but the rehearsal connection fails during encryption negotiation before authentication. The .NET SqlClient strict-encryption loopback check returned SQL error 20 (“this machine does not support” the required encryption). ODBC Driver 17 reports encryption unsupported and no credentials in its security package. LocalDB registry access is denied in this environment. The exact isolated config and non-destructive rehearsal procedure are in `PHASE2B_MIGRATION_REHEARSAL.md`; encryption remains required and certificate validation remains enabled. Therefore migration application, SQL constraint checks, backup/restore and database workflow comparison are unverified and blocked.

`tools/TenantBackfillPreparation` is a required-dry-run JSON snapshot reconciler with mapping/parent validation. Its transaction coordinator is tested with a fake transactional store, including partial-failure rollback and audit pairing. It has no concrete database writer or apply command. No project/legacy rows were read for this run, no TenantBackfillException rows were persisted, and no ownership was applied. Mapping semantics and approval ledger remain subject to the decisions in `OPEN_DECISIONS.md`; see `TENANT_OWNERSHIP_MAPPING_SPEC.md` and `TENANT_BACKFILL_RECONCILIATION.md`.

### Phase 2D additive inspection model (30 October 2026)

`20261030104113_InspectionActivityFoundation.cs` and `20261030104113_InspectionActivityFoundation.review.sql` are generated with EF Core 10.0.13 for review only. They add 12 tables for plans, tasks, checklist snapshots, activity/revisions, checkpoint definitions/results, activity review events, findings, corrective actions/events and evidence references. They do not alter or seed existing `SurveySession`, `SurveyAnswer`, `Finding`, `QcReview`, `SurveyFile`, equipment, project or tenant rows. The existing unique filtered `SurveySession.AreaEquipmentId` index remains unchanged. The EF `Down` drops only these new tables.

EF tooling confirms no pending model changes after scaffolding. `Up` has 12 table creates, 35 indexes, 34 foreign keys and the equipment check constraint; it contains no operations against existing business tables. The review script is generated for the Phase 2A-to-Phase 2D interval only. Do not execute until a trusted TLS isolated SQL Server is available. Rehearse existing migrations + Phase 2A + Phase 2D, verify constraint/index counts and existing workflow queries, then test backup/restore and rollback. No production migration execution is authorized by this document.

### Inspection planning additive migrations (31 October 2026)

`20261031043123_InspectionPlanningScheduling` adds the campaign, annual population, sampling, work package, assignment audit, planning approval and recurrence structures, and adds planning metadata to `InspectionTasks`. `20261031044320_InspectionPlanningLeadAssignment` adds the optional QC Inspection Lead reference to campaigns. `20261031044847_InspectionSamplingCampaignRevision` adds a campaign-revision key to sampling approvals so a new planning revision can obtain a new sample while prior frozen selection history remains intact. `InspectionPlanningScheduling.review.sql` covers this feature interval from the checklist/HAER baseline and is generated for review. No production or local SQL database was used. The scripts were checked for table/column/index drops; the model snapshot reports no pending changes. Apply only in a dedicated rehearsal after backup/restore and workflow regression gates pass.
