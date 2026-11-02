# Phase 2D Test Results

Run date: 30 October 2026. Scope was local development only. No production database was accessed.

## Passed

| Check | Result |
|---|---|
| API Release build (`dotnet build Ex-SurveyApp_api/Ex-SurveyApp_api.csproj --no-restore -c Release`) | Passed; 0 warnings, 0 errors. |
| Workflow test runner Release build | Passed; 0 warnings, 0 errors. |
| New inspection activity synthetic suite (`--inspection-activity-checks`) | Passed all 21 checks. |
| Existing default backend workflow runner | Passed QC workflow, query translation, file upload, project, report, client, defect, photo and existing role tests shown by runner. |
| Non-destructive SQL diagnostic (`--local-sql-diagnostic`) | Ran; failure is accurately reported below. No query/database modification. |
| Source/model compatibility checks in new suite | Confirmed unique SurveySession/AreaEquipment index is still present and new FK/index/check-constraint metadata exists. |

The 21 new synthetic checks cover Visual/Close repeated activities for one asset, unique stable IDs, append-only activity/response revisions, reviewer metadata, multiple checkpoint findings, corrective-action evidence events, acceptance not closing findings/actions, accepted activity edit guard, client-only closure, request-grade validation, stale concurrency token rejection, absence of client tenant fields, legacy unique index retention and modern parent-derived tenant resolution.

## Blocked

- SQL Server handshake reports SqlClient error 20: “The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.” No database name/schema query was reached, and no SQL was executed. TLS settings were not weakened.
- Consequently, no existing or Phase 2A migrations were applied; the new Phase 2D migration was not applied; no eight-table Phase 2A validation, backup, restore, or SQL Server foreign-key/locking test was performed.
- SQL Server application, foreign-key validation, SQL-backed concurrency, persisted ownership-conflict handling and backup/restore remain blocked by the encrypted connection error. EF model scaffolding is now verified separately with local EF tooling.
- The two WhatsApp ZIPs, `Sample checklist.pdf`, and current signed BRD are not present. A local WhatsApp-labeled image was not a workflow reference, so it was excluded from requirement evidence.

## Limits

EF InMemory verifies application/model logic and uses synthetic records only. It does not prove persisted SQL constraints, reliable append-only enforcement, transaction behavior, backup restore, tenant data isolation, or API authorization for routes that do not exist yet. No mobile/frontend flow or report output was added or tested for the new model.
