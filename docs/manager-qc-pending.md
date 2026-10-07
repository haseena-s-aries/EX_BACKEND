# Manager QC Pending

**QC Pending** performs QC work. It reuses `QcDashboardScreen`,
`QcProjectDetailsScreen` and the original `/api/qc` controller actions. The existing
**QC Review** menu continues to provide read-only monitoring and reporting-manager setup.

## Access

- QC users access projects where `AssignedQcId` is their user ID.
- Managers access projects whose assigned QC has `ManagerId` equal to their user ID.
- Admin accesses all QC projects.
- Creating a project does not grant a Manager QC action permissions if its assigned
  QC reports to another Manager. Reporting reassignment changes action access immediately.

The same access rule applies to dashboard/projects, project details, area details,
equipment inspections, inspection photos, start-review, approve, request-changes
and comments. Surveyor and Client remain excluded from QC endpoints.

## Shared workflow

Manager and QC actions operate on the same `QcReview`. Managers may start review,
save comments, select severity for a change request, request changes and approve.
The actual acting user is stored as reviewer and in the existing audit history.

`PendingReview → InReview → ChangesRequested → PendingReview` after Surveyor resubmission,
then `InReview → Approved`. Equipment continues to expose `QcApproved` for approved inspections.
There is no Manager-specific status or approval stage. Approved inspections remain locked,
including for Manager and Admin.

The QC Pending dashboard uses the existing QC design and backend inspection aggregates:
Awaiting QC Review, Changes Requested, Approved and Overdue. QC Pending uses the existing
`GET /api/qc/projects?pendingOnly=true` API. It includes projects with submitted inspections
in PendingReview, InReview or ChangesRequested; it excludes draft-only and fully approved
projects. The normal QC assigned-project list remains available with no pending filter,
and its existing approved-project history remains intact. The frontend renders the
backend result without applying a Manager-specific filter.

`Project.AssignedQc` is now an EF navigation over the existing `AssignedQcId` foreign key;
Manager access follows `AssignedQc.ManagerId`. This adds no SQL columns or foreign keys.

## Use

1. Restart/rebuild the API and restart the frontend after pulling these source changes.
2. Existing `User.ManagerId` and audit-history migration from the earlier enhancement
   must already be applied. This permission/navigation change adds no migration.
3. Admin can configure reporting relationships under **QC Review → Reporting managers**.
4. Sign in as Manager, open **QC Pending**, select a project, select an area and review
   an equipment inspection using the existing QC actions. Admin has the same menu.

Verification extends `tests/QcWorkflowChecks` with mixed Manager/QC review cycles,
all out-of-scope reads/actions/photos, reassignment, Admin scope and approved locks.
Frontend tests cover the shared pending dashboard, sidebar visibility and navigation
to/from the existing review screen. Checks use EF InMemory and SQL query translation;
live browser/device acceptance has not been performed. SQL verification is available:

```powershell
dotnet .\tests\QcWorkflowChecks\bin\QcChecks\QcWorkflowChecks.dll --verify-sql
```

The read-only check verifies ManagerId/AssignedQcId columns, saved assignments, EF
navigation and actual pending queries for Manager and QC. In the local database checked
on 6 October 2026, the sole QC account (`QC`) had a NULL ManagerId. It was assigned to the
sole approved Manager (`manager`) using the existing Admin assignment action; the previous
value was saved at `C:\Users\AL414\source\repos\backups\qc-reporting-link-20261006-132242.json`.
Existing project QC assignments and inspection/review statuses were not changed.
