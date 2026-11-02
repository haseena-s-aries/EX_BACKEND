# Phase 2C — Tenant Authorization Preparation

**Status:** Prepared locally; existing endpoints remain unchanged and tenant enforcement is inactive. No database was contacted or migrated for this phase.

## Implemented components

- `TenantResourceKind` now classifies Projects, Areas, Equipment, Survey sessions, Checklist answers, Findings/defects, ProjectArea photos, SurveyFiles, QC reviews, Reports/exports and Client dashboards.
- `TenantScopeResolver` re-reads active membership, tenant lifecycle, resource parents, project bindings and verified legacy crosswalks from the server-side EF context. A requested tenant ID is only a selector. Missing mappings, invalid/inactive membership, inconsistent parents, and conflicting tenant ownership fail closed.
- Child ownership follows stored parents: session → equipment/project or verified legacy mapping; answers/findings/QC → session; files → every populated parent; photos → area/project; report/export → project. Client dashboard ownership is derived from assigned projects and denies projects spanning more than one tenant.
- `TenantScopeAuthorizationHandler` places an authorized tenant context into the current HTTP request only after policy validation. `TenantQueryScope` exposes tenant-filtered query roots for current project data, legacy mapped records, report revisions and client dashboards. It refuses use without a validated request context and excludes conflicting project/crosswalk ownership.
- The existing tenant feature flag remains false by default. No existing controller/action received a tenant policy attribute, and current role/auth behavior is preserved.

## Route planning and permission model

`TENANT_ENDPOINT_ENFORCEMENT_MATRIX.md` lists all 92 discovered API route actions with current role metadata, proposed resource and parent ownership path, collection/resource scope, read/write/export classification, flag and test evidence. It is generated from MVC metadata and method/route naming; body IDs, query composition, nested resources and action semantics still require controller-by-controller review. Do not use this proposed matrix as a substitute for route-specific authorization code review. Shared identity/company reference routes are identified separately.

Before activation, each collection action must query only through `ITenantQueryScope`; each single-resource action must resolve the actual server-side resource and all relevant parent IDs; mutating requests must validate every referenced entity belongs to the same tenant/project; and exports/files must authorize before streaming or generating output. Existing role attributes remain in force as an additional permission gate. Client-provided IDs never establish ownership.

## Test evidence and limits

`--tenant-foundation-checks` now exercises synthetic cross-tenant reads and mutations for project children, inspections, checklist answers, findings, files/photos, QC, reports/exports and dashboards; missing/conflicting ownership; invalid/inactive membership; tenant switching; query-scope filtering; and the disabled-feature-flag path. `--endpoint-scope-checks` verifies passive classifications for all discovered route actions. API and test Release builds, endpoint inventory, tenant foundation/backfill/migration-script checks, OTP security checks, bootstrap checks and the full backend workflow regression runner passed in this run.

All tenant authorization checks use EF InMemory synthetic records. They establish resolver/policy behavior for those fixtures only. They do **not** establish isolation over persisted SQL Server records, prove that every production query is scoped, or validate database constraints. FR-01 and NFR-10 therefore remain Missing.

## Activation prerequisites

1. Approve the hierarchy, membership cardinality, role mapping, legacy ownership model and platform Admin boundaries in `OPEN_DECISIONS.md`.
2. Repair and document encrypted connectivity to a separate isolated SQL Server; rehearse migration, backup and restore there.
3. Review and approve explicit project/contract/site ownership mappings and exception resolution; apply backfill only with separately authorized, transactional tooling.
4. Complete route-by-route controller review and add endpoint-level tests against persisted isolated SQL data, including nested IDs, list filters, files and report exports.
5. Independently verify cross-tenant isolation from persisted records, then approve a staged feature-flag rollout and rollback procedure.

No production data, database settings, routes or feature flags were changed in Phase 2C.
