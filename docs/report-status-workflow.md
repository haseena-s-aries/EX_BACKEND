# Report Status workflow

Report Center displays a project-based Report Status below project selection and above the report categories. Its values are **Ongoing**, **Hold** and **Submitted**. This field is independent of Project Status, Inspection Status, QC Status and individual defect statuses.

Existing projects without a workflow record read as Ongoing. The first archived project PDF persists an Ongoing workflow; generating or downloading a PDF never submits it. Reading a status does not create or modify records.

## Permissions and transitions

All requests reuse the existing project report scope, including additional Surveyor assignments.

| Role | Read | Ongoing / Hold / Submit | Reopen Submitted |
| --- | --- | --- | --- |
| Admin | Authorized projects | Yes | Yes |
| Manager | Existing managed project scope | Yes | Yes |
| Surveyor | Assigned projects | Yes | No |
| QC | Assigned projects | No | No |
| Client | Assigned projects | No | No |

- Hold requires a nonblank reason of up to 2,000 characters. Resume changes Hold to Ongoing and retains the original reason in history.
- Resume a held report before submitting it.
- Submission requires explicit confirmation and selection of the latest generated draft project inspection PDF. Its UID Number, Document Number and Revision must be present.
- The project must contain equipment. Each equipment item's latest inspection, using the existing HAER inspection history, must be `QcApproved` (legacy workflow) or `Accepted` (inspection activity workflow). Existing QC decisions and defect statuses are not changed.
- Submission creates a new immutable project inspection PDF snapshot from current report data and the selected draft's document details. The archive stores its hash, submitting user and date. Previous draft files remain intact.
- While Submitted, normal project PDF previews/downloads return the submitted artifact, verified against its saved hash. Document-detail generation requests are rejected. Other register exports, inspection workflows and project editing retain their existing behavior; they cannot change the frozen PDF.
- Reopening requires an authorized Admin/Manager and a nonblank reason. It increments the workflow revision and returns to Ongoing. Earlier submitted files and submission events remain available. A newly generated draft is required before resubmission.

The workflow revision counter is separate from artifact version numbers and the user-entered document Revision field.

## API and persistence

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/reports/projects/{projectId}/workflow` | Status, permissions, latest draft, submission details and history |
| PUT | Same route | Change status with `expectedVersion`; Hold also needs `reason`; Submitted needs `confirmSubmission: true` and `draftArtifactId` |
| POST | Same route + `/reopen` | Open a revision with `expectedVersion` and `reason` |

Every transition stores an append-only `ReportWorkflowEvents` record and an existing AuditLog entry, including actor, UTC timestamp, reason, statuses, workflow revision/version and artifact ID. Optimistic concurrency rejects stale writes. Generation and transitions share a per-project lock, and relational transitions use a serializable transaction.

Project list APIs expose `reportStatus` for Admin/Manager and Surveyor project cards. Project Details views show the authoritative workflow read-only. Client Reports also expose this lifecycle status; the earlier on-demand generation description remains available separately as `availability`.

## Migration and deployment

**Migration created, not applied:** `20261111054419_ProjectReportStatusWorkflow`.

It adds only `ProjectReportWorkflows` and `ReportWorkflowEvents` with their indexes and relationships. It does not rewrite existing project, inspection, QC or report archive rows.

Apply the migration before running the updated API. The Development connection resolver restricts local migrations to `(localdb)\MSSQLLocalDB` / `ExSurveyMigrationDesignOnly`. From the API solution directory:

```powershell
dotnet ef database update --project Ex-SurveyApp_api
```

For deployment review, `docs/deployment/report-workflow-idempotent.sql` contains the incremental migration after `InspectionActivityHold`. The existing full `docs/deployment/ex-survey-migrations-idempotent.sql` has also been regenerated. Neither script was executed against any database during implementation. Deploy the matching frontend and backend together.

## Main implementation files

- Backend: `Models/ReportWorkflowModels.cs`, `Controllers/ReportWorkflowController.cs`, `Controllers/ReportsController.cs`, `Data/SurveyDbContext.cs`, the migration and model snapshot.
- Visibility: Project/Surveyor DTOs and controllers, plus Client Reports' status response.
- Frontend: `src/api/reportWorkflow.ts`, `src/components/ReportWorkflowPanel.tsx`, Report Center, project details views and dashboard project cards.

## Verification

Synthetic tests use isolated InMemory databases and do not update local or live SQL databases.

```powershell
dotnet run --project tests/QcWorkflowChecks -c Release -- --report-workflow-checks
dotnet run --project tests/QcWorkflowChecks -c Release -- --project-http-checks
```

Checks cover defaults, mandatory reasons, explicit confirmation, missing draft/document/QC prerequisites, immutable submitted downloads, role and assignment scope, revision reopening, stale/concurrent changes, audit history and unchanged independent status fields. Existing report, defect export, operational report and client checks cover regressions. Frontend tests cover popup cancellation, reasons, submission confirmation, server errors, read-only display, revisions, project-switch races and generation locking.

## Separate existing verification issue

The optional endpoint-scope inventory check currently fails on `EquipmentReferenceDocumentsController.List`, which lacks the inventory classification metadata. This equipment-document controller was not changed by the report workflow. The new workflow controller has its own scope inventory annotation and enforces project access for every action; its authorization and HTTP tests pass.
