# Test Strategy

## Existing and Phase 1 evidence

- Frontend: all 20 Jest suites/75 tests passed; coverage threshold is still not configured.
- Backend: `tests/QcWorkflowChecks/QcWorkflowChecks.csproj`, a custom .NET 10 console runner with in-memory checks and optional SQL paths. It is not xUnit. It contains a mutating SQL repair option; never include that in routine CI.
- Phase 1 checks passed: API/test runner Release builds on .NET 10; bootstrap and OTP security tests; existing QC/project regression suite; SQL Server migration script generation without a database connection; frontend Webpack production build; NPM offline audit (0 findings).
- NuGet audit found vulnerable `System.Security.Cryptography.Pkcs` 10.0.0 transitively via PDFsharp; direct version 10.0.13 was added. The package vulnerability service was not reachable for a final refreshed audit. GitHub Actions and native mobile build jobs are configured but have not run.

## Test layers and gates

| Layer | Required coverage |
|---|---|
| Unit | Bootstrap config validation/idempotency; tenant authorization policy; checklist applicability/calculation; sampling and 90/10 + 5% credit; defect severity and lifecycle; sync idempotency/conflict decisions. |
| API/integration | Role and tenant isolation across every route; QC transitions; immutable inspection attempts; uploads/checksums/retries; import/export; auth throttling; validation and pagination. Use isolated test DB/container, never production. |
| Client component | Web/native parity for login, assigned work, equipment lookup, inspection, photos, QC and client flows; accessibility, keyboard, touch/glove targets and error states. |
| Migration | Empty and upgraded schema; data migration on anonymized representative export; idempotent reruns, delta, orphan handling, rollback, checksums/count reconciliation. |
| Offline/device | App termination/restart, 28-day disconnected scenario as contractually defined, storage exhaustion, device loss, clock skew, duplicate/out-of-order sync, partial media, reader disconnect, tag mismatch and security wipe. |
| Performance/resilience | 50k items/site, 500k/tenant, 2m photos target; scan <1 sec and save <200ms under agreed device/network conditions; 99.5% availability and approved RPO/RTO evidence. |
| Security | OWASP ASVS Level 2 mapping, tenant-crossing tests, threat model, dependency/secret scan, mobile storage review, penetration test, audit retention and recovery evidence. |
| Acceptance | Surveyor, QC, Manager/Admin and Client scenarios; KAS3 2026/2027 counts and contractual allocation; hazard escalation and closure; signed reports/handover pack. |

## Required acceptance invariants

1. A user cannot read, mutate, export, or infer a different tenant’s data, including by changing object IDs.
2. Every inspection remains attributable to a user, device, checklist version, equipment, time and evidence set; QC reviews an immutable version.
3. A sync retry cannot duplicate an inspection, finding, photo, credit or tag assignment.
4. Missing/failed upload is visible and recoverable; no “synced” status before server acknowledgment and checksum validation.
5. KAS3 migration reconciles source rows and media, records exceptions and is repeatable.
6. Safety-critical finding triggers the approved immediate response and cannot be silently closed by an unauthorized role.

## Running existing checks safely

Inspect the console runner’s argument parsing before execution. Use only its in-memory/default mode or a dedicated in-memory bootstrap check. Do not pass SQL connection options, `--repair-sole-reporting-link`, or any production connection. Store test credentials as ephemeral variables/secrets; never commit them. Add test output to CI after the workflow is reviewed.
