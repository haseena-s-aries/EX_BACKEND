# Phase 2E — SQL Server Rehearsal

**Status:** Blocked before authentication/query execution. This report distinguishes local diagnostic observations from migration/SQL facts that remain unverified. No production database was accessed, and no SQL setting/certificate/registry value was changed.

## Current diagnostics

The non-destructive local harness targets only `localhost/master` with `Encrypt=True` and `TrustServerCertificate=False`. It fails before query/login:

```text
SqlException Number=20; State=0; Message=The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.
```

No query was executed and no database was modified. This is consistent with the earlier read-only Phase 2C findings in `SQL_CLIENT_TLS_DIAGNOSTICS.md`:

- `MSSQLSERVER` is running; `SQLBrowser` is disabled.
- The inspected SQL instance has TCP/IP and Named Pipes disabled; Shared Memory is enabled, and no TCP listener was observed.
- ODBC 17 `sqlcmd` previously reported that encryption is unsupported by that provider path and that no SSPI credentials were available.
- .NET SqlClient 6.1.7 failed before authentication. No matching Schannel System event was found in the queried 14-day window. SQL Server ERRORLOG could not be read.
- No separate isolated SQL Server instance with a trusted certificate was identified.

The observed .NET exception points to the client encryption/Windows security-provider path; SQL authentication and database authorization have not been reached. The error does not establish that a certificate is invalid. Because TCP is disabled on the inspected instance, the diagnostics do not prove a TCP/TLS handshake with SQL Server. Certificate presentation, chain/name matching, negotiated protocol/cipher and server-side TLS logs remain unknown.

Read-only inspection also found environment-specific settings with certificate trust bypass enabled. They were not changed or used by the strict diagnostic. They are not acceptable as the basis for a trusted TLS rehearsal; use a dedicated test configuration with `Encrypt=True` and `TrustServerCertificate=False`.

## Migration and data rehearsal results

No SQL-backed rehearsal step ran. Therefore these are **not tested with persisted SQL data**:

- Existing migration application/history, including Phase 2A and Phase 2D ordering.
- The 12 tables, 35 indexes, 34 foreign keys and task equipment check constraint on SQL Server.
- Synthetic plan/activity/checkpoint/finding/action inserts through the SQL schema.
- Project/equipment crosswalk conflicts and FK restrictions.
- Concurrent rowversion updates and unique checkpoint revision races.
- Existing inspection/QC/report queries against the migrated database.
- Backup, restore, rollback and recovery point validation.

EF scaffolding, SQL script generation and `has-pending-model-changes` are design-time checks and do not count as SQL rehearsal.

## Safe setup needed to unblock

Repository owner/DBA should provide a separate test instance and test database, a server certificate matching the test DNS name and chaining to a trusted issuer on the client, TCP connectivity through approved change control, and a test-only login restricted to that test database. Retest with encryption/certificate validation strict; collect provider, protocol/cipher, client Schannel/SSPI and server ERRORLOG evidence. Do not disable encryption, set `TrustServerCertificate=True` for the rehearsal, change machine security settings, or point the test at production.

After connectivity is verified, rehearse in a disposable database: apply the complete existing migration chain; verify Phase 2A; apply Phase 2D; inspect table/FK/index/check-constraint metadata and migration history; insert synthetic representative modern and legacy-linked records; test repeated activities, conflicting ownership, FK restrictions and concurrent rowversion updates; run existing surveyor/QC/report tests; take a backup; restore it to a separate test database; and document the rollback procedure. Keep all results explicitly identified as isolated test data.
