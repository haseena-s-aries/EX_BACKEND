# SQL Server migration deployment

The idempotent script covers the full EF Core migration chain through InspectionDefectRegister. Use it on an existing empty database or a database whose schema matches its EF migration history. It does not copy local records.

From the backend repository, regenerate the script after adding migrations:

```powershell
dotnet tool run dotnet-ef migrations script 0 --idempotent --configuration Release --project Ex-SurveyApp_api --startup-project Ex-SurveyApp_api --output docs/deployment/ex-survey-migrations-idempotent.sql
```

1. In SSMS, connect to the intended SQL Server and select the target database. Run the preflight queries below and confirm the server and database names.
2. If the database has application tables, confirm its schema matches the recorded migrations. Stop if the history is missing or incomplete for the existing schema, or if the migration IDs are from a different chain. Reconcile the schema and history before running this script.
3. Take a database backup and validate the deployment against a staging copy. Run the entire SQL file in SSMS or sqlcmd with batch handling for `GO` and with execution configured to stop on errors. With sqlcmd, use `-b`. Deploy the updated API after the script succeeds.
4. Run the history query again. Confirm that the nine migrations below are present. On staging, rerun the file and confirm that the schema and history are unchanged.

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
