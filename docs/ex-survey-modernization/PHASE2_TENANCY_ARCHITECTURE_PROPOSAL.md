# Phase 2 Tenancy Architecture Proposal

**Status: design proposal for review; Phase 2A foundation implemented locally, migration unapplied.** Eight additive tenant-foundation tables and inactive authorization primitives are present, but this does not approve the hierarchy or activate tenant ownership/isolation. No database migration has been applied. This proposal uses the Aries Software Scope & Architecture and the Phase 0 repository audit as its baseline. Current screens, APIs, roles and inspection/QC workflow remain unchanged.

## 1. Target hierarchy and ownership

```text
Aries Platform
└── Client Tenant
    └── Contract
        └── Project
            └── Site / Vessel
                └── Area
                    └── Equipment
                        └── Inspection Attempt(s)
```

The platform owns tenant registration, platform operations, feature entitlements and platform audit events. A `Tenant` owns its memberships and business data. Each tenant may have multiple contracts; each contract may include one or more projects; each project may include one or more sites/vessels. `Site` is a new explicit entity. Existing `Project.VesselOrSiteName` is only a possible display-name source; it does not prove that every project has one unambiguous site.

**Decision required:** Aries uses “Contract/Project” as a combined hierarchy step. The proposal separates `Contract` and `Project`, initially mapping each existing project to one provisional contract. Aries/KAS3 must confirm whether this is correct, whether a contract may contain multiple projects and how project/site keys are assigned before the schema is approved.

## 2. Proposed entity mapping

| Existing record/entity | Proposed meaning | Additive treatment and preservation |
|---|---|---|
| `User` | Global login identity | Keep current ID and credentials; tenant access belongs to memberships, not a single global role claim. |
| `UserRole` | Existing global role enum | Keep for backward compatibility during transition; new tenant-scoped roles are separate membership grants. Do not interpret `Admin` as a platform superuser by default. |
| `Client` | Possible tenant/customer account | Preserve rows and IDs. Map only where owner/KAS3 identifies the tenant; do not infer tenant from display name alone. |
| `Project` | Project under a contract | Preserve IDs and API shape. Add nullable `TenantId` and `ContractId`; keep assignment/user fields. Initial one-project/one-contract mapping is provisional. |
| Legacy `Job` | Legacy project/job reference | Preserve until every route and report is inventoried. Link via `LegacyEntityMap`; do not silently merge with `Project`. |
| `ProjectArea` | Area within a project site | Preserve IDs. Add `SiteId` and nullable `TenantId`; retain `ProjectId` during compatibility. Do not assume `Project.VesselOrSiteName` uniquely identifies a site. |
| Legacy `Area` | Legacy area | Preserve; map only through an approved stable source key and project/job context. |
| `AreaEquipment` | Current equipment/asset | Preserve IDs, RFID UID and project/area references. Add nullable `TenantId`; add composite parent constraints after backfill. |
| Legacy `Equipment` | Legacy asset record | Preserve and map only where a stable, reviewed crosswalk exists; no name/tag-only automatic merge. |
| `SurveySession` | Mutable working inspection session | Preserve all current sessions and current Surveyor → QC → resubmission workflow. Link to a new immutable attempt record; keep existing APIs reading the current session during transition. |
| `SurveyAnswer` | Working answers | Preserve IDs and session references. At submit/resubmit, snapshot answer values into immutable attempt revision answers. |
| `Finding` | Defect record tied to inspection | Preserve IDs, lifecycle/status and evidence. Add tenant and attempt linkage only after deterministic session→attempt mapping; maintain existing finding updates and client closure rules. |
| `QcReview` | QC review of an inspection | Preserve IDs, decisions, comments and reviewer identity. Associate with attempt/revision without collapsing existing review history. |
| `AuditLog` | Tenant or platform audit record | Preserve all rows. Add nullable `TenantId` and scope classification. Resolve tenant from the referenced entity where deterministic; keep platform events platform-scoped. Never delete unresolved events. |
| `SurveyFile` and inspection/project media | Tenant-owned evidence | Preserve storage keys and bytes. Add tenant ownership metadata and authorize every read/download through its parent; never expose a file by guessed ID/key alone. |
| Checklist templates/questions | Versioned shared or tenant library | Preserve current templates/questions as draft source. Publish immutable template versions; existing inspection attempts pin the selected version and question snapshot. |
| `RegistrationOtp` | Transient authentication record | Remains global and short-lived; it has no tenant ownership. Existing Phase 1 lockout/concurrency behavior is retained. |

## 3. Proposed schema

### Ownership and roles

- `Tenant(TenantId, TenantKey, DisplayName, Status, CreatedAt, …)`, with unique stable `TenantKey` and lifecycle state. Do not overload the existing CompanyMaster.
- `Contract(ContractId, TenantId, ContractKey, Name, Status, …)` and `Project.ContractId`; keep `Project.Id` unchanged.
- `Site(SiteId, TenantId, ProjectId, SiteKey, Name, SiteType, Location, Status, …)`.
- `TenantMembership(MembershipId, TenantId, UserId, Status, InvitedBy, CreatedAt, …)`, unique `(TenantId, UserId)` while active membership rules are defined.
- `TenantRoleGrant(MembershipId, RoleCode, ScopeType, ScopeId)` for scoped grants such as tenant, contract or project. Role/scope combinations are checked by authorization policy; no role text from a request or stale JWT authorizes access.
- `LegacyEntityMap(SourceSystem, SourceEntityType, SourceId, TargetEntityType, TargetId, MappingStatus, EvidenceRef, …)`, unique on source identity and target mapping. `TenantBackfillException` records unresolved or ambiguous rows without copying personal data into logs.

### Tenant scoping and relational constraints

- Add nullable `TenantId` to tenant-owned business rows first. Keep existing primary keys and FKs in this compatibility stage.
- Add unique parent keys `(TenantId, Id)` and composite FKs: project→contract, area→project/site, equipment→area/project, session/attempt→equipment, answer/finding/review→session or attempt, and file→its owning record. This prevents a child row from pointing across tenants even if application authorization fails.
- For `AreaEquipment`, constrain `(TenantId, ProjectId, AreaId)` to the matching `ProjectArea(TenantId, ProjectId, Id)` so the duplicated project/area identifiers cannot disagree.
- Tenant-scoped identifiers such as project/job number and RFID UID use a tenant-prefixed uniqueness rule after Aries confirms whether values must be globally unique. Existing global RFID uniqueness remains during the first backfill stage.
- Use `TenantId` from the authenticated membership and server-side resource lookup. Ignore/reject a caller-supplied tenant header/body value unless it matches a selected, authorized membership. Platform operations use separately named policies and explicit audited access.
- The initial enforcement is an explicit tenant-scoped query/service boundary with deny-by-default authorization and composite FKs. Evaluate SQL Server Row-Level Security as a second barrier only after connection-pool `SESSION_CONTEXT` set/reset behavior, background jobs and support access have automated tests. RLS does not replace endpoint authorization.

### Repeat inspections and immutable attempts

- Add `InspectionAttempt(AttemptId, TenantId, EquipmentId, AttemptNumber, SurveySessionId, ChecklistVersionId, SupersedesAttemptId, StartedAt, SubmittedAt, FinalizedAt, State, …)`; unique `(TenantId, EquipmentId, AttemptNumber)` and unique `(TenantId, SurveySessionId)`.
- Keep `SurveySession` as the editable operational session required by current routes. A separate attempt record snapshots a distinct inspection of the same equipment. Existing sessions receive attempt number 1 during backfill; they retain their current IDs.
- Add `InspectionAttemptRevision(RevisionId, TenantId, AttemptId, RevisionNumber, SubmittedAt, SubmittedBy, ContentHash, …)` and immutable `InspectionAttemptAnswer` snapshots. A Surveyor resubmission following QC change creates another revision of the same attempt; a later scheduled/repeat inspection creates a new attempt. This preserves both the current QC correction loop and immutable submitted evidence.
- Findings and QC review rows keep their current IDs, gain attempt/revision links, and retain all state/history. Any future “reopen” or close-by-client behavior needs explicit Aries/client policy and must not erase prior review events.
- Database permissions/triggers or application write rules prevent update/delete of submitted snapshot rows. Corrected evidence is appended as a new revision with actor/time and reason; never overwrite a signed/accepted snapshot.

### Checklist versions

- Add `ChecklistTemplateVersion(VersionId, TemplateId, TenantId nullable, VersionNumber, Status, PublishedAt, PublishedBy, ContentHash, …)` and immutable `ChecklistQuestionVersion(QuestionVersionId, VersionId, SourceQuestionId nullable, Code, Text, ResponseType, Unit, LimitsJson, SortOrder, Required, …)`.
- Published versions cannot be edited; edits create a new draft/version. Tenant overlays are explicit derived versions, never mutations to the shared library.
- Each attempt/revision pins `VersionId`; answer snapshots include `QuestionVersionId`, question code/text, response, unit, limits and measured value as submitted. Deleting or renaming a current checklist question cannot change historical inspection evidence.
- Aries technical authority must confirm checklist ownership, IEC revision, supported answer/NA semantics, unit/limit types and tenant override approval.

## 4. Existing-role to proposed permission mapping

This is an approval proposal, not a permission change. A user may have multiple scoped memberships. Endpoint authorization uses role + tenant membership + resource scope + workflow state.

| Existing role | Proposed default grant | Important restriction |
|---|---|---|
| `Admin` | Tenant Administrator for an explicitly assigned tenant | No global/platform privilege by role name alone. Existing cross-project admin paths require resource-by-resource tenant lookup. |
| `Manager` | Contract/Project Manager for assigned scope | May coordinate existing projects and Manager QC monitoring only inside the assigned scope. |
| `Qc` | QC Reviewer for assigned project/contract | Review, request changes and approve only assigned inspections; preserve existing reviewer/history rules. |
| `Surveyor` | Inspector for assigned project/site/work pack | Create/edit work only as allowed by current inspection state and assignment. |
| `Client` | Client Viewer/Defect Responder for assigned tenant/project | Read assigned deliverables; write only client actions explicitly approved by workflow policy. |
| No current equivalent | Platform Operator / Aries Platform Administrator | Separate protected grant, MFA, time-limited access, reason and audit event; never auto-map existing Admin users. |

## 5. Endpoint tenant authorization

Inventory every controller route before implementation. The following policy applies to every request, including nested IDs, media, exports and background execution:

1. Authenticate the caller; resolve active tenant membership and selected role/scope from server-side records.
2. Resolve the resource and its owning tenant server-side. Never authorize using a caller-supplied `TenantId`, project ID alone, or client-side filtering.
3. Require tenant match plus role/scope permission and current workflow permission. Tenant-wide lists start from the caller’s authorized scope query.
4. For nested endpoints (area, equipment, session, answer, finding, QC review, photo, report), verify the entire parent chain belongs to the same tenant and requested parent. Use composite FK/query joins.
5. Apply the same authorization to file/photo/hazard-plan endpoints, report PDFs, exports, search, assignment options, counts, dashboards, notifications, websocket/webhook and future sync routes.
6. Background jobs require an explicit service identity and tenant context; no ambient “all tenant” context. Platform support actions are separate, time-bound and audited.
7. Cross-tenant ID probes return a consistent not-found response or a policy-approved 403 without disclosing row existence. Log only safe actor/resource identifiers and reason codes.

Before applying this policy, produce an endpoint matrix from all controller actions and middleware, with current role, proposed tenant scope, parent resource, read/write effects, workflow state and test case. No endpoint can be declared tenant-secure until the matrix is complete.

### Current controller-route coverage matrix

The following matrix is compiled from the current controller route attributes. The proposed role labels refer to the role mapping above and require approval. Every listed business route resolves a tenant through its resource or tenant membership; route IDs alone are never sufficient. Authentication routes are the only exception because no tenant is selected before login/registration.

| Current route(s) | Proposed scope / permission check |
|---|---|
| `POST /api/auth/register/send-otp`, `verify-otp`, `resend-otp`, `complete`, `login` | Global unauthenticated identity operation; rate-limit and validate account state; never accept tenant ownership from request body. |
| `GET /api/auth/me` | Authenticated caller’s own identity and active membership list only. |
| `POST /api/auth/users`; `GET /api/auth/users`, `/users/approved`, `/users/rejected`; `PUT /api/auth/users/{userId}/role`; `DELETE /api/auth/users/{userId}` | Tenant Admin manages membership/users within the selected tenant; role grant must be valid for its contract/project scope; prevent editing another tenant’s membership or platform grants. |
| `GET /api/auth/approvals/pending`; `PUT /api/auth/approvals/{userId}/approve`, `/reject` | Tenant Admin sees and resolves only tenant-local requests; require explicit tenant join/invitation policy. |
| `GET /api/clients`, `GET /api/clients/{id}`, `POST /api/clients` | Tenant-scoped customer/tenant directory as approved; never return all tenants through a client-name list. Clarify legacy Client vs Tenant before migration. |
| `GET /api/companies`, `GET /api/companies/{id}/logo` | Global curated reference data, or tenant-owned catalog if Aries approves custom companies; global reads expose only explicitly public fields. |
| `POST /api/projects` (JSON/multipart), `GET /api/projects`, `GET /api/projects/assignment-options`, `GET /api/projects/{id}`, `PUT /api/projects/{id}` (JSON/multipart), `DELETE /api/projects/{id}` | Manager/Tenant Admin reads/writes within authorized contract/project; lists and assignment options filter by tenant and scope; project delete retains current cascade/audit protections and is denied if outside scope. |
| `POST /api/projects/{projectId}/areas`, `PUT|DELETE /api/projects/{projectId}/areas/{areaId}` | Manager/Tenant Admin under the verified project; area must belong to the route project and same tenant. |
| `POST|GET /api/projects/{projectId}/areas/{areaId}/photos`, `GET|DELETE /api/projects/{projectId}/areas/{areaId}/photos/{photoId}` | Manager/Surveyor according to current action; validate tenant→project→area→photo chain for upload, list, download and delete. |
| `GET /api/projects/{id}/hazardous-area-plan`, `GET /api/projects/{id}/client-logo` | Read only after tenant/project access; validate stored file ownership and return content only through the authorized project. |
| `GET /api/surveyor/dashboard`, `/projects`, `/projects/{projectId}`, `/projects/{projectId}/areas`, `/projects/{projectId}/areas/{areaId}`, `/projects/{projectId}/areas/{areaId}/equipment` | Surveyor sees assigned project/work-pack scope only; all dashboard counts derive from the same scoped query. Validate parent chain for nested lists. |
| `POST|PUT|DELETE /api/surveyor/projects/{projectId}/areas/{areaId}/equipment[/{equipmentId}]` | Surveyor may manage equipment only if assignment and workflow permit; Tenant Admin/Manager responsibilities require preserving current APIs/roles. Never trust separate project/area IDs unless composite ownership matches. |
| `GET /api/surveyor/equipment/{equipmentId}`, `GET /api/surveyor/equipment/by-rfid/{rfidTagUid}` | Surveyor lookup remains constrained to assigned tenant/project; RFID search is tenant-scoped and handles duplicate UID policy without cross-tenant disclosure. |
| `GET|POST /api/surveyor/equipment/{equipmentId}/inspection`, `PUT /api/surveyor/inspections/{inspectionId}`, `GET /api/surveyor/checklist` | Surveyor reads/creates/edits only assigned attempt; checklist version is available only for tenant/contract scope; edit access follows QC state and creates immutable submitted revisions. |
| `POST|GET|DELETE /api/surveyor/inspections/{inspectionId}/photos[/{photoId}]` | Surveyor assignment + inspection ownership required for upload/read/delete; validate tenant/attempt/photo chain and prevent storage-key access bypass. |
| `GET /api/qc/dashboard`, `/projects`, `/projects/{projectId}`, `/projects/{projectId}/areas/{areaId}`, `/equipment/{equipmentId}/inspection`, `/inspections/{inspectionId}/photos/{photoId}` | QC reviewer sees assigned project scope; Manager and Admin continue current QC access but are further narrowed to their approved tenant/contract; dashboard and media follow same rule. |
| `POST /api/qc/inspections/{inspectionId}/start-review`, `/approve`, `/request-changes`; `PUT /api/qc/inspections/{inspectionId}/comments` | Authorized QC Reviewer or Manager only for the scoped inspection and allowed workflow state; preserve existing actor, comment and review history. |
| `GET /api/manager/qc-review/dashboard`, `/projects`, `/projects/{projectId}`, `/projects/{projectId}/areas/{areaId}`, `/equipment/{equipmentId}`, `/inspections/{inspectionId}/photos/{photoId}` | Manager sees only projects in their tenant/contract responsibility; Admin is also tenant scoped unless separately granted platform support. Validate nested inspection/photo ancestry. |
| `GET /api/manager/qc-review/reporting-managers`, `PUT /api/manager/qc-review/users/{qcId}/manager` | Tenant Admin manages only tenant-local QC/reporting-manager assignments; do not let reassignment grant cross-tenant inspection access. |
| `GET /api/defects`, `GET /api/defects/{findingId}/photos/{photoId}` | Manager/Tenant Admin defect register only within authorized scope; every photo is checked against finding and tenant. |
| `GET /api/client/dashboard`, `/projects`, `/projects/{projectId}`, `/projects/{projectId}/areas`, `/areas/{areaId}/equipment`, `/equipment/{equipmentId}` | Client reads only projects explicitly assigned to its tenant/membership; nested IDs must resolve back to an assigned project. |
| `GET /api/client/defects`, `/projects/{projectId}/defects`, `/files/{fileId}`, `/reports`, `/projects/{projectId}/reports`, `/activity`, `/notifications` | Client sees tenant/project-scoped defects, files, reports, activity and notifications; prevent aggregate counts or links from revealing another tenant. |
| `GET /api/client/projects/{projectId}/remarks`, `POST /api/client/remarks` | Client can view/post only for assigned tenant/project and approved remark workflow; no client-side tenant assignment. |
| `GET /api/reports/projects`, `/projects/{projectId}`, `/projects/{projectId}/pdf`, `/projects/{projectId}/assets/{key}` | Existing permitted roles retain report access only in their assigned scope; authorize project, PDF and every referenced asset independently. Never use storage key as authorization. |

This route matrix is a design baseline, not evidence that all current endpoints are already tenant-secure. Implementation must add an automated test per route (or shared route-policy group with representative nested-ID cases) and fail closed for any newly added route without an explicit scope policy.

## 6. Additive migration sequence — proposal only

This sequence describes future migrations; none has been created or applied. Migrations must be small, reversible where possible and rehearsal-tested against a restored, isolated copy after explicit approval.

| Step | Proposed migration / work | Gate and compatibility impact |
|---|---|---|
| 0 | Freeze approved schema version; inventory every current route, model, index, FK, trigger and legacy read/write path; capture backup/restore evidence and pre-migration counts on a non-production copy. | Aries/KAS3 owner approves entity semantics, source IDs, privacy/retention and rollback window. |
| 1 | Create `Tenant`, `Contract`, `Site`, `TenantMembership`, `TenantRoleGrant`, `LegacyEntityMap`, and `TenantBackfillException`; seed no implicit/global tenant. | Additive tables; existing API remains unchanged. |
| 2 | Add nullable `TenantId` to `Project`, `ProjectArea`, `AreaEquipment`, `SurveySession`, answer, Finding, QcReview, AuditLog, project/inspection file metadata, pin/assignment records and any other business-owned child found by route/model inventory. Add nullable `ContractId` to `Project`, `SiteId` to `ProjectArea`; do not add tenant keys to users, OTPs or global reference data without an ownership decision. | Expand schema only; old binaries can continue if columns are nullable and no new FK blocks existing writes. |
| 3 | Populate approved source→target crosswalk and tenant membership rows. Backfill project/contract/site mappings, then area/equipment, sessions/answers/findings/QC/files/audit in parent order. Keep original IDs, timestamps, values and source keys. | Dry-run first. Unknown, multi-tenant, orphan, conflicting project/area, duplicate site-name or unmappable legacy Job/Equipment rows go to exception report; never guess a tenant. |
| 4 | Reconcile counts and ownership; add indexes and composite unique keys/FKs as `NOT VALID` equivalent is unavailable in SQL Server, so validate orphan/cross-tenant counts before adding each trusted constraint. Add supporting indexes online/resumable where edition/version permits. | Any unresolved row blocks the affected constraint and cutover; no silent “default tenant” fill. |
| 5 | Add immutable checklist versions, `InspectionAttempt`, `InspectionAttemptRevision`, answer snapshots and attempt links. Backfill each existing `SurveySession` as attempt 1 and snapshot only from verified saved data. Retain existing session IDs and expose compatibility reads. | Ensure old QC resubmission behavior maps to attempt revision; add new repeat attempts behind a feature flag. |
| 6 | Deploy tenant-aware authorization/services and endpoint matrix tests with existing APIs/screens unchanged. Initially compare old and scoped query counts in non-production; deny unmatched tenant scopes. Add feature flags for per-route enforcement and tenant selection. | Cross-tenant tests and existing role/workflow suite pass; no dual-write between competing legacy/new entity trees. |
| 7 | Enforce `TenantId NOT NULL`, composite FKs, tenant-scoped unique rules and optional SQL RLS only after zero-exception reconciliation, full CI, restore rehearsal and owner approval. | Backward compatibility becomes constrained; coordinate API/client deployment and rollback period. |
| 8 | Retire `Job`/legacy `Equipment` paths only after API/client inventory, KAS3 key reconciliation and a separately approved deprecation. Keep history and compatibility views/read adapters during the agreed retention window. | No destructive drop until client acceptance, backup restore test, retention review and rollback expiry. |

## 7. Backfill and exception handling

- Use deterministic mapping keys from Aries/KAS3 source IDs, approved project/job keys and retained current IDs. Names, email domains, vessel labels and RFID text are not sufficient ownership keys by themselves.
- Derive child tenant only from a validated parent chain. Compare `AreaEquipment.ProjectId` with `ProjectArea.ProjectId`; block rows where the two disagree. Then validate each session’s equipment/project and each answer/finding/QC/media parent.
- Keep a durable, access-controlled exception table/report with source entity/key, reason code, resolution status and reviewer evidence reference; avoid secret or sensitive user data in CI output.
- Reconcile row counts, distinct IDs, FK failures, per-tenant totals, attachment hashes/metadata, current survey/QC state, audit events and legacy mappings before/after each trial.
- Run imports idempotently using a migration batch ID and unique source identity. A rerun must update only staging/progress state, not duplicate business records.
- Do not import KAS3 data until the client provides schema/export, mapping, data dictionary, sample files/media, source IDs, retention/consent rules and signed acceptance counts.

## 8. Cross-tenant security regression suite

Required automated coverage before enforcing isolation:

- Two tenants with duplicate-looking project/equipment IDs (using separate fixtures/keys) cannot list, read, update, delete, assign, search, report or count one another’s data.
- Try valid Tenant A token with Tenant B route IDs for projects, areas, equipment, inspection, answer, finding, QC review, photo, plans, reports and exports; verify no data or existence leak.
- Spoof `TenantId` in query, path, header, JSON body and JWT claims; only active membership and resource ownership decide scope.
- Exercise all five existing roles, multi-membership switching, revoked/disabled membership, stale token, unassigned project and platform role separation.
- Attempt cross-tenant composite-FK writes directly through EF/SQL. Test no tenant context and pooled connection reuse with RLS if selected.
- Check tenant isolation on dashboard totals, QC queue, defect aging, assignment options, notifications, audit history, background jobs and file storage lookup.
- Verify existing inspection create/edit, QC start/comment/request changes/approve, Surveyor correction/resubmission, findings and project deletion behavior remains unchanged within a tenant.
- Test concurrent attempt-number creation, revision sequencing, immutable snapshot update/delete rejection and idempotent backfill rerun.

## 9. Rollback and recovery

- Keep migrations additive and versioned. Do not drop or rename legacy tables/columns during rollout. Keep a pre-change full backup plus transaction-log backup and demonstrate restoration into an isolated database.
- Before the tenant-enforcement flag, rollback means disable the new scoped path and keep the additive nullable schema/backfill. Do not delete new rows or crosswalks.
- After scoped reads/writes start, preserve all new attempts/snapshots. Rollback the API to compatibility routes only if the old route can safely read the updated data; otherwise freeze writes and restore to a defined point with owner approval.
- If a backfill/constraint fails, stop at the last validated batch, retain the exception manifest, correct mapping in staging, and rerun idempotently. Never drop a partially populated tenant key to “fix” an inconsistency.
- Before any destructive cleanup, require a signed cutover and recovery plan, verified restore, owner/client acceptance, and a time-limited rollback window. Production execution requires separate explicit authorization.

## 10. Compatibility-impact assessment

| Area | Expected impact | Mitigation / release gate |
|---|---|---|
| Current Project/Area/Equipment screens and API payloads | IDs and current DTOs can remain stable; adding required tenant IDs directly to clients would break old clients. | Derive scope server-side; keep request/response contracts stable and make tenant selection explicit only after identity design approval. |
| Surveyor and QC workflow | Current mutable work session and resubmission loop cannot be replaced with a one-shot immutable row. | Keep `SurveySession`; add immutable attempt/revision snapshots as an additive sidecar. Existing QC regression suite is a hard gate. |
| Admin behavior | Existing Admin may see many projects. Treating Admin as global platform admin would create a privilege expansion. | Map users to tenant admin only after tenant assignment; create a new platform operator grant with separate review/audit. |
| Legacy Job/Equipment APIs/reports | May overlap current model and carry untracked data. | Inventory routes/usage; map with crosswalk and compatibility read adapter; no automatic merge or deletion. |
| Data model/indexes | Added FKs/indexes may increase write cost and make current inconsistent rows fail. | Profile data, fix exceptions, add constraints incrementally; measure query plans and write duration on restored test copy. |
| Mobile/web clients | Existing workflows must continue without code changes for first schema expansion. | Use additive nullable columns; API owns tenant resolution; feature-flag enforcement; web/mobile regression and end-to-end acceptance. |
| SQL Server RLS | Requires correct session context across pooled connections and jobs; misconfiguration can deny legitimate work or leak rows. | Optional defense in depth only; explicit pool reset, unset-context deny behavior and integration tests before enabling. |
| Runtime/database support | .NET 10 is LTS through November 2028. Microsoft's SQL Server provider page currently lists SQL Server 2019+, while its general provider matrix lists SQL Server 2012+ for EF Core 8/9/10. The actual configured database engine/version is not established, and SQL Server connection tests failed before a session. | Resolve the official documentation minimum-version difference with the DBA against the actual SQL Server product/compatibility level; test `Migrate` and representative queries against an isolated copy before tenant rollout. Do not treat NuGet restore or SQL-script generation as database compatibility evidence. |

## 11. Approval inputs

Before any Phase 2 schema implementation, obtain decisions for: (1) Contract vs Project cardinality and stable keys; (2) tenant provisioning and cross-tenant user membership; (3) role mapping and platform support model; (4) site/vessel identity and the meaning of current `VesselOrSiteName`; (5) legacy Job/Equipment ownership mapping; (6) global vs tenant-scoped RFID/project identifiers; (7) checklist authority/version and submitted-evidence retention; (8) SQL RLS selection; (9) data residency, audit retention and delete/restore policy; (10) KAS3 source schema/export and client reconciliation/sign-off; and (11) rollback window and non-production migration rehearsal owner.

**Recommended first implementation package after approval:** tenant authorization foundation only: `ITenantContext`/membership resolver, explicit resource-scope service, controller endpoint matrix, test fixtures with two tenants and cross-tenant denial tests. Keep all tenancy DDL behind the approved additive migration plan; preserve current route contracts and inspection/QC flows. Do not begin offshore synchronization or production rollout.

### Phase 2A implementation update

Foundation models/configurations, `TenantScopeResolver`, fail-closed authorization policies, the disabled feature flag and isolated checks are now implemented locally. The generated review SQL creates only additive foundation tables and is unapplied. Project ownership is represented by a bridge to the existing `Project` table; multiple tenant bindings remain representable so the resolver can deny ambiguous ownership. No current endpoint carries tenant enforcement metadata. After architecture approval, review/rehearse the migration on isolated SQL Server, then design deterministic membership and ownership backfill from approved Aries/KAS3 inputs. Keep route enforcement, business-table tenant columns, offshore synchronization and production rollout gated.

### Phase 2B preparation update

Added a dry-run JSON snapshot reconciler and an atomic transaction coordinator contract. The utility validates explicit reviewer-approved Project→Tenant/Contract/Site mapping, duplicate/conflicting ownership, missing mapping, contract/site consistency, orphan active areas/equipment and cross-project area/equipment links. It preserves source IDs and emits issues without guessing from names. Synthetic tests exercise idempotent re-runs and rollback/audit atomicity. No concrete EF write adapter or API mapping endpoint exists; approval capture needs a durable review ledger and approved audit design. The SQL rehearsal is blocked before SQL login by the local encryption/SSPI provider issue described in `PHASE2B_MIGRATION_REHEARSAL.md`; no database migration or backfill was applied.
