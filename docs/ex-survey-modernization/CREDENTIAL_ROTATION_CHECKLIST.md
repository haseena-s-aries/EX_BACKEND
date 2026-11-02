# Credential Rotation Checklist — Repository Owner

**No live credential was rotated by this change.** Values are intentionally not reproduced. Treat any value that appeared in tracked files or Git history as exposed until the owner verifies its scope and rotation.

## Non-sensitive verification record (Phase 1)

- Current committed `appsettings.json` and `appsettings.Development.json` no longer contain the SMTP username/password, JWT signing key or bootstrap account password fields. The committed settings were checked by key name only; values are never printed in the audit record.
- `appsettings.Production.json` is ignored local configuration. Its contents were not opened or changed. The .NET SDK had copied it into API/test build and publish output folders (including stale .NET 8 outputs); those generated copies were removed, and the API project now excludes it from future build and publish output. The repository owner must privately verify the original local file's credentials exist only in the approved secret store.
- Local Gitleaks CLI was not available in the environment. GitHub secret scanning workflow is configured but has not run, so this is not a full current-tree/history secret-scan result. The owner must review the first workflow result and scan prior Git history/CI artifacts privately.
- Generated copies of the ignored production settings were found and removed from API/test build and publish folders. A subsequent .NET 10 Release build verified that neither API nor test output re-copied it. Other build outputs remain untracked/ignored unless explicitly listed by Git.
- Use the checklist below to record only provider, secret identifier or vault path, environment, rotation timestamp, verifier and pass/fail. Never put the credential value in the record.

## Before rotating

- [ ] Identify environments and owners for each credential below; check repository history and CI logs privately.
- [ ] Prepare replacement values in the approved secret manager or host environment; do not send them in chat, tickets, logs, or source control.
- [ ] Plan a brief overlap window where the provider supports dual credentials, plus a rollback contact.
- [ ] Coordinate API restart/token invalidation and mobile/API clients as needed.

## Rotate and verify

- [ ] **JWT signing key** — set `Jwt__SigningKey` in the secret manager/runtime environment. Use a cryptographically random key of at least 32 bytes. Coordinate invalidation/reissue of all existing tokens and restart every API instance.
- [ ] **SQL Server login/password, if SQL authentication is used** — set `ConnectionStrings__SurveyDatabase` outside JSON. Prefer managed identity/least privilege where supported. Confirm application connectivity using a non-production environment and owner-run production validation.
- [ ] **SMTP credentials** — set `EmailSettings__SmtpUsername` and `EmailSettings__SmtpPassword`. Confirm registration email delivery in a non-production environment.
- [ ] **Bootstrap Admin account** — set `BootstrapAdmin__Email` and `BootstrapAdmin__Password` only for the intended bootstrap window; remove/disable after verifying the owner-controlled account process.
- [ ] **Bootstrap Manager account** — set `BootstrapManager__Email` and `BootstrapManager__Password` only if required; remove/disable after bootstrap verification.
- [ ] **Credential verification evidence** — for each secret, record provider/vault reference, environment, rotation date, old-secret revocation evidence, verifier and pass/fail. Do not record secret values, full connection strings, one-time codes, token contents or screenshots that reveal them.
- [ ] **Local production configuration** — privately review the existing untracked `appsettings.Production.json`; do not add it to Git. Move any real values into the corresponding environment variables/secret manager.
- [ ] Revoke old values at their providers after replacement is verified; inspect provider audit logs for unexpected use.
- [ ] Decide with the repository owner whether Git history must be rewritten. A new clean file does not remove old values from prior commits; coordinate clones, forks, release tags and backups before history changes.

## Runtime configuration reference

ASP.NET Core loads environment variables using double underscores for nested keys. The committed `appsettings.json` no longer supplies the SMTP username/password or JWT signing key; Development JSON no longer supplies bootstrap account credentials. Local developers should use `dotnet user-secrets` or environment variables. Production secrets belong in the deployment secret manager, never in JSON tracked by Git.

## Owner sign-off

- [ ] Provider rotation complete and verified.
- [ ] Old credentials revoked.
- [ ] Token invalidation/reissue complete where applicable.
- [ ] Git-history remediation decision recorded.
- [ ] CI secret scan reviewed; historical findings resolved or tracked with approved remediation.
- [ ] GitHub secret scan completed for current files and relevant history/artifacts; any finding is handled by repository owner without automatic history rewrite.
