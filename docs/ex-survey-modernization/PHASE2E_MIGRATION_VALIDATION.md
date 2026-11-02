# Phase 2E — EF Migration Validation

**Run date:** 30 October 2026. **Status:** EF metadata validation passed. SQL application/rehearsal remains blocked. No production or other database was modified.

## Toolchain

- .NET SDK: 10.0.401.
- EF Core runtime/design/SQL Server packages: 10.0.13.
- `dotnet-ef`: 10.0.13 restored from the existing NuGet cache and pinned in `.config/dotnet-tools.json`.
- `SurveyDbContextDesignFactory` supplies a localdb design-only provider configuration. Migration scaffolding does not load application settings, start the app's seeding path, or open a SQL connection.

## Migration inventory and order

`dotnet ef migrations list --no-connect` reports the existing migration chain followed by:

1. `20261029123545_Phase2aTenantFoundation`
2. `20261030104113_InspectionActivityFoundation`

The prior hand-authored Phase 2D draft was removed after comparison so the same 12 tables are not created twice. The Phase 2D migration was scaffolded from the model after Phase 2A and includes the generated `.Designer.cs` and updated `SurveyDbContextModelSnapshot.cs`.

## Scaffold comparison result

The generated `Up` contains:

- 12 `CreateTable` operations (the expected Phase 2D table inventory).
- 35 indexes, 34 foreign keys, the one-equipment check constraint, and one SQL Server `rowversion` concurrency column.
- 0 `AddColumn` operations on existing tables.
- 0 drops, renames, alters or raw SQL operations in `Up`.

The former hand-authored draft had the same 12-table inventory. The generated model adds the intended rowversion token for concurrent activity updates and uses actor naming consistently for activity-review events. `Down` removes only the 12 Phase 2D tables in dependency order. The existing `SurveySession.AreaEquipmentId` unique filtered index remains as-is; no operation changes it. No legacy entity table or existing key is modified.

`dotnet ef migrations has-pending-model-changes` reports **no changes** after scaffolding. The SQL review artifact was generated from Phase 2A to Phase 2D using EF tooling. It contains 12 table creates and no alterations/drops of existing business tables. Migration history ordering is validated from the assembly without checking which migrations are present in a database.

## Important verification limit

`--no-connect` migration listing and model-diff checks prove assembly ordering and snapshot consistency only. They do not prove that Phase 2A has been applied anywhere, that SQL Server accepts the DDL, or that an existing database has no conflicting objects. No database migration was applied. SQL constraint validation, migration-history rows, FK/index inspection, transaction behavior, SQL rowversion races, rollback and backup/restore require the isolated SQL rehearsal in `PHASE2E_SQL_REHEARSAL.md`.
