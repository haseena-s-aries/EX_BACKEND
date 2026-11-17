# Registration approval and role change requests

Implemented 13 November 2026. The existing five roles, OTP registration, password hashing, account IDs and project assignments are retained.

## User flows

- Complete the existing registration OTP/password steps. The new confirmation displays the registered email, requested role and **Pending Approval**. The backend keeps the existing `ApprovalStatus.Pending` enum value for compatibility and explicitly returns `accountStatus: PendingApproval`. No token is issued during registration.
- A pending user with the correct password sees **Your account is awaiting administrator approval.** Pending or rejected accounts cannot access authenticated modules.
- Admins receive registration alerts for all roles. Managers receive alerts/counts/list entries for Surveyor, QC and Client registrations only. Managers cannot review Admin or Manager registrations.
- User Management contains Pending Registrations with date/status, approval confirmation and a mandatory rejection-reason dialog. Decisions record reviewer, timestamp and audit history. The user receives a stored notification and a decision email through the existing email service. Mail failures do not undo a saved decision.
- Settings → Request Role Change shows the actual current role, requested-role dropdown, mandatory reason and the user's request history. Only one pending request per user is allowed. Submitting does not change the role.
- Only Admins can list or review all role requests. Approval uses the same shared role-change validation/application logic as existing direct role management. Reporting QC users must be reassigned before demoting their Manager. The last active approved Admin cannot be demoted. A stale request whose original role/account has changed must be rejected and resubmitted.
- Approval revokes old JWTs through AuthVersion as well as the existing role-claim validation. Users sign in again for the new permissions. Rejection preserves the existing role. User IDs and project/job assignment records are not rewritten; any existing ManagerId cleanup on leaving the QC role follows the existing role-management behavior.
- Reviewed requests and audit records remain stored. Requests prevent deleting their requesting/reviewing accounts through the existing dependency checks.

## Notifications and counts

The previous registration alerts were derived from pending-list responses and read state was device-local. New registration and role-request alerts are persisted in UserAccessNotifications, with a unique recipient/event key. The migration backfills one alert per existing pending registration/eligible approver; it does not invent historical registration dates.

The existing bell combines these alerts with existing project/inspection notifications. New alert read/dismiss operations persist on the backend and enforce recipient ownership. Project/inspection notification behavior remains unchanged. User Management's sidebar badge counts only authorized pending registrations; the Admin-only role-request tab has a separate pending count. These are not added together. Counts poll every 20 seconds; the bell keeps its existing 60-second polling and refreshes immediately after changes made in the same app instance. Cross-device changes appear on the next poll.

## APIs

| Path (under /api) | Access / behavior |
| --- | --- |
| auth/approvals/pending | Admin/Manager; role-scoped pending registrations, including RegisteredAt |
| auth/approvals/{id}/approve | Admin/authorized Manager; existing PUT route |
| auth/approvals/{id}/reject | Admin/authorized Manager; PUT now requires `{reason}` |
| user-access/counts | Admin/Manager; separate pendingRegistrations/pendingRoleChanges |
| user-access/role-requests | GET own history; POST `{requestedRole, reason}` for active approved users |
| user-access/role-requests/all | Admin-only GET |
| user-access/role-requests/{id}/review | Admin-only PUT `{decision: Approved or Rejected, reason}` |
| user-access/notifications | GET own accessible durable alerts |
| user-access/notifications/{id}/read | PUT own alert read state |
| user-access/notifications/{id}/dismiss | PUT own alert dismissal |

## Database and deployment

Migration: `20261113072459_UserRegistrationAndRoleRequests`.

Adds nullable Users.RegisteredAt and ApprovalRejectionReason, RoleChangeRequests and UserAccessNotifications. Existing roles, approval enum values, users and assignments are preserved. A filtered unique index enforces one Pending role request per user. Concurrency checks protect approval/role decisions from duplicate processing.

Local ExSurveyMigrationDesignOnly was migrated during implementation. No live database was modified. For live ExSurveyApp, back up and validate on staging, then run `deployment/user-registration-role-requests-idempotent.sql` in SSMS SQLCMD Mode (or sqlcmd -b). It requires AccountProfileAndRecovery; apply `account-profile-recovery-idempotent.sql` first if that baseline is missing. Deploy backend and frontend together because registration rejection now requires a reason. The full deployment script includes all 18 migrations.

## Verification

`dotnet artifacts/user-access/QcWorkflowChecks.dll --user-access-checks` runs isolated HTTP checks with real controllers, JWT authorization and captured mail. Covers registration OTP/completion, pending login/access, Manager scope, unique alerts, approval/rejection metadata, notification ownership/read/dismiss, role validation/duplicate requests, Admin-only review, stale JWT rejection, assignment preservation and audit history. Existing account-security checks also pass.

Frontend tests cover confirmation, role-request validation/history, Manager tab visibility, rejection reasons and decision refresh. Frontend regression coverage: 202 tests across 46 suites, including server-backed notification read/dismiss checks. TypeScript passes. Production web build is checked. Real email delivery and interactive browser/native-device testing remain deployment checks; automated tests do not send real messages.
