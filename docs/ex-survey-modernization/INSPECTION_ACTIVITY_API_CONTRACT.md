# Inspection Activity API Contract — v1 Draft

**Status:** DTOs, validation, application services and a draft OpenAPI 3.1 file are implemented locally. Every proposed path remains inactive. No controller routes were added and Swagger does not publish these paths.

## Activation gates

Both configuration gates default to false:

- `InspectionActivities:Enabled` — blocks the application service by default.
- `TenantFoundation:Enabled` — required by the server-side activity authorizer.

There are no activity controllers/endpoints in MVC. Do not set either flag to activate these APIs until the Phase 2D migration has been rehearsed, ownership mappings are available, tenant authorization is tested against persisted rows, and product/QC/client roles are approved. Existing surveyor, QC, client, report and project routes are unchanged.

## Proposed paths

| Method and path | Purpose | Expected role from current application role set |
|---|---|---|
| `POST /api/v1/inspection-plans` | Create a project-bound plan | Admin or Manager |
| `POST /api/v1/inspection-tasks` | Add a task for one modern or legacy equipment ID | Admin or Manager |
| `POST /api/v1/inspection-tasks/{taskId}/activities` | Start Visual, Close or Detailed activity | Assigned Surveyor; Admin/Manager preparation access |
| `POST /api/v1/inspection-activities/{activityId}/checkpoint-responses` | Append versioned checkpoint response | Assigned Surveyor; Admin/Manager preparation access |
| `POST /api/v1/inspection-activities/{activityId}/findings` | Add finding from a checkpoint response | Assigned Surveyor; Admin/Manager preparation access |
| `POST /api/v1/inspection-activities/{activityId}/evidence` | Link an existing authorized `SurveyFile` | Surveyor/QC/Manager/Admin, tenant scope checked |
| `POST /api/v1/corrective-actions` | Create action for a finding | Surveyor/Manager/Admin, tenant scope checked |
| `POST /api/v1/corrective-actions/{actionId}/submit-for-verification` | Submit action after after/correction evidence is linked | Responsible or task-assigned Surveyor; Manager/Admin |
| `POST /api/v1/inspection-activities/{activityId}/submit` | Submit completed activity | Assigned Surveyor; Admin/Manager preparation access |
| `POST /api/v1/inspection-activities/{activityId}/review` | Record InReview, ChangesRequested or Accepted | QC/Manager/Admin, tenant scope checked |
| `POST /api/v1/corrective-actions/{actionId}/verification` | Record Aries verification | QC/Manager/Admin, tenant scope checked; role mapping needs approval |
| `POST /api/v1/corrective-actions/{actionId}/client-decision` | Accept/reject closure or reopen | Client assigned to the owning project only |

Route definitions and request/response schemas are in `INSPECTION_ACTIVITY_API_V1.openapi.yaml`. C# v1 request/response DTOs are in `Ex-SurveyApp_api/Contracts/V1/InspectionActivityContracts.cs`.

## Ownership and authorization

- No request DTO has a tenant ID, tenant key, actor ID or reviewer ID field. Actor identity comes from the authenticated claim and is reloaded from `Users`.
- Actor must be active, approved and hold the allowed existing `UserRole`.
- A validated tenant context must already exist. Project/task/activity/action/file ownership is re-resolved from stored records. Missing, conflicting or cross-tenant ownership fails closed.
- Modern ownership follows `InspectionTask → AreaEquipment.ProjectId → TenantProjectBinding`. Legacy equipment requires a unique verified crosswalk to the project and a matching tenant binding.
- The assigned-client check reads `Project.AssignedClientId`; client-supplied identity is never accepted as authority.
- Any future controller must apply the approved tenant resource authorization policy in addition to role authorization before calling the service.

## Validation and workflow rules

- A task has exactly one modern `AreaEquipmentId` or legacy `EquipmentId`.
- Activity grade is limited to the existing `Visual`, `Close`, or `Detailed` enum and must match the persisted checklist version's grade.
- Start/completion timestamps must be valid. Required checkpoint responses are checked before submission.
- Checkpoint definition must belong to the activity's exact checklist snapshot. Response and technical-condition codes are opaque/configured values; no MODEC/IEC labels are seeded.
- Response revisions append; activity writes include a base64 SQL rowversion token. Stale token yields a conflict response. SQL-backed race behavior remains untested.
- Corrective action submission for Aries verification requires an `After` or `Correction` evidence link in its event history. Aries verification only accepts an action already in `SubmittedForVerification`.
- Accepted activities are immutable through the application rules. Repeat inspection uses another activity ID.
- Review acceptance changes activity review state and appends a review event; it never closes or mutates findings/actions.
- Aries verification is a separate step. Only the project-assigned Client may formally accept closure, reject closure or reopen; rejection/reopen requires a reason.

## Current API gaps before activation

- The expected tenant scope currently relies on a future controller applying the Phase 2C tenant policy; no controller exists in this package.
- Reviewer role mapping and the precise Aries verification role need business approval.
- Corrective-action transition/deadline, assignment and rejection semantics need KAS3/QC approval.
- No GET/list/report routes, pagination, idempotency key, request correlation standard, client-visible error envelope, mobile/web caller or API contract tests against persisted SQL data are implemented.
- OpenAPI is a review file and is not registered with live Swagger. Existing routes are not modified.
