# QC shared Surveys, Defects and Checklists

QC now reuses the existing ProjectsScreen, ProjectDetailsScreen, DefectRegisterScreen and EquipmentCategoryChecklistScreen. The sidebar module order and existing QC Pending/review screens are retained.

## Read scope and permissions

- Project list/details, photos, common layouts, report status and equipment use the stored Primary QC or Additional QC project relationship.
- Read access was enabled for QC on the existing defect register, registration-project picker, HAER equipment register, checkpoint catalog/selection, and project schedule endpoints.
- Selection reads validate the active approved actor, existing tenant/project authorization, and equipment-to-project relationship. The catalog remains shared reference data.
- The QC checklist uses read-only saved checkboxes/categories. It can filter categories and view saved selections, but cannot save mappings, edit definitions, publish or create an inspection through the category setup endpoints.
- QC does not gain project creation/deletion, team assignment, area/layout uploads, equipment metadata editing, register creation/editing or checklist catalog writes.
- Existing QC inspection approval and corrective-action verification are retained. Inspection-linked defects open their existing Workflow view from the shared register.
- QC team names come from the existing scoped QC project details endpoint; no global assignment directory is exposed.

## Verification

- Frontend: all 44 suites / 185 tests passed, including shared QC navigation, category filtering without writes, saved-selection retention, scoped team names, read-only project details, and correction verification from the shared defect register.
- Backend: 47 HTTP assertions passed using isolated in-memory fixtures. Checks cover Primary and Additional QC projects, unassigned and mixed project/equipment IDs, photo reads, shared catalogs, and HTTP 403 on restricted writes. Browsing leaves fixture row counts unchanged.
- Existing backend workflow checks passed, including Surveyor/Admin defect editing, QC review, photo-slot operations, and SQL Server query translation.
- No SQL database was migrated or modified for this change. There is no new migration.
- Browser screenshot verification was not performed; the user previously selected automated verification.

Deploy the updated frontend and backend together. Restart/recycle the API after deployment to load the additional scoped GET permissions. The currently running API is not automatically replaced by source edits.
