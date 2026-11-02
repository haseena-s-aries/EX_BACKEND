# Phase 2D — Inspection Activity Foundation

**Status:** Additive domain/persistence preparation implemented locally. No new controller route or UI workflow is active. No tenant enforcement was enabled. No migration was applied to any database.

## Scope delivered

The backend now has an additive domain path:

```text
InspectionPlan → InspectionTask → InspectionActivity → ActivityRevision
  → CheckpointResponseRevision → ActivityFinding → CorrectiveAction
  → CorrectiveActionEvent → Client decision
```

Activities link to a checklist version snapshot. Checkpoint definitions and responses keep question/version, applicability, response code, technical condition, workflow state, observation, actor and timestamp as separate data. `ActivityEvidence` reuses existing `SurveyFile` IDs and can describe checkpoint, original, before, after, correction, certificate or other evidence roles. The model does not duplicate blob storage.

Plans/tasks can point to modern `AreaEquipment` or legacy `Equipment`; a database check constraint requires exactly one equipment reference. The original IDs remain unchanged. `LegacySurveySessionId` is an optional unique bridge; no existing session is converted automatically. Repeated activities use independent GUID activity IDs and are allowed by the new model.

## Review and corrective action

Activity decisions are represented separately from checkpoint technical condition and workflow state. Review decisions have an actor, timestamp and comments. Corrective actions have an action reference, supplementary text, proposed action, opaque external work-order/SAP reference, responsible user, Aries verification fields and client decision fields. Append-only event rows retain decision and evidence history.

The preparatory policy requires a `Client` role, assignment to the owning project, and a state eligible for client decision. Activity acceptance does not mutate activity findings or close actions. This is domain-level preparation only: no existing Finding, QC or route behavior was changed, and no new route currently invokes the policy. Client assignment must be resolved from persisted project ownership when an API is implemented.

## Tenant ownership path

New activity records do not accept or store a caller-supplied tenant ID. For a modern equipment pin, the prepared resolver follows task → `AreaEquipment.ProjectId` → existing `TenantProjectBinding`; an optional plan project must agree. For legacy equipment it requires a unique verified `LegacyEntityMap` to a project and a matching tenant binding. Missing/conflicting maps fail closed in that resolver. This does not turn on tenancy policies or establish SQL-backed isolation; Phase 2C's feature flag remains disabled.

## Compatibility and safety

- Existing `SurveySession`, `SurveyAnswer`, `Finding`, `QcReview`, `AuditLog`, files, APIs, roles, reports and UI remain the active path.
- The unique filtered `SurveySession.AreaEquipmentId` index is unchanged. The migration adds no required column to an existing table and no backfill.
- `Visual`, `Close` and `Detailed` reuse the existing `InspectionType` enum. No MODEC condition/response labels or IEC classification values are seeded.
- SAP/work-order values are stored as unvalidated references only. No connector or ERP calls were added.
- New domain types are not yet wired into dependency injection, endpoints, reports or mobile/web screens.

## Test evidence

Release builds succeeded for the API and `QcWorkflowChecks`. The new synthetic EF InMemory suite now passes 22 checks covering separate Visual/Close activities on one equipment, stable distinct IDs, revision uniqueness, response revision history/reviewer metadata, two findings from one checkpoint, evidence/action events, accepted activity immutability guard, acceptance not closing findings, client-only closure policy, grade/checklist validation, stale token rejection, absence of client tenant fields, disabled feature gate, existing unique index presence and tenant resolution through stored parent data. The full existing backend workflow runner also passed.

These are code-level tests with synthetic in-memory data. They do not prove SQL Server migration correctness, persisted tenant isolation, API behavior for the new model, or physical backup/restore.

## SQL rehearsal and migration status

The non-destructive local SQL diagnostic still reports SqlClient error 20: “The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.” No query was executed. No migration, backup or restore was run. Strict TLS settings were not weakened.

The Phase 2D migration and SQL review script were subsequently scaffolded with repository-local EF Core CLI 10.0.13. The generated model snapshot is synchronized (`has-pending-model-changes` reports no differences). The SQL script remains for review only; no database migration was applied.

## Reference coverage

The MODEC activities report was reviewed previously and its observed Close/Visual cards, checkpoint condition/state separation, correction narrative/photos and example fields are recorded in the comparison register. A single WhatsApp-labeled local JPEG was checked and was unrelated to the inspection workflow; the two WhatsApp ZIPs and `Sample checklist.pdf` were not available. They remain unreviewed. The current signed BRD is also unavailable.

## Remaining gates

1. Provide/review both WhatsApp archives, the sample checklist and signed BRD.
2. Approve activity/reinspection versus QC resubmission semantics; checklist version publication; response, applicability, condition and grade vocabularies.
3. Approve KAS3 corrective-action, Aries verification, client closure/reopen and responsible-party rules.
4. Regenerate and review EF migration metadata/model snapshot, then rehearse it against an isolated SQL Server using trusted TLS, including existing migration history, FK/index validation, workflow regression and backup/restore.
5. Define transactional application persistence and authorize new API/mobile/web workflows separately.
6. Complete tenant ownership backfill and independent persisted-data route isolation before activating tenant enforcement.
