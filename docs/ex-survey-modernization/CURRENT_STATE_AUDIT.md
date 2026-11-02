# Ex Asset Management & Inspection Platform — Current-State Audit

**Audit date:** 2026-10-29  
**Verification update:** 2026-10-29, Phase 1 package verification and Phase 2 tenancy design preparation. The original Phase 0 findings remain the baseline; updates below record new local changes and test limits.  
**Repositories reviewed:** `ex-survey` (frontend) and `Ex-SurveyApp-API` (backend).  
**Scope:** read-only Phase 0 review. No application code, database, migration, or deployment was changed as part of the audit.

## Executive summary

The project is an existing React Native/React web client backed by an ASP.NET Core API and SQL Server data model. It already supports account approval, project/area assignment, equipment records, inspection answers/photos, QC decisions, client views, and project reports. It is not yet a multi-tenant KAS3-ready platform: tenant isolation, true offline operation, KAS3-scale import/migration, IEC checklist library, repeat inspection history, full defect lifecycle, and the edge/RFID system described by the architecture are absent or partial.

The architecture PDF supplies 71 functional requirements (FR-01 through FR-94, with gaps in numbering) and 17 NFRs. The separate BRD mentioned in the source brief was not present, so items that need it remain unverified. Bumi’s proposal marks the local edge/RFID scope optional; the Aries architecture proposes it as a Phase 1 core. This needs a formal scope decision before designing that subsystem.

## Repository and architecture inventory

| Area | Observed state |
|---|---|
| Web/mobile client | React 19, React Native 0.87, React Native Web, TypeScript; custom Webpack web target; native Android and iOS folders; separate platform navigation. Not Expo. |
| API | ASP.NET Core 10, EF Core/SQL Server provider 10.0.13, JwtBearer 10.0.13, IdentityModel JWT 8.19.2; MailKit; PDFsharp/SkiaSharp; Swagger only in Development. `global.json` pins SDK 10.0.401. Official NuGet restore and API Release build passed. |
| Persistence | SQL Server configured for normal use; EF InMemory is a Development/test option. API startup does not automatically apply migrations. |
| Delivery/operations | GitHub Actions workflows and Dependabot manifests were added for frontend and API build/test/security checks. No infrastructure-as-code, Docker/deployment definition or coverage gate was found. Workflows have not yet run on GitHub. |
| Versions | Frontend HEAD `5b29639`; API HEAD `b44e1e0`; both on `main` during review. API repo has an unrelated untracked `Ex-SurveyApp_api/appsettings.Production.json`; it was not inspected or modified. |

The API was upgraded from .NET 8 to .NET 10 LTS (Microsoft lists support through November 2028). Microsoft's provider page lists SQL Server 2019+, while its general provider matrix lists SQL Server 2012+ for EF Core 8/9/10. The configured database version is unknown, and this environment could not authenticate to SQL Server. Therefore SQL engine compatibility and real migration behavior remain unverified. [Microsoft .NET support lifecycle](https://learn.microsoft.com/dotnet/core/releases-and-support), [EF Core SQL Server provider](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/), [EF Core provider matrix](https://learn.microsoft.com/en-us/ef/core/providers/).

## User-facing screen inventory

Screens found in `ex-survey/src`: `AdminEquipmentListScreen`, `ClientWorkspace`, `DashboardScreen`, `LoginScreen`, `ManagerQcReviewScreen`, `OtpScreen`, `ProjectDetailsScreen`, `ProjectsScreen`, `QcDashboardScreen`, `QcProjectDetailsScreen`, `ReportsScreen`, `SignupScreen`, `SurveyorAreaDetailsScreen`, `SurveyorDashboardScreen`, `SurveyorEquipmentDetailsScreen`, `SurveyorInspectionScreen`, `SurveyorProjectDetailsScreen`, and `UserManagementScreen`. Web and native routing are maintained separately. This inventory is source-code based; no live end-to-end walkthrough was run.

## Role and workflow inventory

Roles in the API are Admin, Manager, QC, Surveyor, and Client. Registration uses OTP and pending approval; user creation/role management and approval endpoints exist. The intended main path currently represented is: manager/admin project and area setup → surveyor assigned work and equipment inspection → QC review/approve/request changes → client/project reporting and remarks. It is only a partial match for the proposed enterprise workflow: no tenant boundary, campaign/work-pack planning, permit/isolation flow, competence gate, or complete defect closure workflow was found.

## API endpoint families

| Route family | Observed responsibilities |
|---|---|
| `/api/auth` | OTP registration, login/me, user CRUD/role assignment, approvals. |
| `/api/projects` | Project/area CRUD and assignment; area photos, plans, client logos. |
| `/api/surveyor` | Assigned work, equipment pins, RFID UID lookup, inspection CRUD/checklist, photos. |
| `/api/qc` | QC dashboard and review, inspection/photo read, approve/request changes/comments. |
| `/api/manager/qc-review` | Manager QC monitoring/reporting assignments. |
| `/api/client` | Client dashboards, projects, areas, equipment, defects, files, reports, activity, notifications, remarks. |
| `/api/defects` | Admin/Manager defect list and finding-photo read; not a complete defect write/lifecycle API. |
| `/api/reports/projects` | Project report JSON/PDF/assets. |
| `/api/clients`, `/api/companies` | Reference data. |

## Data model and migration inventory

There are 17 EF migrations through `20261029110000_AddOtpVerificationSecurity`. The current model includes `Project`, `ProjectArea`, `AreaEquipment`, photos, checklist templates/questions, `SurveySession`, `SurveyAnswer`, `Finding`, `SurveyFile`, `QcReview`, `ReportRevision`, `AuditLog`, `SyncBatch`, client remarks, and company master data. Registration OTP now stores attempt count, lock expiry and a concurrency version. A parallel legacy family (`Client`, `Job`, `JobAssignment`, `Area`, `Equipment`) remains in the same context and creates ownership/duplication risk.

`AreaEquipment` stores project and area IDs independently; the database does not enforce that the area belongs to the same project. A unique filtered index permits only one survey session per equipment, which prevents repeat inspection history. `SyncBatch` is a table, not an offline sync implementation. No Tenant, Contract, Site, Device, Campaign, WorkPack, tag history, or outbox/sync protocol model was found. Database migration state was not queried.

## Inspection, checklist, defect, RFID, and reports

- Surveyor inspection creates a draft on open, records answers, then uploads photos. There is no offline autosave/resume. Submitted inspection answers can still be changed while QC is InReview; only Approved is locked.
- The API seeds three generic Visual/Close/Detailed templates with four required questions each. This is not the proposed IEC A/B/C checklist library.
- RFID support is a nullable UID field, optional entry in the surveyor area UI (“Scan or enter tag UID”), a globally unique filtered index, and online lookup. There is no scan control, NFC/RFID reader integration, tag lifecycle/history, or offline lookup.
- Findings have statuses, but no full write/lifecycle API, A/B/C fault matrix, immediate hazard alert, or client-only defect closure/reopen flow was evidenced.
- Reports include JSON/PDF and protected assets. No KAS3 HAER pack, XLSX import/export, edge daily report, or validated final handover pack was found.
- Photos are stored on the local filesystem with file checks. Resumable transfer, edge replication, cloud object storage, and disaster-recovery evidence were not found.

## Authentication and security observations

Passwords are hashed. OTPs are four digits, stored with a 10-minute expiry and a 60-second resend cooldown; Phase 1 adds five attempts per issued code, lock through the code expiry, concurrency protection, and an IP fixed-window throttle for anonymous auth endpoints. JWT role/active status is rechecked. SMTP username/password, JWT signing key and Development bootstrap credentials were removed from tracked settings. Runtime secret input now uses environment variables or .NET User Secrets. The owner still needs to rotate live credentials and decide Git-history remediation; see `CREDENTIAL_ROTATION_CHECKLIST.md`. HTTPS redirect is disabled only in Development, and Swagger is Development-only. No tenant isolation/filter/RLS was found.

The bootstrap startup block had a `BootstrapManager:Password` key typo (`BootstrManager:Password`), duplicate Admin pair validation, and no Manager provisioning path. That was corrected in `Services/BootstrapAccountProvisioner.cs`; in-memory regression checks cover partial configuration, normalization, active/approved account creation, hashed password, idempotency, and conflicting roles.

## Test inventory and limits

The frontend has 20 Jest test files. Backend `tests/QcWorkflowChecks` is a custom .NET 10 console check runner using in-memory assertions and opt-in SQL paths, not a conventional xUnit suite. Its mutating SQL repair mode is not invoked by CI. Frontend: 20 suites/75 tests pass; Webpack production build succeeds with Babel configuration and bundle-size warnings; online npm audit reports zero vulnerabilities. API and runner Release builds succeed on .NET 10/EF Core 10.0.13 and the aligned JwtBearer/IdentityModel packages. Bootstrap, OTP, endpoint-scope inventory, migration-script and existing QC/project regression checks pass. A separate guarded SQL integration path is present but could not authenticate to the local SQL Server; it failed before creating a test DB. NuGet package restore passed from the official feed, but the online vulnerability audit was blocked by the configured local proxy/TLS credentials. No production database or deployment was used. GitHub workflows have not run. Android SDK is unavailable locally and iOS requires macOS; native build evidence remains pending CI.

## Source documents and limitations

- `Ex Asset Management & Inspection Platform — Software Scope & Architecture.pdf` (39 pages): requirements, roles/tenant structure, proposed architecture, migration, NFRs and milestones. It contains editorial/source-copy errors and should be corrected before baselining.
- `11 (1).pdf` (53 pages): Bumi technical proposal. KAS3 is stated as 5,577 items (3,223 for 2026; 2,354 for 2027), with 90% Close / 10% Detailed and 5% Detailed credit per annual list. Its Section 4 (p.13) labels local edge/RFID optional. It includes an AD50 sample IEC checklist, not KAS3 source data.
- `EX_Asset_Management_Inspection_03-10-2026.pdf` (31 pages): ToXSL proposal, with differing assumptions (including 4,000 tags, D365, central-server assumptions); hardware supply is excluded/client supplied in the cited scope, while software integration is included.
- The named BRD `Ex_Asset_Management_BRD_v0_1.docx` was not available. No KAS3 source database export/schema was available. Do not treat AD50 sample content as KAS3 data.

## Readiness conclusion

Existing apps are a useful starting point, but not ready to satisfy the stated tenant, offline, KAS3 acceptance, or enterprise security requirements. The bootstrap correction is complete; defer tenant schema, offline protocol, and RFID/edge implementation until the open decisions in `OPEN_DECISIONS.md` are resolved.
