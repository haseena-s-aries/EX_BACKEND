# Defect Register report exports

Report Center → Defect Reports offers Register Preview, CSV, PDF and Excel for the selected project.
Rebuild/restart the API and deploy the updated frontend together. No schema migration is required by this feature.

## Routes

All routes require authentication and the existing project report permission check.

| Method | Route under `/api/reports/projects/{projectId}` | Result |
| --- | --- | --- |
| GET | `/defect-register` | Complete register preview |
| GET | `/defect-register.csv` | UTF-8 CSV with 16 columns |
| GET | `/defect-register.pdf` | A3 landscape PDF |
| GET | `/defect-register/excel` | Genuine XLSX workbook |
| GET | `/defect-register/photos/{fileId}` | An associated attachment, checked against this project's register |

PDF and Excel files use the existing immutable artifact archive. They appear in Recent Reports and can be downloaded through the existing authenticated archive endpoint. PDF previews reuse the archive viewer. Concurrent archive requests are serialized per project; relational revision allocation also uses a serializable transaction. The frontend locks export buttons while generating.

## Data and layout

- Manual register findings, historical inspection findings and inspection activity findings are read without reconciliation or mutation.
- A bridged legacy finding is represented by its activity finding rather than exported twice. Multiple corrective action references and statuses are retained in the same defect row.
- The Item Number comes from HAER `EquipmentCode`. Missing AOSD IDs display `NA`; other missing text displays `—`.
- Both remarks columns and explicit photo slots remain separate. Activity photos are restricted to the same checkpoint definition or activity-level evidence; corrective evidence is restricted to the finding's own actions.
- PDF and Excel retain actual status names. Item and fault-category cells are yellow, closed statuses green, and headers grey-blue. The HAER export's white header is unchanged.
- Client branding comes from the scoped project's uploaded logo; the configured company logo remains on the right.
- PDF pages repeat the header. Oversized rows use labelled continuation segments at the same readable font size. Photos preserve aspect ratios and are embedded for offline viewing.
- Excel preserves values as text, wraps cells, freezes the header and embeds images using DrawingML anchors. Up to three images per photo slot fit within Excel's row-height limit. Larger groups, non-image attachments, unreadable or missing image files retain explicit filename/authenticated-reference text instead of overlapping cells or disappearing. PDF attachments remain attachment references in the PDF register.
- CSV preserves filenames in the two photo columns; it contains no image or cell styling. Formula-leading strings are escaped. Use XLSX when Excel must retain typed text identifiers such as leading zeros.

## Changed implementation

- `Controllers/OperationalReportsController.DefectRegister.cs`
- `Services/DefectRegisterReportData.cs`
- `Services/DefectReportExports.cs`
- `Services/DefectWorkbook.cs`
- Frontend: `src/api/reports.ts`, `src/screens/ReportsScreen.tsx`, `src/components/DefectReportRegisterPreview.tsx`

## Verification

Synthetic in-memory checks do not connect to a local or live SQL database:

```powershell
dotnet run --project tests/QcWorkflowChecks -c Release -- --defect-report-checks
dotnet run --project tests/QcWorkflowChecks -c Release -- --project-http-checks
dotnet run --project tests/QcWorkflowChecks -c Release -- --report-checks
dotnet run --project tests/QcWorkflowChecks -c Release -- --operational-report-checks
```

Frontend tests cover the four actions, selected-project requests, export locking, error recovery and history refresh. Generated fixtures are also checked with an independent XLSX/CSV reader and rendered PDF inspection; fixtures contain synthetic data only.
