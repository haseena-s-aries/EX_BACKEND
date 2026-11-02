# Requirement Traceability Matrix

**Baseline:** Aries *Ex Asset Management & Inspection Platform — Software Scope & Architecture* (28 Oct 2026), checked against source in both repositories. Status means evidenced in this source snapshot, not deployed or independently tested. The referenced BRD was unavailable. Phase 1 evidence follows. `PI` = Partially Implemented, `M` = Missing, `U` = Unverified, `B` = Blocked pending decision.

## Phase 1 evidence

- API target/runtime upgraded to .NET 10; EF Core and SQL Server provider are 10.0.13; JwtBearer is 10.0.13 with IdentityModel JWT packages aligned to 8.19.2. Official NuGet restore and API Release build passed. The current NuGet pages list 10.0.14/8.23.0 stable patches; online restore/audit could not validate them because of local proxy/TLS authentication failure.
- OTP verification permits five attempts per issued code, locks through expiry, resets on reissue, rejects stale concurrent writes, and anonymous auth endpoints have a 20-request/5-minute IP fixed-window throttle.
- Tracked settings no longer provide SMTP username/password, JWT signing key or Development bootstrap credentials. Owner rotation/history remediation remains outstanding.
- Local key-pattern scan found no credential-like assignment in tracked or non-ignored new text files (values suppressed). Gitleaks is not installed locally; GitHub secret-scan workflow has not run. An ignored local production JSON was not opened; generated copies in API/test build and publish folders were removed, and API/test .NET 10 Release output was verified absent after adding build/publish exclusion.
- Frontend/API GitHub Actions, secret scan, dependency review, Dependabot and automated security/migration checks were added. GitHub has not run these workflows yet.
- Local evidence: 20/20 Jest suites (75 tests), web production build, API/test runner Release builds, bootstrap/OTP checks, existing backend regression suite, OTP migration SQL script generation, official NuGet restore, and online NPM audit (0 vulnerabilities) passed. The isolated SQL integration harness is present but could not connect before database creation (Windows SSPI / local access denied); therefore SQL apply/locking is unverified. NuGet vulnerability refresh was blocked by the configured proxy; direct `System.Security.Cryptography.Pkcs` is 10.0.13.
- Phase 2A: additive tenant foundation models, configurations, an unexecuted migration/review SQL, inactive authorization policies and isolated foundation checks are implemented locally. No ownership rows/backfill or existing-route enforcement is active; architecture decisions remain open. FR-01 and NFR-10 remain Missing.
- Phase 2B preparation: strict-encryption local SQL diagnostics, isolated rehearsal procedure, dry-run mapping reconciler, approval-gated transaction coordinator and synthetic mapping/rollback tests are implemented. No SQL rehearsal or application-data reconciliation completed because the local client TLS/SSPI provider fails before login; no mapping writes occurred. FR-01/NFR-10 remain Missing.
- Schema-neutral Phase 2 package: all 10 API controllers now carry passive endpoint-scope inventory metadata; `--endpoint-scope-checks` asserts all MVC route actions are classified and is included in backend CI. This inventory does not enforce tenant authorization.
- Phase 2C preparation: fail-closed server-side ownership resolution and validated tenant request context now cover the classified tenant resource kinds; `ITenantQueryScope` supplies tenant-filtered query roots; the feature flag remains disabled and no existing route has tenant enforcement attached. Synthetic tests passed for cross-tenant resource reads/mutations, missing/conflicting ownership, membership state/switching, and selected files/report/dashboard paths. The generated route matrix contains 92 actions. API and test Release builds, tenant/backfill/migration-script checks, OTP security checks, bootstrap checks and the full backend regression runner passed. SQL/TLS findings are recorded separately; no isolated encrypted SQL database was available, so no persisted isolation or migration evidence exists.
- SafeEx comparison intake: the public SafeEx Cloud page and 2024 “SafeEx Cloud and Integrability” PDF were reviewed, and the supplied MODEC activities report was visually reviewed (7 pages). The report shows two activities on equipment SWE-50528, separate activity acceptance and checkpoint state, correction text/evidence, equipment metadata and filters. Public/vendor material adds the Equipment, Plan/Plan Task, Checklist/Check, Activity, Response, Response Condition and Response Correction vocabulary. This does not establish universal labels, KAS3 approval rules, exact private SafeEx screens or availability in a given SafeEx version. The WhatsApp ZIPs, sample checklist and current BRD remain unavailable; no official Aries FR/NFR status changed. `SAFEEX_BRD_CHANGE_PROPOSAL.md` is a review draft, not a revision of the absent signed BRD.
- Checklist/HAER sprint (30 Oct 2026): checklist admin API/UI, technical publish authorization, draft-to-published version lifecycle, equipment-grade/protection applicability and immutable snapshots are implemented. Existing templates migrate to Draft; historical section and response-scale values remain null until explicitly classified. Synthetic tests cover lifecycle, authorization, applicability, historical-answer guard and prior snapshots. HAER metadata editing is project-scoped; JSON/CSV/PDF/Excel-compatible exports include metadata availability, per-equipment activity history and outstanding defect states. API HTTP PDF/Excel export tests and synthetic report tests pass. Migration and review SQL are generated and model snapshot has no pending changes; none is applied. Full frontend Jest: 23 suites/83 tests. API Debug and isolated-output Release builds and the full backend regression runner passed. EF InMemory and HTTP test fixtures are synthetic; persisted SQL, live browser workflow, client-approved report template and approved IEC content remain unverified. Repository-wide `tsc --noEmit` still fails on pre-existing DOM globals, backup folder imports and older navigation/mobile typing errors; no errors were reported in the new checklist/admin/report screen or API type files on the filtered check.

| ID | Requirement (short form) | Status | Evidence / gap |
|---|---|---|---|
| FR-01 | Tenant/contract/site hierarchy and isolation | M | Foundation tables/models, server-side resolver/query-scope primitives and a 92-action enforcement plan exist. Feature flag remains disabled; no route enforcement, tenant backfill or persisted-database isolation proof. |
| FR-02 | Product roles, invite/deactivate | PI | Five roles, user administration and approvals; no tenant-scoped role assignment established. |
| FR-03 | Entra ID + MFA | M | Local JWT/password/OTP flow; no Entra integration or MFA policy evidenced. |
| FR-04 | Approved devices | M | No device registry/approval enforcement found. |
| FR-05 | Offline credentials and PIN | M | No offline credential/PIN path. |
| FR-06 | Inspector competence checks | M | No competence/certification gate found. |
| FR-10 | Area/location classification | PI | Projects, areas and equipment pins exist; required Ex hierarchy/classifications incomplete. |
| FR-11 | Full HAER Ex attributes | PI | Additive equipment metadata now covers description/model/serial, Ex protection, gas/temperature/zone/ambient/IP, certificate/notified body/special conditions, circuit and RFID identification. Admin/Manager can edit through a project-scoped API. Missing values are Unknown unless explicitly marked Not Applicable. Synthetic tests only; metadata completeness/client acceptance unverified. |
| FR-12 | Equipment suitability assessment | M | No suitability workflow/model found. |
| FR-13 | Annual lists and history | PI | Campaigns and annual equipment lists retain existing AreaEquipment IDs and planning revisions. KAS3 register reconciliation and persisted SQL verification remain outstanding. |
| FR-14 | Documents available offline | M | Online protected files; no offline package. |
| FR-15 | Data reconciliation | M | No reconciliation workflow. |
| FR-16 | PDF plans with pins | PI | Plans/pins exist; full verified annotation/export flow not established. |
| FR-17 | CSV/XLSX import | M | No evidenced import/export workflow. |
| FR-20 | HF RFID/NFC/QR/manual identification | PI | Inspector scan/QR/manual UI, scanner abstraction, simulated lookup and registration/replacement APIs exist. Physical HF reader compatibility and supported Android NFC hardware are not verified. |
| FR-21 | Offline scan | PI | Downloaded account-scoped equipment/tag mappings support offline resolution and ambiguous-tag denial; device durability and sync remain unverified on Android. |
| FR-22 | Tag pairing history | PI | Active tag assignments, deactivation/replacement and immutable assignment history are implemented in API/UI; only synthetic checks exist without current SQL persistence evidence. |
| FR-23 | Wrong/unregistered tag warning and manual flag | PI | Unknown, duplicate, inactive and conflicting identifiers are surfaced; manual lookup is available. Live reader behavior is unverified. |
| FR-24 | Tag inventory | M | No tag stock/assignment inventory. |
| FR-25 | NDEF payload | M | No NDEF encoding/validation. |
| FR-26 | Coverage reporting | M | No tag coverage report. |
| FR-30 | Inspection grades | PI | Visual/Close/Detailed activities can be started from the surveyor equipment history screen and are stored as distinct attempts. Client-approved grade rules/checklist mappings are not established. |
| FR-31 | 90/10 allocation and 5% credit | PI | Per-campaign Close/Detailed/minimum credit percentages and rounding rules are configurable and approval-gated; synthetic examples pass. No KAS3 values are hardcoded or reconciled and final client rules remain unapproved. |
| FR-32 | Sampling selection | PI | Seeded random, scored risk, equipment-type round-robin and justified manual sample previews are implemented. Campaign-assigned active/approved QC Inspection Lead approval freezes samples; Admin override is implemented. Business approval of the role/override policy, risk rules and persisted data remain outstanding. |
| FR-33 | Progress and credit | PI | Campaign dashboards separate completed, submitted and QC-approved activity, show unique-equipment grade counts, annual detailed credit, pending/overdue/inaccessible, defects, rotations and productivity. A manager workload projection now summarizes open, overdue, inaccessible and completed tasks by assigned inspector. Compliance is not inferred from QC approval. SQL-backed actual counts remain unverified. |
| FR-34 | Work packs/routes | PI | Campaign-linked work packages/tasks include site/area/rotation, equipment, grade, route order, assignment and due date. A campaign agenda now sorts scheduled tasks and identifies overdue/inaccessible work. Multi-area package generation, route optimization and live database validation remain. |
| FR-35 | Permit/isolation tracking | PI | Permit and isolation prerequisite fields persist on tasks; permit approval/clearance enforcement is not implemented. |
| FR-36 | Recurring inspection schedule | PI | Future recurrence definitions persist with interval, grade and next date. Automatic occurrence generation and duplicate-safe task materialization remain. |
| FR-37 | Inaccessible item carry-forward | PI | Authorized Surveyor/Manager can mark a task inaccessible with a required reason and carry-forward status. SQL-backed history and future task materialization remain unverified. |
| FR-40 | IEC A/B/C checklist library | M | Checklist authoring supports sections and applicability, but no controlled IEC A1-A23/B1-B33/C1-C3 content was supplied or fabricated. Only approved client/technical source content may be published. |
| FR-41 | Pass/Fail/NA, numeric limits and defects | PI | Checklist drafts configure Pass/Fail/NA, required fail remarks, measurement units and approved-source numeric limits; activity UI/API records observations/readings. Full defect classification/lifecycle and authoritative technical content remain incomplete. Synthetic checks only. |
| FR-42 | Checklist versioning | PI | Draft revisions use optimistic concurrency; separate Qc/Admin technical approval publishes an immutable version; new versions clone and archives retain prior snapshots. Synthetic lifecycle/snapshot tests pass. No persisted SQL rehearsal for this sprint. |
| FR-43 | Tenant checklist overlays | M | No tenant model/overlay. |
| FR-44 | Inspector signature | M | No signature capture/evidence found. |
| FR-45 | Downloadable offline work package | PI | Versioned offline package now carries the existing project hazardous-area plan and area reference images with revision, size, SHA-256 and assignment-scoped download URLs. Android source encrypts files with Android Keystore AES-GCM; required documents gate package readiness. Equipment certificate files and document-admin associations are not modeled. Synthetic API/Jest tests pass; Android device and persisted database behavior remain unverified. |
| FR-46 | Scan card workflow | M | No scan-card flow. |
| FR-47 | Guided route and skip reason | PI | Assigned project/area navigation exists; no route/mandatory skip-reason policy. |
| FR-48 | Photo metadata/compression/annotation | PI | Native camera/gallery and web file picker, authenticated evidence display, size/type validation and activity/checkpoint/uploader/time association are implemented. Offline photo queue code and synthetic recovery tests exist; no Android device test or live browser upload/retrieval was completed in the integration acceptance run. Compression and annotation remain absent. |
| FR-49 | Autosave/resume | PI | Online activity drafts and offline local draft/outbox code exist; Jest tests cover restart-style recovery, account switching and retry/conflict behavior. The previous browser draft check and current tests do not establish SQL-backed persistence or Android restart durability. |
| FR-50 | Defect lifecycle | PI | Activity finding/corrective-action APIs support rectification, evidence, Aries verification and client decisions. Inspector/Aries screens and client workspace closure/rejection/reopen controls exist; QC acceptance does not close a finding. Synthetic UI/service/security tests pass. Live persisted workflow remains unverified. |
| FR-51 | Fault library | M | No fault library. |
| FR-52 | A/B/C severity matrix | M | QC severity does not implement fault A/B/C matrix. |
| FR-53 | Immediate hazard alert | M | No immediate alert workflow found. |
| FR-54 | Aging defect register | PI | Defects can be listed; aging/SLA register not evidenced. |
| FR-55 | Client-only closure and reopen | PI | API restricts decisions to the assigned project client and requires Aries verification first; the client workspace exposes accept, reject and reopen actions with comments. Synthetic tests cover role denial and state history; live SQL/browser behavior remains unverified. |
| FR-56 | Optional ERP work order | M | No ERP/work-order integration. |
| FR-60 | Tablet-edge-central architecture | M | Current API is central; no tablet edge node/sync service. |
| FR-61 | Resumable media transfer | M | Basic local file upload; no resumable transfer. |
| FR-62 | Sync status | PI | Offline client exposes pending/synced/failed state and an idempotent sync API with receipt checks. Synthetic client/API tests pass; device behavior, persistent SQL receipts and live reconnection remain unverified. |
| FR-63 | 28-day edge operation | M | No edge storage/service. |
| FR-64 | MDM and OTA | M | No MDM/managed update integration. |
| FR-70 | HAER deliverable | PI | HAER register supports required equipment metadata, status/reconciliation, area filtering, Unknown/N/A distinction, activity history and outstanding defect states; scoped JSON/CSV/PDF/Excel-compatible SpreadsheetML `.xls` exports include report/client/revision/generated metadata. Backend synthetic export tests pass. Client branding acceptance and persisted SQL validation remain. |
| FR-71 | Before/after defect recommendation | M | No specified final defect rec report. |
| FR-72 | Per-item IEC record | PI | Per-equipment HAER history lists grade/date/status/result per attempt; activity screen retains checklist snapshots, numeric/checkpoint revisions and QC decision history. No approved IEC library or validated per-item report pack is claimed. |
| FR-73 | Daily edge report | M | No edge or daily edge report. |
| FR-74 | Final handover pack | PI | Consolidated handover ZIP and checksum manifest generation exist; archived package versions remain Draft and synthetic tests validate ZIP contents/checksums. This integration run did not generate/retrieve one through a live SQL-backed session; final handover approval is not implemented. |
| FR-75 | Dashboards | PI | Role dashboards exist; not all tenant/KAS3 metrics. |
| FR-76 | Tenant report templates | M | No tenant-scoped report template model. |
| FR-77 | Handover workflow | M | No formal handover/acceptance workflow. |
| FR-80 | Backup/restore | U | No infrastructure or restore evidence in repos; operational configuration unknown. |
| FR-81 | Source-system mapping | M | No approved source mapping. |
| FR-82 | Trial migration | M | No migration tooling/evidence. |
| FR-83 | Items/history/photos/open defects migration | M | No source KAS3 data/export or migration process. |
| FR-84 | Idempotent and delta migration | M | No migration pipeline. |
| FR-85 | Client acceptance pack | M | No reconciliation/acceptance pack. |
| FR-86 | Open-format export | PI | JSON/PDF reports; open-format full data export not evidenced. |
| FR-90 | ERP integration | M | No ERP connector. |
| FR-91 | Retry handling | U | No integration retry queue; transient handling for external connector unknown. |
| FR-92 | OpenAPI | PI | Swashbuckle configured in Development; production API publication/versioning not evidenced. |
| FR-93 | Webhooks | M | No webhook delivery/subscription system. |
| FR-94 | CMMS export | M | No CMMS export. |

## Proposed SafeEx comparison requirements (unapproved)

The following are change candidates from the user's requested scope, not confirmed SafeEx behaviors or new contract IDs. They map to existing Aries IDs and remain unapproved pending reference artifact review and business decisions.

| Proposal | Aries overlap | Current evidence / gap | Status |
|---|---|---|---|
| Multiple immutable inspection activities per equipment | FR-30, FR-33, FR-36, FR-72; NFR-05 | Existing `SurveySession` has a unique filtered index on `AreaEquipmentId`; no activity/revision entity. | Proposed; SafeEx evidence pending |
| Versioned checkpoint results, applicability, evidence and review state | FR-40, FR-41, FR-42, FR-48, FR-72 | Answers are free text/comment; no stable question code/version, applicability, timestamp, explicit evidence join or immutable template snapshot. | Proposed; technical approval required |
| Independent technical condition and workflow status | FR-41, FR-50, FR-52, FR-55 | No checkpoint condition dimension; status may be confused with technical result. | Proposed; condition vocabulary/authority pending |
| Corrective-action aggregate with client-only final closure and event history | FR-50, FR-53, FR-54, FR-55, FR-56 | Finding supports partial status/recommendation only; no complete action, evidence pairing, verification/client decision/reopen history. | Proposed; KAS3/QC approval required |
| SAP/Ex metadata crosswalk without live connector | FR-11, FR-12, FR-20, FR-81, FR-90 | Some Ex/RFID attributes exist, but SAP location/group and approved external mappings do not. | Proposed; ERP and technical validation required |
| Per-equipment activity PDF and campaign export | FR-33, FR-70, FR-71, FR-75, FR-76, FR-86; NFR-03 | Existing general project PDF/JSON; no activity-history/campaign format. | Proposed; business/report approval required |

### Sprint 1 and Sprint 2 delivery evidence

- The additive Phase 2A/2D and Sprint 2 migrations, plus the unused nullable shadow-FK cleanup, were applied in sequence only to the synthetic LocalDB database `ExSurveySprint2_Test`. The final EF snapshot has no pending model changes. The rehearsal verified 13 activity tables, 37 foreign keys, 50 indexes, task consistency constraint, preservation of the legacy `SurveySession` unique index, and backup/restore.
- A local web browser ran the surveyor-to-QC flow against the persisted database: start Close; save Pass/Fail, an out-of-range configured reading and correction; submit; QC return with comments; resubmit; approve; confirm the accepted record did not change after an edit attempt; start a second Visual; and verify both history rows. SQL queries confirmed the persisted attempts, checkpoint revisions and review decisions.
- Numeric settings use checklist-approved unit/range values; no technical threshold was invented. Activity approval did not close findings. The implementation does not activate tenant enforcement.
- An explicit `ExSurveySprint2LocalDB` development launch profile targets only `(localdb)\\ExSurveySprint2;Database=ExSurveySprint2_Test`, sets `InspectionActivities:Enabled=true`, and leaves `TenantFoundation:Enabled=false`. Shared defaults remain disabled.
- The LocalDB connection used `Encrypt=True;TrustServerCertificate=False`, but DMV evidence showed `ENCRYPTED=FALSE` on LocalDB shared-memory transport (NTLM). This verifies persisted SQL behavior on an isolated instance, not a trusted TCP/TLS certificate handshake.
- The photo picker and authenticated upload/display code are present. Live browser file selection was declined by the browser approval gate, so no physical photo was uploaded during the UI run. Synthetic evidence metadata associations passed the persisted service test; physical upload/display remains unverified. No offline photo queue is claimed.
- Verification: API and test Release builds succeeded; backend regression, 22 in-memory activity checks, OTP security checks, 108-route inventory check, 20 frontend suites/75 tests and web production build passed. Webpack emitted three existing configuration/asset-size warnings. `npx tsc --noEmit` still reports 87 existing errors, none in `InspectionActivityScreen.tsx`; Android was unavailable (`adb` not installed). No production target was used.

FR-01 and NFR-10 remain Missing. Activity delivery is usable in the explicitly selected local development profile, but photo browser upload, native-device behavior, trusted SQL TLS, formal business checklist acceptance, and full defect closure remain incomplete.

## Feature gap closure update (2026-10-31)

`FR-34` remains Partial Implementation: manager/Admin campaign planning now includes an inspector workload summary and dated task agenda based on current work-package data. It reports task states only and makes no claim of QC acceptance or compliance. Synthetic projection/UI tests pass; full frontend Jest passed (27 suites/102 tests), `npx tsc --noEmit` passed, and the web production build passed. No SQL access, migration or live workflow acceptance was performed for this update.

## Offline reference documents sprint update (2026-11-02)

`FR-45` remains Partial Implementation. The assigned offline package endpoint now includes the existing project hazardous-area plan (required when present) and area reference images (optional), with file size, checksum, revision metadata and per-task authenticated URLs. The URL requires an active approved Surveyor and a task assigned to that user; resource IDs are checked against the task project/area. Android native storage writes private app files encrypted using AES-GCM with an Android Keystore key, verifies bytes/checksum/signature before atomic replacement, and exports only an app-cache viewing copy that is removed on close/startup. PDF/JPEG/PNG/WebP viewing, current-versus-superseded revision visibility and local activity document-use records were added. Server-side usage audit is opt-in (`InspectionActivities:DocumentUsageEnabled`, default false) until additive migration `20261102044951_OfflineInspectionDocumentUsage` is applied and validated; the migration is generated only and was not applied.

Evidence: backend Release build passed; synthetic EF InMemory API checks passed for manifest generation, assigned/unassigned download authorization, opt-in document-use persistence in the synthetic context, scope mismatch denial and idempotent sync; frontend Jest passed (27 suites/107 tests), TypeScript passed and web production build passed. Android code/device build, encrypted file persistence, restart recovery, storage exhaustion on device, PDF/image rendering on target hardware and SQL persistence remain unverified. This does not establish operational or KAS3 acceptance.

Current gap classification from source review:

| Classification | Requirements/capabilities | Evidence and limit |
|---|---|---|
| Not implemented | Offline project/reference document package and viewer; recurrence occurrence materialization; permit/isolation clearance workflow; formal report/handover approval; tag stock inventory and tag coverage; HAER true OOXML `.xlsx`; approved IEC question library; tenant enforcement | Existing interfaces/data affordances do not satisfy the full requirements. No synthetic test is treated as persisted or operational acceptance. |
| Partially implemented | Inspector workload/calendar visibility; offline inspection package and sync; RFID/NFC/device identification; recurring schedule definitions; permits/isolation prerequisites; report archive and handover exports; defect/corrective-action workflow; inspection/checklist administration | Code and selected synthetic tests exist. Key gaps include no required document bundle, no task materializer, no permit authority transitions, no final approval, unverified device/hardware/SQL execution, and business-controlled content/roles. |
| Implemented in code; live integration testing pending | Campaign/work package APIs and screens; inspection draft/submit/QC return/approval; corrective action, Aries verification and client closure; HAER/inspection/defect reports and handover ZIP | Existing local/test evidence is documented in prior sprint entries. The present gap-closure task did not connect to SQL Server or run a live browser scenario. |
| Requires business/client approval | IEC applicability/content and numeric criteria; defect severity and escalation rules; permit and isolation authority/readiness; recurrence policy and duplicate handling; detailed sample/risk rules; report final approval and client acceptance roles; KAS3 mapping and source reconciliation | Do not invent standards, closure rules, client allocations or compliance decisions. |
| Requires external infrastructure/hardware | Trusted isolated SQL environment for persisted validation; Android SDK/device and secure-key verification; target HF RFID reader/NFC compatibility; SAP/ERP endpoint and approved mapping; approved source/reference documents | Keep these as separate acceptance gates; no production connection/deployment is authorized. |

## Non-functional requirements

| ID | Requirement (short form) | Status | Evidence / gap |
|---|---|---|---|
| NFR-01 | 28-day offline operation | M | No offline persistence/sync. |
| NFR-02 | Scan <1s; save <200ms | U | No measured latency/benchmarks. |
| NFR-03 | 50k/site, 500k/tenant, 2m photos | U | No load/storage benchmark or paging evidence for all paths. |
| NFR-04 | Sync prioritization/time/compression | M | No sync engine. |
| NFR-05 | Zero data loss | U | No validated durability/recovery evidence; photos local. |
| NFR-06 | 99.5% central availability | U | No SLO/monitoring evidence. |
| NFR-07 | RPO/RTO | U | No recovery plan evidence. |
| NFR-08 | OWASP ASVS L2 + pen test | PI | OTP attempts/IP throttle and secret-reference changes have regression checks; formal ASVS mapping, secret rotation, history remediation and penetration test remain outstanding. |
| NFR-09 | TLS, AES/SQLCipher, full-disk protection | PI | Tracked server secrets removed and environment/User Secrets paths documented; TLS configuration exists. Mobile/local encrypted storage and full-disk protection remain unverified. |
| NFR-10 | Tenant isolation | M | Synthetic resolver/policy/query-scope tests pass, but tenant feature flag is disabled, existing routes are not tenant-enforced, and persisted SQL isolation is unverified. |
| NFR-11 | Audit retention | PI | AuditLog exists; retention/immutability policy unknown. |
| NFR-12 | Data residency | U | Hosting region not specified/evidenced. |
| NFR-13 | Glove-friendly UI | U | No usability/device acceptance evidence. |
| NFR-14 | Device/permit constraints | M | No device approval/PTW enforcement. |
| NFR-15 | Observability | U | No deployment/central monitoring evidence. |
| NFR-16 | 70% coverage + CI/IaC | PI | Frontend/API build-test/security workflows and Dependabot are configured; no GitHub run evidence, coverage threshold, IaC or measured 70% coverage. |
| NFR-17 | Source/IP ownership | U | Contract/legal terms are outside source evidence. |

## Evidence pointers

Frontend evidence: `ex-survey/src/screens/` and platform navigation files. Backend evidence: `Ex-SurveyApp_api/Controllers/`, `Models/`, `Data/SurveyDbContext.cs`, `Data/Migrations/`, `Program.cs`, and `tests/QcWorkflowChecks/`. Requirement text is in the Aries architecture PDF; Bumi proposal §4 p.13 makes edge/RFID optional, while Aries p.6 proposes it as Phase 1. The absent BRD and KAS3 source export limit traceability. Full implementation evidence and caveats are in `CURRENT_STATE_AUDIT.md`.

## Inspection planning sprint evidence (2026-10-31)

Added `InspectionPlanningController`, planning entity/configuration classes, `InspectionPlanningCalculator`, additive migrations `20261031043123_InspectionPlanningScheduling`, `20261031044320_InspectionPlanningLeadAssignment` and `20261031044847_InspectionSamplingCampaignRevision`, and review-only SQL. Synthetic API checks pass 25 assertions for project-scoped campaign creation, lead selection/discovery and approval denial for non-lead QC, annual allocation, recurrence setup, revision-bound sample freeze/reapproval, task generation, status guards, reassignment/audit, carry-forward, assigned-surveyor access, progress and duplicate rejection. Calculation tests pass 8 assertions. Frontend Jest: 24 suites/85 tests; planning screen tests: 2; web production build passes. Full backend regression runner passes. EF snapshot has no pending model changes. No SQL migration was applied, no live KAS3 data was used, and tenant authorization remains disabled. `tsc --noEmit` still fails on pre-existing errors elsewhere; no diagnostics name the new planning screen.

## Integration acceptance attempt (2026-10-31)

The requested campaign-to-client-closure browser workflow was **not demonstrated**. `ExSurveySprint2_Test` files exist and its LocalDB error log records a restore, but LocalDB registry access fails for `sqllocaldb info/start`; strict-encryption `sqlcmd` using installed ODBC Driver 17 fails before login with client encryption/SSPI errors. No database was queried, changed, reset, or migrated. `QcReviewWorkflow` maps `Approved` into `Status` and drops the old column in source, but its target data/history could not be compared, so it was not run. API startup checklist seeding is also mutating, so startup against the existing test database was avoided.

Current local validation: backend Release regression passed; operational reporting checks passed 16 synthetic assertions; endpoint-scope inventory passed for 161 actions across 17 controllers; migration-script generation check passed without connecting; frontend Jest passed 26 suites/99 tests; TypeScript check passed; web production build passed with Babel and bundle-size warnings. None is live SQL/browser evidence. The test DB migration history, persisted record IDs, image upload/retrieval, refresh/new-session behavior and archived artifact retrieval over HTTP remain unverified. Android is blocked by missing JDK/`JAVA_HOME`, Android SDK and emulator. Tenant isolation and NFR-10 remain Missing; approved IEC content, KAS3 migration and production readiness remain unverified/not approved.

## Development testing restoration attempt (2026-10-31)

Machine inventory found only the already-installed default SQL Server Developer Edition service, LocalDB, and ODBC Driver 17; no Docker/Podman runtime or separate server endpoint/test identity is available. The previous LocalDB instance is inaccessible through its registry configuration and the known strict encryption/SSPI path is unresolved. One bounded, read-only LocalDB connection attempt returned the already documented ODBC 17 encryption/SSPI failure; it was not retried. Existing October 30 test database files and restore evidence remain untouched. No migration was applied.

The `ExSurveySprint2LocalDB` launch profile is now configured to skip its automatic default-checklist seed; the guard applies only to that Development profile and does not change Production startup behavior. The explicit UI seed command refuses any catalog other than `ExSurveyIntegration_Test`, requires integrated auth and strict TLS validation, checks for pending migrations and unrelated data, and labels generated checklist criteria synthetic/not IEC-approved. API/check-runner Release build passed with zero warnings/errors and the backend synthetic regression runner passed. EF snapshot check reports no pending model changes; a full migration script was generated offline. `QcReviewWorkflow`'s total mapping for the repository-defined non-nullable bit/date schema is confirmed from source, but target history/data validation remains blocked. Its rollback is lossy for non-approved statuses; before applying, check comment lengths against 4,000 characters and duplicates in the new unique review index. The deterministic area equipment sequence backfill and removal of the unused activity revision shadow column also require target data preflight. The checklist snapshot unique-index replacement needs review for legacy null-protection rows. No end-to-end, persisted-data acceptance step is complete.
