# Proposed BRD Addendum — Inspection Activities and Corrective Actions

**Draft for business review; not approved and not merged into the client BRD.** No current BRD file was present in the repository or identified among available files. Public SafeEx sources support planning/tasks, repeated inspection history, check/response/correction, activity/grade, evidence, offline use, reports and API integration. The supplied MODEC training report demonstrates two activities/grades on one item, separate activity acceptance and check states, a correction narrative with photos, example SAP/Ex fields and report filters. The requested WhatsApp screenshots and sample checklist remain unavailable. Confirm against the signed BRD and customer source artifacts before baselining. SafeEx currently labels Cloud 4.0 as “coming”; public descriptions do not prove feature availability in a specific deployed version.

## Proposed requirements

1. Retain multiple inspection activities for the same equipment. Each activity records equipment identity, approved inspection grade, schedule/work package where applicable, inspector, start/completion time, immutable checklist template version, review state, comments, approval history and historical revisions.
2. Preserve checkpoint-level code/version, applicability, response from an approved scale, observation, recorded time, evidence links, corrective-action link and review state. Technical condition and workflow status are independent values.
3. Track corrective-action supplemental information, action reference, proposed action, optional external work-order reference, responsible party, correction evidence, before/after evidence, Aries verification and client closure decision. Rejection and reopening retain full audit history. KAS3 final defect closure remains client-only unless the signed contract says otherwise.
4. Allow approved SAP functional location/description, Ex register location, equipment group, manufacturer/OEM, certificate number, Ex protection type, technically validated ignition-capable classification and RFID notes. SAP integration is excluded until ERP and field mappings are approved.
5. Provide equipment activity history, checkpoint results, review/acceptance, corrective-action history, before/after evidence, grade history and date/equipment/review-status filters; support detailed per-equipment PDF and consolidated campaign export using neutral styling.

The public 2024 SafeEx model describes separate `Equipment`, `Plan`/`Plan Task`, `Checklist`/`Check`, `Activity`, `Response`, `Response Condition` and `Response Correction` objects. Use this as a comparison vocabulary for the design only; it is not a requirement to copy the SafeEx schema or their example response labels.

## Approval notes

These proposals do not establish IEC question text, Ex classifications, inspection grades, condition labels, response semantics, due-date rules, SAP schema, campaign definition, report branding or workflow roles. Technical authority, Aries product, QC, client/KAS3, ERP and report owners must approve the applicable items. MODEC example labels must not become universal defaults.

## Source merge checklist

Before this becomes BRD text, attach and cite the two SafeEx WhatsApp archives, `Sample checklist.pdf`, `MODEC GUY Uaru EWU Training - Activities report 2026-10-29.pdf`, and the signed current BRD; record exact page/screenshot references and reconcile each proposal in `SAFEEX_COMPARISON_REQUIREMENTS.md` against Aries FR/NFR IDs.
