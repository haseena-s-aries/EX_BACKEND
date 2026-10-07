# Manager QC Review

Source backups were made before edits and verified by file hashes:

`C:\Users\AL414\source\repos\backups\qc-workflow-20261006-144100`

Both application source trees are included. Dependencies, Git metadata, IDE caches,
build output and logs are excluded. This is a source backup, not a SQL database backup.

## Apply and run

1. Stop the API debugger in Visual Studio.
2. Apply `20261006104409_ManagerQcMonitoring` to the API's SQL database. In Visual
   Studio's **Package Manager Console**, select `Ex-SurveyApp_api` as the Default
   project and run `Update-Database`. Alternatively, run the supplied idempotent
   `manager-qc-migrations.sql` script in SQL Server Management Studio against the
   application's database. Use one method; already applied migrations are skipped.
3. Rebuild and start the API using its **https** launch profile, because the existing
   frontend uses `https://localhost:7288/api`. For PowerShell:

   ```powershell
   cd C:\Users\AL414\source\repos\Ex-SurveyApp-API
   dotnet run --project .\Ex-SurveyApp_api --launch-profile https
   ```

4. Start the frontend:

   ```powershell
   cd C:\Users\AL414\source\repos\ex-survey
   npm.cmd run web
   ```

5. Sign in as Admin. Open **QC Review → Reporting managers** and set each QC user's
   reporting Manager. Existing users start with no manager; no relationship is
   guessed from approval records. Create/Edit Project now shows
   `QC Name — Manager: Manager Name`. Project assignment continues to use `AssignedQcId`.
6. Sign in as Manager and open **QC Review** from the sidebar, mobile web tabs, or
   native dashboard. Drill into a Project, Area, Equipment, Inspection and QC History.

The migration has been generated and checked, but has not been applied to the live database.

## Access and counts

- Managers see projects they created and projects whose assigned QC reports to them.
  Admin sees all projects. Scope is enforced on every project, area, equipment and photo API.
- Surveyor, QC and Client cannot access the Manager APIs or see the monitoring menu.
- Reporting-manager changes require Admin. Managers cannot edit reporting assignments.
- Monitoring provides no equipment/checklist edit, QC decision or reopen endpoints.
  The current workflow has no controlled reopen action, so approved reviews stay locked.
- Workflow counts count submitted inspections, not all equipment. Unstarted equipment
  and drafts are visible in drill-down and excluded from awaiting-QC counts.
- Overdue means the inspection's existing due date has passed and QC is not approved.
  Project status and QC review status remain separate.

## History

The existing `QcReview` remains the current state. Existing `AuditLog` rows are extended
with QC status, comments, severity and review timestamps; workflow events are appended
in the same database save as the existing actions. Comments updates are also recorded.
The existing QC inspection reader and status aggregation are shared with monitoring.

The migration copies each available old review into an **Existing QC Snapshot**.
Previously overwritten comments or earlier review rounds cannot be recovered. After
this migration, new submissions, review starts, change requests, resubmissions,
comment updates and approvals form the complete timeline without replacing old events.

## Verification

Run the backend checks without writing to the active debugger's output directory:

```powershell
cd C:\Users\AL414\source\repos\Ex-SurveyApp-API
dotnet build .\tests\QcWorkflowChecks --no-restore -p:UseAppHost=false -p:OutputPath=bin/QcChecks/
dotnet .\tests\QcWorkflowChecks\bin\QcChecks\QcWorkflowChecks.dll
```

The checks exercise real controllers with EF InMemory, including multi-round QC
history, locked approvals, scoped counts, reporting-manager changes and protected
photos. SQL Server query translation is verified without connecting to a database.
They do not replace a live SQL migration or browser/native-device acceptance test.

Frontend verification:

```powershell
cd C:\Users\AL414\source\repos\ex-survey
npm.cmd run build
npm.cmd test -- --runInBand ManagerQcReviewScreen QcDashboardScreen SurveyorInspectionScreen SurveyorProjectDetailsScreen SurveyorDashboardScreen
```

The existing App test's React Navigation ESM parsing failure and existing TypeScript
errors in theme/OTP/web declarations predate these changes. The targeted suites pass.
