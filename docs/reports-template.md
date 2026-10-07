# Project reports

Reports uses the supplied explosion proof survey report as its presentation template: cover, revision history, dynamic contents, introduction, hazardous area plan, area layouts/indexes/registers, defect register and area-grouped inspection checklists. Front matter is unnumbered; body section numbering follows the reference (1 through 5). Cover and body pages are A4 portrait; wide layouts and registers are A3 landscape.

## Use

Open Reports, select a project, press Preview Report, then Generate PDF. Preview PDF opens the generated document with zoom/page controls. Download PDF saves the same generated file. No equipment selection, photo re-upload or duplicate survey entry is required. Refresh/selection changes invalidate the generated file so new project data can be loaded.

## APIs and data

- `GET /api/reports/projects`: accessible projects.
- `GET /api/reports/projects/{projectId}`: one aggregate of existing project, areas, equipment, positions, sessions, saved answers, findings, photos and QC results.
- `GET /api/reports/projects/{projectId}/pdf`: generate and download the reference-style report.
- `GET /api/reports/projects/{projectId}/assets/{key}`: authenticated access to assets included in that project's report.

All routes require authentication. Admin sees all projects; Manager sees projects they created or assigned to their reporting QC users; QC, Surveyor and Client see their assigned projects. Data, PDF and assets share the same project scope. Asset responses never expose physical paths. PDFs are private/no-store.

The loader reads existing tables in batches. PDF generation is read-only; no database migration or Update-Database is required. Project/QC workflow states and uploaded originals remain unchanged. Existing revision records are used only when linked through the project's actual survey sessions and jobs. UID, document number, revision and signatures that are not stored are shown as `-`, rather than copied from the reference.

Saved equipment sequence and XPercent/YPercent determine layout pins. Images retain resolution/aspect ratio; EXIF orientation is respected. Uploaded PDF plans are embedded as original PDF pages. Missing/unreadable assets show a clean message instead of preventing generation. All inspection photos are included; the defect table shows up to two related photos per row, with the complete photo gallery in the inspection section.

Checklist answers come from saved responses. Detailed/Close/Visual columns use each question's saved checklist template, including sessions with no selected template or questions spanning several grades. Unknown grades retain their actual result in Remarks. Stored findings populate the defect register; when an inspection has no finding records, its actual failed answers populate report-only defect rows. This does not create findings or change the dashboard counts. Unavailable recommendations remain `-`.

## Runtime and verification

The API uses PDFsharp for text/tables/vector PDF import and SkiaSharp for lossless image decoding/orientation. Windows uses installed Arial and Times New Roman. Linux needs DejaVu fonts and the appropriate SkiaSharp native assets package before deployment.

From the API repository, build and run the isolated report checks:

```powershell
dotnet build tests/QcWorkflowChecks --no-restore -p:UseAppHost=false -p:OutputPath=bin/QcChecks/
dotnet tests/QcWorkflowChecks/bin/QcChecks/QcWorkflowChecks.dll --report-checks
dotnet tests/QcWorkflowChecks/bin/QcChecks/QcWorkflowChecks.dll
```

Checks cover report access, saved answer/grade mapping, images, multipage PDF plans, long text/table pagination, Swagger and authenticated HTTP data/PDF downloads. The fixture reports are written outside the source repositories under `../tmp/pdfs/reports`.

`--report-sql` is an optional read-only local verification command: it uses the configured SQL connection, selects an accessible existing project and exports a sample to `../output/pdf/ex-survey-project-report.pdf`. It does not update SQL or uploaded files.

Frontend checks run from `ex-survey`:

```powershell
npm.cmd test -- --runInBand ReportsScreen QcPendingNavigation QcPendingBadge QcPendingCount Defects EquipmentType
npm.cmd run build
```

Web uses an authenticated blob download and browser PDF preview. Android uses Download/Ex-Survey; iOS opens the generated file in the system document preview/export flow. Native device behavior needs verification on the target device.
