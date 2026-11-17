# Account profile and password recovery

Implemented 13 November 2026 for Admin, Manager, Surveyor, QC and Client. Settings contains My Profile, Security and Appearance. Existing role permissions, approval status, assignments and saved theme preferences are retained.

The Login → Forgot Password journey now stays on the same screen: enter an email, request a verification code, then enter the code, new password and confirmation. The email contains a copyable 16-character hexadecimal code grouped as XXXX-XXXX-XXXX-XXXX. The code has 64 bits of cryptographic randomness, expires after 20 minutes and is stored only as a hash, bound to the account email and token purpose. Requests retain generic responses, existing rate limits and per-account cooldowns. Resend is available after 60 seconds; changing the email clears the entered code/passwords. A successful reset signs out previous online sessions and returns to Login. Existing emailed-link endpoints remain compatible with already-issued links.

This same-screen update reuses AccountActionTokens and requires no additional database migration beyond AccountProfileAndRecovery. SMTP is still required to verify ownership of the email; entering an email alone cannot change its password.

## Deployment

1. Back up the live ExSurveyApp database and validate the migration on staging.
2. For a database already through `20261112045739_ProjectMultipleClients`, run `docs/deployment/account-profile-recovery-idempotent.sql` in SSMS with SQLCMD Mode enabled (or sqlcmd with `-b`). It explicitly targets ExSurveyApp and checks the baseline. Do not manually insert migration-history rows.
3. Deploy the updated API and frontend together. Restart the API application pool.
4. Confirm `AccountSecurity__PublicAppUrl=https://139.5.189.155:339/` and the existing SMTP settings on the deployed host. The production appsettings default uses this frontend URL; Development uses `http://localhost:3000/`. IIS environment settings can override it. Use a certificate trusted by clients.
5. Test reset and verification delivery using a real eligible account and the production SMTP service.

Migration `20261113044054_AccountProfileAndRecovery` adds Users.PhoneNumber, Designation, ProfilePhotoPath, AuthVersion and the AccountActionTokens table. It does not drop or rewrite existing users. It has already been applied to local ExSurveyMigrationDesignOnly. The live database was not modified during implementation. Restart the existing local API process to load the new endpoints.

## Endpoints

All paths below start with `/api/account`.

| Method / path | Access | Purpose |
| --- | --- | --- |
| GET /profile | Authenticated own user | Actual profile and pending email |
| PUT /profile | Authenticated own user | Full name, phone and designation only |
| PUT /profile/photo | Authenticated own user | Multipart field `photo`, JPG/JPEG/PNG up to 5 MB |
| DELETE /profile/photo | Authenticated own user | Remove own photo |
| GET /profile/photo | Authenticated own user | Protected image response |
| POST /forgot-password | Anonymous, rate limited | Generic confirmation; queue reset email for eligible local accounts |
| POST /reset-password | Anonymous, rate limited | Validate token, replace password, invalidate tokens and online sessions |
| POST /forgot-password-code | Anonymous, rate limited | Request an emailed code for the same-screen reset form |
| POST /reset-password-code | Anonymous, rate limited | Validate email, code and matching passwords before resetting |
| POST /change-password | Authenticated, rate limited | Verify current password before replacing it |
| POST /email-change | Authenticated, rate limited | Reauthenticate and send verification to the new address |
| POST /verify-email | Anonymous, rate limited | Activate verified email and revoke previous online sessions |

## Security and behavior

- Reset/verification tokens use 32 cryptographically random bytes, expire after 20 minutes and are single use. Only SHA-256 hashes are stored. Purposes are checked separately; password changes invalidate all outstanding account tokens.
- Links use the configured frontend origin. Production requires HTTPS; redirects supplied by requests are not accepted. Tokens are carried in URL fragments and removed from browser history when the frontend reads them. Verification requires an explicit confirmation action.
- Anonymous reset responses do not reveal account existence. Existing request rate limits apply, with additional per-account issuance limits of five tokens per hour and a one-minute cooldown for the same purpose.
- Passwords use the existing password hasher and existing nonempty password policy (maximum 256 characters). No new sixteen-character minimum is introduced. Emails never contain passwords.
- JWTs include AuthVersion, checked against the current account on authenticated requests. Password changes and verified email changes revoke online sessions, including the current one. Previously issued JWTs without the claim remain compatible only while AuthVersion is zero. Existing offline credential/cache behavior is unchanged; an offline device cannot receive immediate server revocation until it reconnects.
- Only active, approved accounts can use local password recovery. No account is activated or assigned a new role through these endpoints. There is no existing external SSO integration; accounts without a local password are directed to their identity provider/administrator instead of being given a separate local password.
- New email addresses do not replace the active login identifier until verified. Duplicate addresses are rejected. The previous address receives change notifications.
- Profile photo storage reuses existing protected storage and image signature checks. Profiles accept only whitelisted fields; roles, account status and assignments remain controlled by existing administration.
- Startup bootstrap provisioning now links the existing bootstrap account once using an audit marker. Subsequent restarts preserve profile edits, verified emails, passwords and account state. Explicit account-provisioning commands remain separate.

## Mail operations

Forgot-password messages use a bounded, in-memory background queue to avoid waiting on SMTP in the anonymous response. Queued messages are not durable across process restarts; users can request another link after the cooldown. Delivery errors are logged without message bodies or tokens. A full queue still returns the same generic confirmation. Email-change delivery failures return a clear error while leaving the active login email unchanged. Existing SMTP configuration must be valid; automated checks capture messages without sending real emails.

## Verification

- Isolated HTTP checks exercise actual controllers, JWT validation, password hashing, protected photo storage and captured emails for all five roles, using a test-only in-memory database.
- Checks cover unknown/disabled/unapproved accounts, expiry, single-use and concurrent token consumption, password login after reset, revoked JWTs, email-change verification and replay, photo validation/replacement/removal, profile authorization, startup bootstrap persistence, SMTP failure and rate limiting.
- Frontend tests cover profile save/cancel, staged photo changes, password validation, generic recovery confirmation, redirect after reset, authenticated avatar updates and client mobile Settings callbacks. TypeScript and production web builds are checked.
- Existing workflow/bootstrap regression checks pass. Production SMTP delivery and interactive browser/native-device testing remain deployment checks; no live messages or live database changes were performed.
