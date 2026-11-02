# Inspection Activity Schema Review

**Purpose:** Review the proposed Phase 2D additive relational model against current Ex-Survey tables. This is a review artifact, not migration approval.

## Existing record preservation

| Existing record | Current responsibility | Phase 2D treatment |
|---|---|---|
| `AreaEquipment` | Current project equipment pins and RFID fields | New tasks may reference its original ID; its project is the proposed ownership path. |
| Legacy `Equipment` / `Area` / `Job` | Older equipment tree | Tasks may reference original Equipment IDs. Tenant/project ownership requires a verified crosswalk; no name inference. |
| `ChecklistTemplate` / `ChecklistQuestion` | Mutable legacy template and question rows | Remain unchanged. New published snapshot and checkpoint definition rows capture an immutable version separately. |
| `SurveySession` | Existing one-per-equipment survey execution and API flow | Kept unchanged. Optional `LegacySurveySessionId` bridge can reference an existing row but no rows are backfilled. |
| `SurveyAnswer` | One free-text answer per session/question | Kept unchanged. New append-only `CheckpointResponseRevision` records have response, technical condition and workflow state separately. |
| `Finding` | Current defect/finding status and recommendation | Kept unchanged. `ActivityFinding` can bridge by optional unique existing finding ID. New actions never automatically change legacy status. |
| `QcReview` / `AuditLog` | Current QC lifecycle and existing audit entries | Kept unchanged. `ActivityReviewEvent` / `CorrectiveActionEvent` are separate new history records. |
| `SurveyFile` (`Files`) | Existing stored file metadata and storage key | Reused by `ActivityEvidence.SurveyFileId`; file blobs are not copied. Existing endpoints are unchanged. |

## Additive table inventory

| New table | Primary relationships / constraints | Purpose |
|---|---|---|
| `InspectionPlans` | Optional existing `Project`; project index | Schedule/work-package grouping. |
| `ChecklistVersionSnapshots` | Existing `ChecklistTemplates`, publisher `Users`; unique template/version | Published checklist identity/hash, without changing old template rows. |
| `CheckpointDefinitionSnapshots` | Checklist version; unique version/question-code/question-version | Stable checkpoint code/version, text and configured applicability/scale references. |
| `InspectionTasks` | Plan; modern `AreaEquipment` or legacy `Equipment`; optional assigned `User`; exactly one equipment FK via check constraint | Planned regular or corrective work item and schedule references. |
| `InspectionActivities` | Task; inspector; checklist snapshot; optional unique legacy session bridge | One historical inspection occurrence; existing Visual/Close/Detailed grade enum. |
| `InspectionActivityRevisions` | Activity and author; unique activity/revision number | Append-only content version/hash and change reason. |
| `ActivityReviewEvents` | Activity and reviewer; activity/time index | Decision/comment/actor/time history. |
| `CheckpointResponseRevisions` | Activity revision, checkpoint definition and actor; unique activity-revision/check/revision | Immutable response revisions with independent applicability, response, condition and workflow values. |
| `ActivityFindings` | Activity, checkpoint response; optional unique existing `Finding` | Multiple checkpoint findings while preserving legacy IDs. |
| `CorrectiveActions` | Finding; optional responsible user, verifier, client decision user; unique action reference | Proposed action, external work-order reference, current state and decision fields. |
| `CorrectiveActionEvents` | Action and actor; action/time index | Append-only event history. |
| `ActivityEvidence` | Existing `Files` row; optional checkpoint/action event and actor | Evidence role, caption, integrity hash and timestamp. |

All declared delete behaviors are `NO ACTION`/restrict for the new graph. The migration does not drop a table, alter an existing column/index, assign owners, populate historical activities or apply to a database.

## Required relationship checks before production design

1. The task's equipment target must be exactly one existing modern pin or legacy equipment row. For a modern equipment pin, validate that the task/plan project agrees with `AreaEquipment.ProjectId`. For legacy equipment, only verified ID crosswalks can resolve the owning project. Current database FKs cannot enforce the project equality across task and equipment; application validation and future composite constraints need a separate review.
2. Each checkpoint response must reference a checkpoint included in its activity's exact checklist version. A normal FK does not prove that the response's activity revision uses that same checklist version. Add service validation or a reviewed composite-key constraint before endpoint writes are enabled.
3. Activity review acceptance and corrective-action closure are separate. The closure policy requires a persisted assigned-client check and a prior eligible Aries verification state; its current isolated policy helper is not wired to an endpoint.
4. Event rows and revisions are append-only by application contract in this package. SQL permissions/trigger-based immutability and tamper evidence are not configured.
5. `ProjectId` is optional on `InspectionPlan`; when supplied it must match its task equipment's project. No project/tenant fields are copied to checkpoints or actions; ownership propagates through the task's stored equipment parent.
6. Tenant resolution is preparatory only. Modern path: activity → task → AreaEquipment → Project → TenantProjectBinding. Legacy path requires a verified Equipment → Project crosswalk and matching tenant binding. There is no tenant FK on any existing table, and no tenant enforcement is active.

## Migration artifact review

`20261030104113_InspectionActivityFoundation.cs` creates only the 12 tables and their constraints/indexes listed above. A standalone SQL review copy generated by EF tooling is in `20261030104113_InspectionActivityFoundation.review.sql`. Its `Down` removes only these new tables in dependency order. Do not run either artifact against production.

The migration was scaffolded using repository-local `dotnet-ef` 10.0.13. Its designer and `SurveyDbContextModelSnapshot.cs` were generated from the current model; `has-pending-model-changes` reports no differences. Comparison with the former 12-table hand-authored draft found the same table inventory, with the generated version also including SQL Server `rowversion` concurrency metadata and correctly named event-actor relationships. The generated `Up` contains 12 `CreateTable`, 35 `CreateIndex`, 0 `AddColumn`, and 0 destructive/rename/raw-SQL operations; `Down` drops only the 12 new tables. This has not been applied or rehearsed against SQL Server.

## Deliberate omissions

- No sample MODEC condition or response code is seeded.
- No A/B/C fault class, Ex protection classification or ignition-capable rule is encoded.
- No tenant ID is accepted from clients or stored on the activity graph.
- No SAP/ERP calls, device/RFID integration, report template or API route is added.
- No existing session uniqueness index is dropped or relaxed.
