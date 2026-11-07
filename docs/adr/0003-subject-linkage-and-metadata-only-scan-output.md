# ADR 0003: Data-subject linkage, metadata-only scan output and least-privilege accounts

- Status: accepted
- Date: 2026-11-07

## Context

Three questions that are about KVKK rather than detection:

1. "Diyabet" in a patient's diagnosis column is health data — a special category under KVKK Art. 6. "Diyabet"
   in an ICD-10 code list is reference data about no one. A detector looking at one column cannot tell them
   apart.
2. A discovery tool reads personal data in order to find it. If its output contains the sampled values, the
   scan report becomes an uncontrolled copy of the most sensitive data in the company.
3. A discovery tool connected with a DBA account can read everything and change everything.

## Options considered

- Linkage: (a) ignore it and accept false positives on every catalog table; (b) require declared foreign keys
  to a person table (many legacy schemas declare none); (c) a table-level rule using detected direct
  identifiers and person-reference column names.
- Output: (a) store samples for reviewers; (b) store hashed samples; (c) store counts, scores and reasons only.
- Accounts: (a) one privileged account; (b) separate owner / scanner / masker accounts.

## Decision

- **Linkage rule (c).** A table is *Direct* if any of its columns is classified as a direct identifier (TCKN,
  name, e-mail, phone, IBAN, card), *Indirect* if a column name is a person reference (`hasta_id`,
  `personel_no`, `customer_id`, `employee_ref` …), otherwise *None*. Special-category findings in *None*
  tables are not classified; they are listed as `suppressed` with the reason, so a reviewer can still see them.
  The ablation in `docs/benchmarks.md` shows the effect.
- **Metadata-only output (c).** `ScanReport` and `ClassificationReport` carry catalog metadata, per-detector
  scores, matched/sampled counts and a reason string. Sampled values exist only in memory while one table is
  evaluated. A unit test asserts that no generated TCKN appears in a serialised scan; the API's `/detect`
  responses and its request logging never echo values either.
- **Three accounts (b)**, created by the same init scripts in docker compose and in the Testcontainers tests:
  - owner (`clinic_owner` / `CLINIC`): owns the source schema; used only to seed the synthetic data;
  - scanner (`kvkk_scanner`): `CREATE SESSION` / `LOGIN` + role `kvkk_read` (SELECT on granted tables). It sees
    only what it was granted — `information_schema` and `ALL_TAB_COLUMNS` filter by privilege — and a test
    proves an ungranted table is invisible and a `DELETE` is rejected;
  - masker (`kvkk_masker` / `KVKK_MASKED`): `kvkk_read` + ownership of its own target schema only (no
    `CREATE ANY TABLE` on Oracle).

## Consequences

- The ICD-10 catalog no longer counts as health data; a health column in a table with no identifier and no
  person reference would be missed (it is suppressed, not lost: it appears in `suppressed`).
- Reviewers who need examples must look in the database with their own access — by design.
- Granting the scanner role is an explicit, auditable decision about the scan's scope.
- Person-reference names are a heuristic; declared foreign keys from the catalog are a listed next step.
