# Inspection Activity and Checkpoint Data Model — Proposal

**Architecture proposal with Phase 2D additive local domain implementation.** Phase 2D adds domain entities, EF relationships, application validation/policy helpers, a review-only migration draft and synthetic tests. No endpoints, UI, reports, production data or existing SurveySession/QC behavior changed. WhatsApp ZIP screenshots, sample checklist and current signed BRD remain unavailable. This is not a claim that names, scales or transitions are contract-approved.

## Existing compatibility anchors

| Existing entity | Current behavior | Proposed compatibility treatment |
|---|---|---|
| `AreaEquipment` / legacy `Equipment` | Modern pin and legacy asset tables both exist; the modern entity contains RFID UID, manufacturer, type, certificate and protection detail. | Keep original IDs. New activity links to one authoritative equipment reference chosen by source-model discriminator + stable ID until legacy mapping decision is approved. Do not merge records by tag/name. |
| `SurveySession` | Session has equipment/job links, surveyor, optional template, mutable status, timestamps; unique index limits one session per modern equipment. | Preserve current rows and API behavior. Add optional one-to-one compatibility link from a new `InspectionActivity` to an existing session for migrated/current workflow representation. Do not remove the unique index until an approved additive migration/rehearsal and client cutover plan are ready. |
| `SurveyAnswer` + `ChecklistQuestion` | One answer per session/question; response is string `Value` and `Comment`. Question has template ID, text, sort and required flag. | Preserve as legacy session payload. New versioned checkpoint results retain immutable question/version references and do not reinterpret old free-text values or condition labels. |
| `QcReview` + `AuditLog` | One QC review per session; mutable current review status/comments and separate action audit records. | Keep current workflow. New activity review events append decisions and reference actor/time; migration must capture known existing review state/history without inventing missing events. |
| `Finding` + `SurveyFile` | Findings have status/recommendation; files may link to session, finding, equipment, area or job. | Keep existing Finding IDs and stored evidence. New corrective action may point to a Finding; file links are represented by an explicit evidence association with before/after/correction roles. Preserve old file records and URLs. |

## MODEC training report evidence mapping

The supplied report (pp. 2–7) gives one concrete example, not a universal rule set:

- Equipment `SWE-50528` has two distinct activity cards: an Ex Close schedule and an Ex Visual schedule, each with performer, start and completion timestamps. This demonstrates repeated activity history for this sample item.
- The activity card includes checklist category and an activity-level acceptance by `EIT_ARIES` with comment. Response rows separately show checkpoint, response/comment, date, condition and state. Keep activity review status separate from checkpoint condition and checkpoint workflow state.
- The T2.A4 example has supplementary information, corrective-action text mentioning an SAP work order, performed-correction text and photos. The report does not expose an SAP order number, assigned responsible party, reviewer rejection, client closure or complete audit timeline.
- The equipment section shows group, SAP location description/code/name, Ex register description/location, elevation, SafeEx planning location, Maker/OEM, certificate, ignition-capable value, RFID/integration notes and IEC type. Treat these as source-specific field examples; infer no ignition classification, SAP field rule or requiredness.
- The report filters completion dates and activity review status, and groups activity data by equipment. It does not establish an approved campaign export or final per-equipment PDF acceptance criteria.

These observations support a plan/task → activity → checkpoint result / response condition → corrective action and correction evidence model, while leaving grade/condition values and role transitions under technical/business approval.

## SafeEx public object-model comparison

The SafeEx Cloud and Integrability (2024 v2) public PDF describes a corresponding vocabulary: `Equipment` / `Equipment Group`; `Plan`, `Plan Task`, regular and corrective plan-task items; `Checklist`, `Checklist Category`, `Check`; `Activity`; `Response`; `Response Condition`; and `Response Correction`. Its glossary describes an Activity as checklist output for an equipment item, a Check as a specific checkpoint, a Response as the check outcome, a Plan Task as inspection or correction work, and a Response Correction as a correction of a failed response. This supports separating an inspection occurrence, checkpoint result, response condition, scheduled task and correction history in the proposed model.

These are vendor-specific object terms, not a requirement to copy their schema. The response examples “Passed”/“Failed” and repair text are examples only; Aries technical authority must approve any Ex-Survey code lists. The public SafeEx model does not establish the requested KAS3 client-only closure rule or the exact role/approval sequence.

## Planning objects

To preserve the SafeEx distinction between a planned task and completed inspection evidence, the future additive design should include:

- `InspectionPlan` (or approved campaign/work package): named planning scope, client-approved time window and schedule policy. Avoid repeating schedule dates as equipment metadata.
- `InspectionPlanTask`: one assigned unit of work for one equipment item, with a task kind (`RegularInspection` or `CorrectiveAction` stored as an extensible approved code), due/scheduled time, assigned user/party, source reference and workflow status. A regular task may produce one `InspectionActivity`; a corrective task references a `CorrectiveAction` and its evidence/events. A task can be rescheduled without rewriting earlier activity data.
- The business decision must define whether one task can produce multiple activities, whether resubmission is a new activity revision, and what happens to overdue/cancelled tasks. These are not inferred from the public diagram.

## Proposed entities

### InspectionActivity

One planned or performed inspection instance for one asset. Suggested fields: `Id`; `EquipmentEntityType` + `EquipmentId`; optional `ProjectId`; `GradeCode` (controlled reference, not hard-coded enum until approved); `ScheduleId`/`WorkPackageId` nullable; `InspectorUserId` plus captured display name; `StartedAt`, `CompletedAt`; immutable `ChecklistTemplateVersionId`; `ReviewStatusCode`; optional `LegacySurveySessionId`; `CurrentRevisionNumber`; `CreatedAt`.

Constraints: asset reference must resolve to one canonical equipment row; `StartedAt <= CompletedAt` when both exist; grade and review status use approved vocabularies; any session link is unique; activity revisions unique by `(InspectionActivityId, RevisionNumber)`. Do not enforce schedule/work package FKs until those entities and their cardinalities are approved.

### InspectionActivityRevision

Immutable snapshot per submitted/revised activity: revision number; submitted/completed timestamps; inspector snapshot; grade; template version; reason/source (initial, correction resubmission or approved revision); content hash; predecessor revision; created actor/time. QC request-changes must create a new revision or draft successor without overwriting the previously reviewed snapshot. The distinction between independent repeat inspection and QC resubmission is still OQ-30.

### ChecklistTemplateVersion and CheckpointDefinition

Represent a published immutable checklist version and stable checkpoint code/version. Store text, sort, applicability expression/code, requiredness and an approved response-scale reference. A version cannot be edited after use; corrections publish a new version. Do not introduce IEC A/B/C labels, limits, units, applicability rules or example MODEC condition labels as defaults. Validation rules should be data driven from an approved version, with effective dates and author/approver audit.

### CheckpointResult

Belongs to one activity revision and one checkpoint definition version. Fields: response-scale code; optional technical condition code; observation; recorded timestamp and actor; applicability outcome/reason; optional numeric value + approved unit; review state where required. Keep **technical condition** separate from **workflow/review status**. Missing/not-applicable is not the same as a failed technical condition. The permitted condition values and response semantics remain pending technical authority approval.

### CheckpointEvidence and CorrectiveActionEvidence

Explicit joins to existing stored files, with evidence role (`checkpoint`, `before`, `after`, `repair`, `certificate`, `other`), captured timestamp, actor, caption and integrity hash. Keep existing `SurveyFile` storage IDs; do not duplicate blobs. Preserve upload and authorization behavior; new resource authorization must be tenant scoped before exposure after tenancy gates pass.

### CorrectiveAction and CorrectiveActionEvent

Action aggregate linked to a Finding and optional activity/checkpoint. Suggested fields: stable action reference; supplementary information; proposed action; external work-order/SAP reference stored as opaque text until ERP mapping approval; responsible party (approved identity/reference); current workflow status; Aries verification and verifier/time; client decision and client actor/time; rejection reason; reopen reason; created/updated metadata. Append-only events preserve submission, assignment, evidence, verification, client acceptance/rejection, reopening and closure decisions. Enforce KAS3 client-only final closure after role mapping is approved; never infer authorization from a supplied client ID.

### Equipment metadata extension

Proposed optional attributes: SAP functional location ID and description; Ex register location; equipment group; OEM/manufacturer; certificate number; Ex protection type; ignition-capable classification plus source/technical-validation state; RFID notes. Prefer a normalized external identifier mapping table for SAP source system + identifier + entity, and a separate reviewed metadata record if values have effective dates. Do not declare global uniqueness or infer ignition capability until the client/technical authority approves semantics. No SAP API/client credentials/connector are included.

## Relationship overview

```text
InspectionPlan
  └─ InspectionPlanTask (regular or corrective)
       ├─ InspectionActivity (inspection execution output)
       └─ CorrectiveAction (repair/follow-up execution)

Equipment (modern AreaEquipment or approved legacy Equipment mapping)
  └─ InspectionActivity (many; independent occurrence)
       ├─ InspectionActivityRevision (one per submitted/reviewed content revision)
       │    └─ CheckpointResult (many; exact definition version per result)
       │         ├─ CheckpointEvidence ── SurveyFile (existing blob metadata)
       │         └─ CorrectiveAction (optional)
       ├─ ActivityReviewEvent (append-only approval/comments/history)
       └─ LegacySurveySessionId (optional compatibility bridge)

Finding (existing)
  └─ CorrectiveAction (one/many per approved business rule)
       └─ CorrectiveActionEvent (append-only) ── CorrectiveActionEvidence
```

## Migration and behavior gates

1. Add new tables and nullable bridge metadata only; do not drop/rename current records, relax authentication, change role checks, or activate tenant policies.
2. Backfill one compatibility activity per existing `SurveySession` only after an explicit migration rule is approved; preserve session, answer, review, finding and file IDs. Do not create inferred historical revisions or timestamps.
3. Seed no grade/condition/checklist rules from the MODEC example. Publish approved checklist versions from signed technical data only.
4. Keep current endpoints/UI behavior on the legacy tables until parity and acceptance tests pass. Any dual write must be transactional and idempotent; otherwise use a controlled cutover window rather than ambiguous dual authority.
5. Rehearse against an isolated SQL Server and compare row counts, FKs, unique constraints, rollback and backup restore before production authorization.

## Minimum acceptance scenarios

- Multiple completed activity records for one equipment retain unique dates, inspector, grade and template version.
- Existing `SurveySession` and QC request/resubmit/approve sequences retain prior behavior and IDs.
- A checkpoint result remains tied to the exact immutable published question version and response scale.
- Technical condition does not change when workflow review state changes.
- Corrective action supports repair evidence and before/after linkage; only an authorized client closes; rejection and reopen append events.
- Report output shows activity and checkpoint history without overwriting older results.
- Cross-tenant or unowned assets and attachments are denied once tenant enforcement is enabled; before then existing route behavior is preserved.
