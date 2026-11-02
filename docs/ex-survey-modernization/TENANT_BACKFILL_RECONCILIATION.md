# Tenant Backfill Reconciliation

**Run status: not run against application data.** No source database connection succeeded and no sanitized project snapshot/crosswalk was supplied. No ownership mapping was approved or applied.

## This run

| Measure | Result |
|---|---:|
| Projects / areas / equipment read | Not available |
| Approved mappings evaluated | Not available |
| Proposed bindings / already applied | Not available |
| Unresolved/exception rows | Not available |
| Rows written | **0** |
| Production or other DB access | **None** |

`Not available` is intentional; it does not mean zero source records or zero issues. The SQL Server TLS/SSPI blocker and missing approved crosswalk prevent data-based reconciliation. See `PHASE2B_MIGRATION_REHEARSAL.md` for exact diagnostic evidence.

## Utility behavior verified with synthetic fixtures

- Approved ownership yields a deterministic project binding proposal.
- Missing/duplicate/unapproved mappings, contract/tenant mismatch, conflicting current ownership, orphan areas and cross-project equipment are blocking exceptions.
- A matching existing mapping is idempotent and yields no additional proposal; a conflicting mapping is rejected.
- The transaction coordinator rolls ownership and audit changes back together after an injected mid-batch failure; a successful synthetic run commits both.
- The standalone command accepts only `--dry-run`, reads a JSON snapshot and writes a JSON report. It has no DB provider, connection string or apply flag.

## Next reconciliation

After local SQL/TLS setup is repaired, restore a sanitized database backup into the isolated rehearsal database and export the current project/area/equipment records without changing them. Obtain Aries/KAS3-approved tenant, contract and site crosswalks with evidence and approval metadata. Run the utility, review every exception with data owners, then regenerate the report from the same source snapshot and mapping version before any separately authorized execution path is designed.
