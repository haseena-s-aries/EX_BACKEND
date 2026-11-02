# Architecture Decisions and Proposals

This is a decision log for modernization. “Proposed” items remain subject to the open decisions; they are not silently treated as approved requirements.

| ID | Decision/proposal | Rationale | Status |
|---|---|---|---|
| ADR-01 | Preserve React Native/React Native Web + TypeScript and ASP.NET Core/EF Core/SQL Server for the first modernization increments. | These are working existing applications; a rewrite would add migration and delivery risk. The Aries proposal’s Kotlin/Nest/Postgres options are not a demonstrated need. | Proposed; revisit only with measured constraint. |
| ADR-02 | Keep a modular ASP.NET monolith initially; split services only for a demonstrated scale/ownership boundary. | Existing transactional workflows and small evidence base favor fewer operational components. | Proposed. |
| ADR-03 | Model tenant → contract/project → site/area → equipment; every business record must derive and enforce tenant ownership. | Requirements call for tenant separation; current code has none. Exact mapping of Project to contract/site needs business approval. | Blocked by ownership and identity decisions. |
| ADR-04 | Add server-side authorization scope checks and database-enforced parent consistency; do not trust client-supplied tenant IDs. | Prevent cross-tenant access and invalid project/area/equipment links. | Required design principle. |
| ADR-05 | Treat each inspection attempt as immutable/versioned; QC review references a specific attempt/version. | Preserve repeat annual history and prevent edits to evidence under review. | Proposed; align with acceptance and retention policy. |
| ADR-06 | Version checklist template, question set, applicability and fault rules; store the exact version on each inspection. | Reproducibility and auditable IEC checklist changes. | Proposed; technical authority must approve content. |
| ADR-07 | Offline design should use durable local encrypted storage, an idempotent outbox, server acknowledgments, conflict rules, resumable media and visible sync state. | `SyncBatch` alone is not a sync system; offline operation must survive app restart and network loss. | Blocked on edge scope, device/platform and authority decisions. |
| ADR-08 | RFID/NFC/QR/manual identification should share one asset-resolution workflow with explicit unregistered/wrong-project warnings and tag assignment history. | Current UID-only online lookup cannot support the expected field process. | Blocked on reader/tag/standard and procurement scope. |
| ADR-09 | Retain local-server/edge as an optional deployable topology unless contract rebaseline makes it mandatory. | Bumi calls it optional; Aries makes it Phase 1. | Open contract decision. |
| ADR-10 | Migrate with additive schema, reversible stages, stable source IDs, checksums, dry runs and reconciled acceptance. | Preserve items, histories, photos and open defects while enabling rollback. | Required approach; source export missing. |
| ADR-11 | Store media in durable object storage or equivalent replicated storage with private access, hashes, retention and resumable upload. | Current local filesystem is not evidence of multi-node durability. | Proposed; hosting/residency decision required. |
| ADR-12 | Introduce integration adapters with an outbox/retry/dead-letter model once ERP/CMMS is selected. | No current integration target or retry semantics exist. | Deferred until ERP/CMMS decision. |
| ADR-13 | Upgrade from .NET 8 to .NET 10 LTS, with EF Core and SQL Server provider 10.0.13. | Maintain a supported runtime and validate provider/query/migration behavior. | Implemented and local regression-tested; JWT bearer remains 8.0.0 pending online dependency review. |
| ADR-14 | Persist OTP attempt count, expiry lock and concurrency version; throttle anonymous authentication endpoints per client IP. | Reduce OTP guessing, credential stuffing and stale concurrent verification bypass. | Implemented; five attempts per code and 20 requests per 5-minute IP window; production proxy/forwarded-header behavior remains an operational check. |
| ADR-15 | Keep secrets outside tracked JSON; use environment variables/Secret Manager, and ignore local production settings. | Prevent credentials from being reintroduced in repository changes. | Implemented for tracked SMTP/JWT/Development bootstrap keys; live rotation and history remediation remain owner actions. |

## Edge, RFID and offline scope conflict

Bumi proposal `11 (1).pdf`, §4 p.13 explicitly labels the web-based local server/Ex register (RFID and edge sync) optional. The Aries architecture, p.6, positions tablet-edge-central and RFID/offline as Phase 1. The ToXSL proposal describes a different hardware/ERP/server assumption set. Confirm contract priority, budget, hardware supply, hosting ownership, support responsibility and acceptance criteria before implementation. The product must distinguish software reader integration from physical tags/readers/tablets procurement.

## Deployment topology options to decide

| Option | Fit | Trade-off |
|---|---|---|
| Central cloud/API only | Simplest initial operations and release model. | Does not meet disconnected operation without substantial device-side capability. |
| Site edge node + central service | Supports local continuity and site-bound synchronization. | Adds edge hardware, patching, backup, security and conflict/ownership duties. |
| Managed tablet offline + central service | No fixed site server; encrypted tablet queue. | Device management and long-duration media/storage limits; weaker local multi-user continuity. |

Do not select until the operational offline requirement, 28-day interpretation, devices, networks, data residency, and vendor responsibility are explicit.
