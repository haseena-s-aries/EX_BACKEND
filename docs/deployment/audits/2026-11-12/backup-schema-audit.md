# ExSurveyApp backup schema audit

Compared the supplied `C:/Users/AL414/Desktop/ExSurveyApp.bak` with the current EF Core runtime model and migration list on 12 November 2026.

- Backup database: **ExSurveyApp**; backup completed **11 November 2026, 15:09:48** (source SQL Server time).
- Restored separately on local `(localdb)\MSSQLLocalDB` as **ExSurveyBackupAudit** with separate workspace data/log files. No overwrite, no migrations applied, and no live database connection.
- Existing local application database and source backup were not modified. Audit database retained for inspection.

| Measure | Backup | Current backend |
|---|---:|---:|
| Application tables (excludes EF history) | 63 | 69 |
| Application columns (excludes EF history) | 690 | 753 |
| Recorded / available migrations | 4 | 16 |

**Missing: 6 complete tables (46 columns), plus 17 columns on existing tables. Three existing columns require nullable definitions.**

## Missing tables and their expected columns

### dbo.EquipmentChecklistCatalogs

| Column | SQL type | Nullable |
|---|---|---|
| Id | int | No |
| Content | nvarchar(max) | No |
| Version | int | No |

### dbo.ProjectClientAssignments

| Column | SQL type | Nullable |
|---|---|---|
| ProjectId | uniqueidentifier | No |
| ClientUserId | uniqueidentifier | No |
| AssignedAt | datetimeoffset | No |
| IsActive | bit | No |
| UpdatedAt | datetimeoffset | No |

### dbo.ProjectDelayEmails

| Column | SQL type | Nullable |
|---|---|---|
| Id | uniqueidentifier | No |
| AcceptedAt | datetimeoffset | Yes |
| AttemptedAt | datetimeoffset | Yes |
| RecipientId | uniqueidentifier | No |
| ScheduleEntryId | uniqueidentifier | No |
| Status | nvarchar(24) | No |

### dbo.ProjectReportWorkflows

| Column | SQL type | Nullable |
|---|---|---|
| ProjectId | uniqueidentifier | No |
| HoldReason | nvarchar(2000) | Yes |
| Revision | int | No |
| Status | nvarchar(20) | No |
| SubmittedArtifactId | uniqueidentifier | Yes |
| SubmittedAt | datetimeoffset | Yes |
| SubmittedByUserId | uniqueidentifier | Yes |
| UpdatedAt | datetimeoffset | No |
| UpdatedByUserId | uniqueidentifier | No |
| Version | bigint | No |

### dbo.ProjectScheduleEntries

| Column | SQL type | Nullable |
|---|---|---|
| Id | uniqueidentifier | No |
| ActorId | uniqueidentifier | Yes |
| DelayDays | int | No |
| DelayReason | nvarchar(2000) | Yes |
| EventKey | nvarchar(100) | No |
| ExpectedWorkEndDate | date | Yes |
| Kind | nvarchar(24) | No |
| OccurredAt | datetimeoffset | No |
| PreviousTargetDate | date | Yes |
| ProjectId | uniqueidentifier | No |
| TargetDate | date | Yes |
| WorkStartDate | date | Yes |

### dbo.ReportWorkflowEvents

| Column | SQL type | Nullable |
|---|---|---|
| Id | uniqueidentifier | No |
| ActorUserId | uniqueidentifier | No |
| ArtifactId | uniqueidentifier | Yes |
| FromStatus | nvarchar(20) | No |
| OccurredAt | datetimeoffset | No |
| ProjectId | uniqueidentifier | No |
| Reason | nvarchar(2000) | Yes |
| Revision | int | No |
| ToStatus | nvarchar(20) | No |
| Version | bigint | No |

## Missing columns on existing tables

| Table | Column | SQL type | Nullable |
|---|---|---|---|
| ActivityFindings | AosdId | nvarchar(max) | Yes |
| ActivityFindings | FailedGrades | nvarchar(max) | Yes |
| ActivityFindings | FaultCategory | nvarchar(max) | Yes |
| ActivityFindings | FollowUpRemarks | nvarchar(max) | Yes |
| ActivityFindings | RegisterRemarks | nvarchar(max) | Yes |
| CheckpointResponseRevisions | GradeResultsJson | nvarchar(max) | Yes |
| CheckpointResponseRevisions | InspectionComment | nvarchar(max) | Yes |
| GeneratedReportArtifacts | DocumentNumber | nvarchar(200) | Yes |
| GeneratedReportArtifacts | DocumentRevision | nvarchar(100) | Yes |
| GeneratedReportArtifacts | UidNumber | nvarchar(100) | Yes |
| InspectionActivities | EquipmentComments | nvarchar(max) | Yes |
| InspectionActivities | IsOnHold | bit | No |
| InspectionActivities | LastModifiedByUserId | uniqueidentifier | Yes |
| Projects | AdditionalQcId | uniqueidentifier | Yes |
| Projects | AdditionalSurveyorId | uniqueidentifier | Yes |
| Projects | TargetDate | date | Yes |
| SurveySessions | LastModifiedByUserId | uniqueidentifier | Yes |

## Existing columns requiring adjustment

| Table | Column | Backup | Expected |
|---|---|---|---|
| ChecklistVersionSnapshots | InspectionType | int NOT NULL | int NULL |
| ChecklistVersionSnapshots | SourceChecklistTemplateId | uniqueidentifier NOT NULL | uniqueidentifier NULL |
| InspectionActivities | Grade | int NOT NULL | int NULL |

`InspectionActivities.RowVersion`: SQL Server reports `timestamp`; EF expects `rowversion`. These are synonyms and require no type change. No other SQL type differences were found for matching columns.

## Migration history

The backup contains these four recorded migrations:

- `20261103074858_InitialCreate`
- `20261103102529_ChecklistSourceMetadata`
- `20261104110245_EquipmentIdentificationName`
- `20261104115125_EquipmentDefectRegister`

These 12 migrations are not recorded:

- `20261105111559_CategoryInspectionChecklist`
- `20261105133840_EquipmentChecklistCatalog`
- `20261106060624_InspectionGradeResults`
- `20261106061852_EquipmentInspectionComments`
- `20261106080939_InspectionDefectRegister`
- `20261106131959_ReportDocumentDetails`
- `20261109043002_ProjectSurveyorHandover`
- `20261109060233_ProjectWorkSchedule`
- `20261110105419_ProjectAdditionalQc`
- `20261110113353_InspectionActivityHold`
- `20261111054419_ProjectReportStatusWorkflow`
- `20261112045739_ProjectMultipleClients`

## Deployment implication

The full `docs/deployment/ex-survey-migrations-idempotent.sql` covers all 16 migrations and is the relevant existing script for this four-migration baseline. Its EF history checks should skip the first four and execute the remaining twelve. The narrower `incremental-after-20261106080939-idempotent.sql` starts too late for this backup and would omit earlier missing checklist/inspection changes. `project-multiple-clients-idempotent.sql` alone adds only the multiple-client relationship.

The observed missing columns, tables, and nullability changes correspond to the pending migrations. This audit does not certify deployment success: migration SQL was not executed against the audit database or live server. Foreign keys, indexes, data backfills, and all prerequisites must also succeed; do not fix this by adding only the listed columns or by inserting migration-history rows manually.

These results describe the supplied backup, not any changes made on the live server after its backup time. No production update was performed.

## Evidence files

Machine-readable inventories and comparison: `artifacts/backup-schema-audit/expected-schema.json`, `actual-columns.json`, `actual-migrations.json`, `actual-counts.json`, and `comparison.json`. Row counts were collected as metadata only; no application records or credentials are included in this report.
