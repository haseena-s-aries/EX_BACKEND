# Project UI adjustments — 2026-10-07

Source backup was created and hash-verified before changes:
`C:\Users\AL414\source\repos\backups\project-ui-20261007-085840`.

- Create/Edit QC options display the saved user's `DisplayName`, explicit `QC` role and reporting `ManagerName`. Existing approved/active role filtering and `AssignedQcId` saving are preserved. No additional User/Manager property was added.
- Client is optional in Create/Edit and both JSON/multipart APIs. Empty frontend selection becomes `null`; multipart omits the field. A selected Client must still be an approved, active Client user. Surveyor and QC remain required.
- `OptionalProjectClient` changes only `Projects.AssignedClientId` to nullable. The migration was applied to the configured development SQL database. Existing assignments were retained. Its rollback requires assigning Clients to projects without one before restoring the non-null column. Other databases can use `optional-project-client-migrations.sql` after the HazardousAreaPlan migration.
- Project Name, Job Number, Location and Vessel/Site Name use uppercase input values and backend normalization for create/edit. Existing rows are not bulk-rewritten. Sector, people's names, identifiers and filenames retain their case.
- The optional plan upload retains JPG/JPEG/PDF validation and original-byte storage. Only Choose File, format guidance and a selected attachment indicator are displayed. No filename or technical metadata is rendered on the details page. View Plan loads the original file only when opened and retains image/PDF zoom controls.
- Area uploads show a selected attachment indicator rather than filename details. View Layout reuses the existing ZoomableImageViewer. Project/Surveyor layout viewers receive existing equipment-pin coordinates; original pin selection/placement and Equipment CRUD remain unchanged. The existing QC viewer and inspection flow were retained.

SQL read-only verification confirmed the existing QC account is literally named `QC`, and its reporting Manager is named `manager`. These are the saved profile names, so the resulting option is `QC — QC — Manager: manager`. Correct real names must be supplied before changing those profile records; example names have not been substituted.

Validation: frontend create form/optional payload/clean viewing/layout-pin tests plus existing Surveyor/QC/Manager tests; backend optional-client JSON/multipart/create/edit/readers, original-file and QC workflow checks; backend and production web builds; live Swagger confirmed HTTP 200 and nullable/optional Client in the request schema. Full TypeScript checking retains existing DOM/OTP errors outside these changes.
