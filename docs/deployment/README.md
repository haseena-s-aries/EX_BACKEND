# SQL Server migration deployment

The full idempotent script covers all 20 EF Core migrations through `20261116124045_EquipmentLayoutImageAssociation`. Use it on an existing empty database or a database whose schema matches its EF migration history. It does not copy local records.

## Updates after the previously shared script

If the live database already has the original nine migrations through `20261106080939_InspectionDefectRegister`, use [incremental-after-20261106080939-idempotent.sql](incremental-after-20261106080939-idempotent.sql). It contains only the six subsequent migrations listed below. Already-recorded migrations are skipped using `__EFMigrationsHistory`.

Before using the incremental file, confirm the baseline exists:

```sql
SELECT SERVERPROPERTY('ServerName') AS ServerName, DB_NAME() AS DatabaseName;
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
```

If the baseline is absent, use the full script only after confirming that the existing schema and migration history match this project. Do not manually insert migration-history rows to bypass a mismatch. Follow the backup, staging, and stop-on-error instructions below. Neither file was executed against the live server when generated.

To regenerate only this increment without connecting to a database:

```powershell
dotnet tool run dotnet-ef migrations script 20261106080939_InspectionDefectRegister 20261111054419_ProjectReportStatusWorkflow --idempotent --no-build --project Ex-SurveyApp_api --startup-project Ex-SurveyApp_api --output docs/deployment/incremental-after-20261106080939-idempotent.sql
```

The incremental file adds report document metadata, additional Surveyor assignment and modifier tracking, project scheduling, additional QC assignment, inspection activity Hold status, and report workflow tables. It contains no account passwords or local data. The newer QC read-only endpoints and dashboard/landing-page UI changes need no further migrations.

From the backend repository, regenerate the script after adding migrations:

```powershell
dotnet tool run dotnet-ef migrations script 0 --idempotent --configuration Release --project Ex-SurveyApp_api --startup-project Ex-SurveyApp_api --output docs/deployment/ex-survey-migrations-idempotent.sql
```

1. In SSMS, connect to the intended SQL Server and select the target database. Run the preflight queries below and confirm the server and database names.
2. If the database has application tables, confirm its schema matches the recorded migrations. Stop if the history is missing or incomplete for the existing schema, or if the migration IDs are from a different chain. Reconcile the schema and history before running this script.
3. Take a database backup and validate the deployment against a staging copy. Run the entire SQL file in SSMS or sqlcmd with batch handling for `GO` and with execution configured to stop on errors. With sqlcmd, use `-b`. Deploy the updated API after the script succeeds.
4. Run the history query again. Confirm that the 20 migrations below are present. On staging, rerun the file and confirm that the schema and history are unchanged.

```sql
SELECT SERVERPROPERTY('ServerName') AS ServerName, DB_NAME() AS DatabaseName;

SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS TableName
FROM sys.tables
ORDER BY SchemaName, TableName;

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
    EXEC(N'SELECT MigrationId, ProductVersion FROM [dbo].[__EFMigrationsHistory] ORDER BY MigrationId;');
```

- 20261103074858_InitialCreate
- 20261103102529_ChecklistSourceMetadata
- 20261104110245_EquipmentIdentificationName
- 20261104115125_EquipmentDefectRegister
- 20261105111559_CategoryInspectionChecklist
- 20261105133840_EquipmentChecklistCatalog
- 20261106060624_InspectionGradeResults
- 20261106061852_EquipmentInspectionComments
- 20261106080939_InspectionDefectRegister
- 20261106131959_ReportDocumentDetails
- 20261109043002_ProjectSurveyorHandover
- 20261109060233_ProjectWorkSchedule
- 20261110105419_ProjectAdditionalQc
- 20261110113353_InspectionActivityHold
- 20261111054419_ProjectReportStatusWorkflow
- 20261112045739_ProjectMultipleClients
- 20261113044054_AccountProfileAndRecovery
- 20261113072459_UserRegistrationAndRoleRequests
- 20261116053821_BrowserFieldworkReceipts
- 20261116124045_EquipmentLayoutImageAssociation

## Equipment marking image associations (16 November 2026)

The latest migration adds a nullable `AreaEquipment.LayoutImageKey` so existing markers remain valid while newly placed markers are tied to their selected area photo or project layout. The updated full idempotent script includes this change. The local Development database applies EF migrations on startup; production deployments should apply the full script using the preflight and staging steps above before deploying the API.

## Create the first Admin on the live IIS server

After publishing and applying the schema, open PowerShell in the published API folder and run:

```powershell
dotnet .\Ex-SurveyApp_api.dll create-admin
```

Confirm the displayed database name, enter a new Admin email, and enter and confirm a password. The password is entered with echo disabled. The command creates an approved, active Admin and exits.

Use the same production database settings and SQL access as IIS. If the connection string is defined only in IIS web.config, a standalone PowerShell process will not inherit it. Supply the same ConnectionStrings__SurveyDatabase value privately to the command process, then clear that process variable after the command exits.

## Create a Surveyor without OTP

From the same published API folder, run `dotnet .\Ex-SurveyApp_api.dll create-surveyor`.
Confirm the database name and enter a new email and matching passwords. The command creates an approved, active Surveyor. Assign the Surveyor to a project from the Admin side.


## Equipment category checklist

Open Checklists as an Admin or assigned Surveyor. This screen uses the existing InspectionActivities feature. Set `InspectionActivities__Enabled=true` on the API host if it is not enabled. Import the sample checklist in Checklist Administration and have the source versions technically approved and published before beginning new category inspections. Existing grade-based inspections and templates retain their original grade.

Select a project and HAER equipment, then select PDF checkpoint rows and choose Save Selected Checkpoints. New Surveyor inspections use the equipment selection when one exists. Existing category inspections retain their original snapshot. Previously saved project-wide selections serve as a fallback until an equipment-specific selection is saved.

## Multiple Client assignments (12 November 2026)

Apply `project-multiple-clients-idempotent.sql` after `20261111054419_ProjectReportStatusWorkflow`, then deploy the updated API and frontend together. This migration adds the project/Client relationship table and copies existing single-client links. It retains `Projects.AssignedClientId`, client company names, and logos. Deactivated links and assignment audit entries preserve history. Company details are now required on create/edit; historical blank company values are not fabricated or overwritten.

The local `ExSurveyMigrationDesignOnly` database was migrated during implementation. No live database was updated. For another database, back it up and validate the script on staging using the same safeguards above. Existing migration-history rows must match the schema.

## Account profile and password recovery (13 November 2026)

For the live baseline confirmed through ProjectMultipleClients, use `account-profile-recovery-idempotent.sql` in SSMS SQLCMD Mode. It targets ExSurveyApp, adds four Users columns and AccountActionTokens, and skips an already-recorded migration. Deploy the API and frontend after applying it. The full script now includes all 17 migrations.

Local ExSurveyMigrationDesignOnly was updated during implementation. No live database was updated by the agent. Restart the local API to load the new endpoints. AccountSecurity:PublicAppUrl is https://139.5.189.155:339/ in production settings and http://localhost:3000/ in Development. IIS can override it using AccountSecurity__PublicAppUrl. Do not point it at the API port.

See ../account-profile-recovery.md for endpoints, verification results and SMTP/session details.


## Registration approval and role change requests (13 November 2026)

After AccountProfileAndRecovery, apply `user-registration-role-requests-idempotent.sql` in SQLCMD Mode, then deploy the API and frontend together. The script explicitly targets ExSurveyApp. It adds User registration/rejection metadata and two tables (RoleChangeRequests and UserAccessNotifications), with a filtered unique index preventing simultaneous pending requests. Existing pending registrations receive one durable alert per eligible approver. Historical registration dates stay null where unknown. The full script now includes 18 migrations.

The local ExSurveyMigrationDesignOnly database is the only database targeted during implementation. No live database was modified. See ../user-registration-role-requests.md for behavior and verification.
