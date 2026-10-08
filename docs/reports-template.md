# Project reports

Reports uses the supplied explosion proof survey report as its presentation template: cover, revision history, dynamic contents, introduction, hazardous area plan, area layouts/indexes/registers, defect register and area-grouped inspection checklists. Front matter is unnumbered; body section numbering follows the reference (1 through 5). Cover and body pages are A4 portrait; wide layouts and registers are A3 landscape.

The first page uses the user-supplied cover artwork in `Ex-SurveyApp_api/Assets/Reports/cover.png`, embedded into the API assembly as `ExSurvey.ReportCover`. The image is fitted within the A4 page with its aspect ratio preserved. Project/title details are drawn as sharp PDF text. The lower-left cell contains the selected company's name, logo, address, email, telephone, fax and website; the lower-right cell contains the client. The original top-right ARIES image remains fixed and untouched, including when no company is selected. Full company contacts appear in the lower company block. Missing company details/logo use clean placeholders. The original three-row grid is filled in this order: Prepared by / UID Number / Date; Checked by / Job Number / Total Pages; Approved by / Document Number / Revision. Each label has its adjacent value column, with text fitted inside the exact cell bounds. Unavailable UID/document values remain `-`. Update the image and rebuild the API to change this cover background. See `our-company-details.md` for the optional company relationship and migration.

## Use

Open Reports, select a project, press Preview Report, then Generate PDF. Preview PDF opens the generated document with zoom/page controls. Download PDF saves the same generated file. No equipment selection, photo re-upload or duplicate survey entry is required. Refresh/selection changes invalidate the generated file so new project data can be loaded.

## APIs and data

- `GET /api/reports/projects`: accessible projects.
- `GET /api/reports/projects/{projectId}`: one aggregate of existing project, areas, equipment, positions, sessions, saved answers, findings, photos and QC results.
- `GET /api/reports/projects/{projectId}/pdf`: generate and download the reference-style report.
- `GET /api/reports/projects/{projectId}/assets/{key}`: authenticated access to assets included in that project's report.

All routes require authentication. Admin sees all projects; Manager sees projects they created or assigned to their reporting QC users; QC, Surveyor and Client see their assigned projects. Data, PDF and assets share the same project scope. Asset responses never expose physical paths. PDFs are private/no-store.

The loader reads existing tables in batches. PDF generation is read-only. Project/QC workflow states and uploaded originals remain unchanged. The optional Project metadata/client-details addition uses migration `20261007120651_ProjectReportMetadataAndClientDetails` (already applied locally); see `project-client-details.md` for other databases. The revision page displays one centered row from the current project's UID, revision, report date, description and prepared/checked/approved values, in a compact centered table with REVISION HISTORY in its first full-width row, bold column labels and clean black borders. Body-page headers use Project Name on the left and the existing Site Name on the right, closely aligned above the header rule; the bottom-left footer reads ARIES MARINE. Cover UID/document/revision/client name and logo now use the stored Project fields. Unavailable values remain `-`, rather than copied from the reference.

Saved equipment sequence and XPercent/YPercent determine layout pins. Images retain resolution/aspect ratio; EXIF orientation is respected. Uploaded PDF plans are embedded as original PDF pages. Missing/unreadable assets show a clean message instead of preventing generation. The defect table retains up to two related photos per row. Standalone inspection-photo gallery pages are omitted after section 5; checklist data, comments, QC details, area dividers, layout images and uploaded originals remain unchanged.

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

Area equipment indexes use three equally spaced vertical blocks on A3 landscape pages, with light blue headers, centered saved sequence/ID/type values and compact wrapped rows. Items fill each block top-to-bottom (up to thirty rows per block, fewer when wrapping requires space), then continue into the next block/page with repeated headers. The existing equipment ID/tag fallback is retained.

The Hazardous Area Equipment Register has a bordered header panel: dynamic client logo, centered fixed register title, dynamic company logo, and stacked Vessel/Site, Location and Inspector (Prepared By) rows. The blue equipment-table headers follow directly underneath. This panel repeats on register continuation pages; absent or unreadable logos leave blank cells. Other table/header/footer rendering retains its previous behavior.

The EX Hazardous Area Equipment Defect Register repeats a bordered header with its centered title, selected company logo and three stacked rows (Ex-Surveyor/Prepared By, Vessel/Site, Location). Missing logos remain blank. Existing defect cells, columns, photo rendering, observations, recommendations, status and remarks are unchanged.
