# Integration Acceptance and Live Demonstration

**Run date:** 31 Oct 2026  
**Environment:** Windows development workstation; no production systems accessed  
**Result:** Blocked before database connection; no live integrated workflow was demonstrated.

## Summary

The frontend and backend code has synthetic regression coverage, but the acceptance path requires a persistent isolated database and authenticated browser sessions. The existing `ExSurveySprint2_Test` LocalDB files are present and its LocalDB log records a restore on 30 Oct 2026. The `ExSurveySprint2` LocalDB instance cannot currently be queried or started from this Windows execution context. No migrations or seed operations were run.

The integrated scenario therefore has **no completed live stages**. In particular, no persisted record IDs, session IDs, generated archive IDs, live upload results, or live user-session evidence can be reported. Synthetic tests are not represented as persisted-data or browser evidence.

## Development profile review

- `ExSurveySprint2LocalDB` targets `(localdb)\ExSurveySprint2` / `ExSurveySprint2_Test`, sets `Encrypt=True` and `TrustServerCertificate=False`, enables `InspectionActivities`, and keeps `TenantFoundation` disabled.
- The generic Development profile in `appsettings.Development.json` points to `localhost` / `ExSurveyApp` and sets `TrustServerCertificate=True`. It was not used because its database identity and certificate trust do not satisfy the isolated, strict-TLS acceptance boundary.
- SQL Server LocalDB reports version 17.0.4025.3. `sqllocaldb info` lists the instance name, but `sqllocaldb info ExSurveySprint2` and `sqllocaldb start ExSurveySprint2` both fail while accessing LocalDB instance registry configuration.
- A read-only `sqlcmd` connection using the installed Microsoft ODBC Driver 17 with encryption required fails with “Encryption not supported on the client” and “No credentials are available in the security package.” The query never reached SQL authentication or database inspection.
- The LocalDB `error.log` records `ExSurveySprint2_Test` being restored and made available on 30 Oct 2026. Its contents, migration history and data rows could not be queried. The prior strict-TLS/SSPI failure against the running SQL Server service remains unresolved; it was not retried without new provider or certificate evidence.
- Docker is unavailable. No separate SQL Server container or trusted-certificate development instance could be started.

## Migration review and safety

The current reviewed migration sequence includes the historical `QcReviewWorkflow` transformation and later inspection, checklist, planning, offline-sync, tag and generated-report changes. The Phase 2E/Phase 2D migration scripts and current model snapshot have previously been generated and inspected without connecting to a database; the current EF tooling reports no pending model changes.

`QcReviewWorkflow.Up` sets `QcReviews.Status` to `3` when `Approved = 1` and `2` otherwise, copies `ReviewedAt` to `StartedAt`, then drops `QcReviews.Approved`. This is a deliberate schema transition in source, but the actual target schema, historical values, nullability, row counts and migration history are unavailable. The requested comparison against representative persisted rows could not be performed, so this migration was **not run**.

The latest tag/archive migration is additive: it creates `EquipmentTagAssignments`, `EquipmentTagHistory` and `GeneratedReportArtifacts`. The generated review script has no `DROP TABLE`, `DROP COLUMN`, `DROP INDEX`, or `ALTER COLUMN` operations. It was not applied.

Application startup currently seeds or updates three `Default {grade} Inspection` templates and their question rows. It can also provision Admin/Manager bootstrap users when credentials are supplied through configuration. Consequently, pointing the API at an existing database is not a read-only operation. Startup was not attempted against `ExSurveySprint2_Test`.

## Live scenario acceptance matrix

| Workflow stage | Code / synthetic evidence | Live UI | Persisted isolated DB | Result |
|---|---|---|---|---|
| Admin creates campaign and assigns work | Planning APIs/screens and synthetic planning checks exist | Not run | Not run | Not demonstrated |
| Inspector identifies assigned equipment | Manual/scan lookup paths and simulated scanner tests exist | Not run | Not run | Not demonstrated |
| Start activity with applicable published checklist | Activity/checklist implementation and synthetic tests exist | Not run | Not run | Not demonstrated |
| Save responses/readings, photograph and finding | API/UI code and isolated tests exist | Not run | Not run | Not demonstrated |
| Submit, QC return, correct, resubmit and approve | Activity/QC paths and regression checks exist | Not run | Not run | Not demonstrated |
| Corrective action, Aries verification, client closure | Defect workflow code/tests exist; closure remains separate from inspection approval | Not run | Not run | Not demonstrated |
| Campaign progress reflects state | Progress reporting synthetic checks distinguish planned/completed/QC/compliance | Not run | Not run | Not demonstrated |
| Generate, archive and retrieve inspection PDF / handover ZIP | Archive logic, exact-byte/checksum and role-denial checks pass with synthetic metadata/temp filesystem | Not run | Not run | Not demonstrated against SQL-backed metadata or live authorization session |
| Refresh and new-login recovery | No full authenticated browser scenario executed | Not run | Not run | Not demonstrated |

## Verification performed

- Backend Release regression runner passed. It exercises existing QC, project/file, defects, HAER/PDF/Excel and related checks with its configured test fixtures; these are not persisted `ExSurveySprint2_Test` evidence.
- Operational report checks passed: 16 synthetic checks, including PDF parsing, handover manifest/checksum, archive revision metadata, exact bytes returned from temporary filesystem storage, path traversal rejection and denial to an unassigned Manager.
- Endpoint-scope inventory passed for 161 route actions across 17 controllers. This is passive classification only and does not enforce tenant isolation.
- EF migration-script check passed without opening a database connection.
- Frontend Jest: 26 suites / 99 tests passed. TypeScript check passed. Web production build passed with Babel loose-mode and large-bundle warnings.
- Web UI unit tests are not live browser acceptance. No login, page refresh, role switch, real upload, or API-backed download was performed.
- Android build and device tests were unavailable: `JAVA_HOME` is unset, no `java` command/JDK was found, and the Android SDK/emulator are absent. No Android configuration or production infrastructure was changed.

## Report and evidence verification

Generated inspection report artifacts have immutable IDs, revision metadata, generation identity/time, Draft approval state and SHA-256 metadata. The underlying binary is stored outside the web root and retrieval verifies the checksum before returning it. Synthetic checks confirm exact bytes and project authorization behavior.

No database-backed artifact metadata was written, and no live archived download was made. No client approval was recorded; no generated artifact is marked final.

## Remaining blockers and next acceptance run

1. Restore the `ExSurveySprint2` LocalDB instance registry/configuration access for the intended Windows account, or provide a separate isolated SQL Server development instance with an encryption-capable client and a certificate trusted for its server name. Keep certificate validation enabled.
2. Confirm the isolated database identity and a recoverable synthetic-data backup. Do not reset or recreate `ExSurveySprint2_Test` without approval.
3. Compare the database's `__EFMigrationsHistory` and representative `QcReviews.Approved` records with the historical `QcReviewWorkflow` transformation before migration execution.
4. Review/gate startup checklist seeding so the demonstration uses explicitly synthetic, non-IEC-approved content and cannot rewrite an existing database's templates.
5. Apply reviewed migrations only to the isolated database, then run the browser scenario with synthetic role accounts and record generated entity IDs, API operations, photo retrieval, report checksum and refreshed/new-session evidence.
6. Install a supported JDK and Android SDK/emulator if Android restart/offline evidence is required.

No production database, production deployment, tenant enforcement or production setting was touched. This report does not establish tenant isolation, approved IEC criteria, KAS3 data reconciliation, physical-reader compatibility or production readiness.

## Development-testing restore attempt (31 Oct 2026)

The machine inventory found a running existing `MSSQLSERVER` service whose installed edition is Developer Edition (SQL Server 17.0.1000.7), plus LocalDB and ODBC Driver 17. No Docker or Podman executable/runtime is available. The existing default SQL Server installation was not changed or reused because the prior strict-TLS client failure remains unresolved; a separate Developer/Express installation would require an elevated installer and likely package/network access. No IT-hosted isolated server or test identity was supplied. The old `ExSurveySprint2_Test` MDF/LDF files and October 30 restore log were left untouched.

The current migration source confirms the original `QcReviews.Approved` and `ReviewedAt` columns were non-nullable. For that schema, the forward conversion is total: `Approved=true` maps to status value 3, `false` to 2, and `StartedAt` receives `ReviewedAt`. The migration drops `Approved`; its rollback maps only status 3 back to true and all other statuses to false, so it cannot restore distinctions among non-approved statuses. No real target schema or representative rows were inspected. Before applying to any non-empty database, verify the target migration head and that every `QcReviews` row matches the expected source schema and the count/values survive the conversion.

Other forward-script preflights: `QcReviewWorkflow` narrows `QcReviews.Comments` from `nvarchar(max)` to 4,000 characters (check maximum existing length), and creates a unique index on `QcReviews.SurveySessionId` (check for duplicates). It makes several legacy parent/date fields nullable. `AreaEquipmentSequenceNo` backfills sequence values deterministically by `(AreaId, CreatedAt, Id)`, then applies a not-null unique `(AreaId, SequenceNo)` index. The unused `InspectionActivityRevisions.InspectionActivityId1` shadow column is dropped in its cleanup migration; verify it carries no required data if the target is already at the preceding migration. The forward script also replaces the old unfiltered checklist snapshot uniqueness index with a filtered index on non-null protection type; null-protection rows no longer receive the old uniqueness guarantee. These items need source-data checks before applying the chain to a non-empty target.

The isolated `ExSurveySprint2LocalDB` launch profile now disables only its automatic startup checklist seed. Production startup behavior was not changed. The explicit UI seed command now refuses any database except `ExSurveyIntegration_Test`, requires integrated authentication with `Encrypt=True` and `TrustServerCertificate=False`, verifies there are no pending EF migrations, and refuses to run if it finds records outside its synthetic fixture. It seeds clearly labeled synthetic users, checklist content, equipment, campaign and work package. Its no-connection guard was exercised and refused to run without the explicit isolated connection. It does not reset the database or run migrations.

Release build of the API/check runner succeeds with zero warnings/errors; the backend synthetic regression runner passed after these changes. EF reports no pending model changes. The repository currently has 31 timestamped migration classes; the complete 0-to-latest script is at `Ex-SurveyApp-API/tmp/ExSurveyIntegration_Test.review.sql` and was generated offline only. The new database still cannot be provisioned here, so none of the migrations, seed writes, application startup, or browser acceptance steps were run.

**Exact external prerequisite:** IT or a machine administrator must provide either (1) an isolated SQL Server Developer/Express instance with a dedicated `ExSurveyIntegration_Test` database, reachable using an encryption-capable SQL client and a server certificate trusted for that endpoint, plus a Windows test identity authorized only to that database; or (2) an approved elevated installation path and package/network access to install a separate instance. Docker is not installed. Do not enable `TrustServerCertificate`, use the generic `ExSurveyApp` development database, or point the seed command at `ExSurveySprint2_Test`.
