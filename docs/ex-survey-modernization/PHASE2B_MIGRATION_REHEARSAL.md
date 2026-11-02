# Phase 2B Migration Rehearsal

**Status: blocked before database session.** No SQL command changed data. No production endpoint or database was used.

## Local diagnosis

- Windows service `MSSQLSERVER` is `Running` (automatic start). This establishes that the local SQL Server service is present, not that an authenticated SQL session works.
- The .NET `Microsoft.Data.SqlClient` loopback probe targeted only `localhost/master`, with `Encrypt=True` and `TrustServerCertificate=False`. It failed before authentication/query execution with SQL error 20: `The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.`
- `sqlcmd` (Microsoft ODBC Driver 17) to `localhost` reported `Encryption not supported on the client` and `SSL Provider: No credentials are available in the security package`. The client then reported it could not establish the connection. This points to the local Windows client TLS/SSPI provider or its runtime credentials, not an observed SQL login rejection. Certificate trust has not been disproved because TLS negotiation could not complete.
- Access to LocalDB instance registry configuration is denied by the current environment (`sqllocaldb info MSSQLLocalDB`), so LocalDB is not a usable fallback here.
- The NuGet proxy also refused access during a first tool-project restore. The tool was subsequently built offline after removing external package dependencies.

Do not set `Encrypt=False` or globally enable `TrustServerCertificate` to bypass this issue. The isolated example configuration retains `Encrypt=True` and certificate validation. Before rehearsal, install/repair a supported SQL client TLS provider (or use a correctly configured SQL Server certificate trusted by this machine), confirm Windows integrated-auth credentials are available in the execution context, and verify loopback TCP/instance connectivity. Keep those changes local to the test host.

## Isolated rehearsal configuration

Use [appsettings.TenantRehearsal.example.json](appsettings.TenantRehearsal.example.json) as a starting point. Copy it to the ignored `Ex-SurveyApp_api/appsettings.TenantRehearsal.json` only on the local test host; keep the DB name `ExSurvey_TenantRehearsal`. It targets `localhost`, uses integrated authentication, requires encryption, validates the server certificate, and leaves tenant enforcement disabled. Never copy production connection strings into this file.

Before EF applies anything, verify the selected server is local and the database name is the exact rehearsal name. Restore a sanitized backup into this isolated database, record its backup checksum, and capture row counts/IDs for Projects, ProjectAreas, AreaEquipment, Jobs, Areas, Equipment, SurveySessions, Findings, QcReviews and AuditLogs. The application does not auto-run migrations at startup; use EF migration commands only after reviewing the target connection.

## Rehearsal sequence (not yet executed)

1. Restore a sanitized baseline backup into the isolated rehearsal DB; verify owner, recovery model, compatibility level and table counts.
2. Generate and inspect the migration script from the current last migration through `20261029123545_Phase2aTenantFoundation`.
3. Apply it only to the isolated rehearsal DB; confirm migration history and the eight new tables (`Tenants`, `TenantContracts`, `TenantProjectBindings`, `TenantSites`, `TenantMemberships`, `TenantRoleGrants`, `LegacyEntityMaps`, `TenantBackfillExceptions`).
4. Verify expected FKs, check constraints, unique indexes and the absence of any rows in new ownership tables. Verify old project IDs, areas, equipment IDs and counts are unchanged.
5. Run API project/surveyor/QC/client/report checks against the restored dataset and compare outputs/counts before and after the additive migration.
6. Take a post-migration backup, restore it as a second isolated database, and verify migration history, row counts and API reads again.
7. Capture duration, blocking/locking, errors and SQL Server version/compatibility level. Do not use the migration `Down`; it is intentionally forward-only. Recovery is restore-to-new-database and a separately approved cutback plan.

## Evidence

Completed here: local SQL service status check; strict-encryption .NET and ODBC diagnostics; SQL DDL generation and static safety check; API Release build; in-memory foundation/backfill tests; existing backend workflow regression runner.

Not verified: database connectivity/session, migration application, actual FK/index enforcement on SQL Server, lock behavior, project record compatibility against a restored DB, backup/restore, or workflow comparison against persisted records. These remain blockers to declaring the rehearsal complete.
