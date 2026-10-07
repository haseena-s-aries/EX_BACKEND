# Defects Flagged

The Manager/Admin dashboard reads `GET /api/defects?openOnly=true`.
The response contains server-generated `summary` counts and a list of existing `Finding` records.

Open means `FindingStatus.Open` or `RectificationSubmitted`. The latter still awaits verification.
Verified and Closed findings remain accessible under All Defects but are excluded from open counts.
Each finding counts once. QC comments, review statuses and requested changes do not create findings.
Severity uses High, Medium or Low; empty or unrecognized severities are displayed as Unspecified.

Admin sees all projects. Manager scope matches existing QC monitoring: projects created by that Manager,
or projects whose assigned QC reports to that Manager. The same scope protects individual defect photos.
Photo metadata exposes authenticated API URLs; physical storage paths are never returned.
Images are loaded only when the user opens the existing full-screen photo viewer.

The list is read-only and reuses existing finding statuses. It does not introduce another defect lifecycle,
automatically resolve a finding on QC approval, or change checklist answers or equipment.
Reported date and due date are explicitly inspection dates: the existing Finding model has no separate
creation timestamp, defect assignee or defect resolution deadline. No new fields or migration are added.

Dashboard reload/pull-to-refresh and the list's Refresh action reload backend counts. Existing data remains unchanged.
