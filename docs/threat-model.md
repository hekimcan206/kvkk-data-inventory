# Threat model

A personal-data discovery and masking tool is itself a high-value target: it is granted read access to the
most sensitive tables, and it produces the copy of production that developers and vendors get. This document
lists what is protected, from whom, and how. All data in this repository is synthetic.

## Assets

| Asset | Where | Why it matters |
|---|---|---|
| Source personal data, incl. special categories (KVKK Art. 6) | Oracle / PostgreSQL source schema | The reason KVKK applies; leaks are reportable breaches (KVKK Art. 12/5) |
| Masking key | `KVKK_MASKING_KEY` environment variable | Whoever holds it can confirm guesses against the masked copy |
| Masked copy | target schema | Handed to developers / testers / vendors; must not re-identify people |
| Scan and classification reports, VERBİS inventory | files, API responses | Map of where the sensitive data is — useful to an attacker, but contain no values |
| Database credentials | connection strings in env vars | Owner / scanner / masker accounts |

## Actors

- **Developer / tester / vendor** with access to the masked copy (honest-but-curious, or careless).
- **Operator** running scans and masking jobs.
- **External attacker** reaching the API or stealing reports.
- **Insider with the key** (operator, security team).

## Threats (STRIDE) and mitigations

| # | Threat | Mitigation | Residual risk |
|---|---|---|---|
| S1 | Spoofing a database to harvest what the scanner sends | The scanner only reads; it sends nothing but SQL and bound parameters | Use TLS / Oracle native encryption in real deployments (not configured in the demo) |
| T1 | Masking job writes into production by mistake | Copy-to-masked-schema only; runner refuses `source == target`; masker account can write only its own schema; source is never `UPDATE`d (ADR 0002) | A misconfigured grant that gives the masker write access to the source |
| T2 | SQL injection through table/column names or values | Identifiers come from the catalog and are always quoted with embedded quotes doubled; all values are parameters; CLI/API take only schema names | — |
| R1 | No record of what was scanned or masked | Structured JSON logs (Serilog) and OpenTelemetry traces/metrics for scans; CLI writes scan / classification files with timestamps | No tamper-evident audit store in this repo |
| I1 | Scan output becomes a copy of the data | Reports carry counts, scores and reasons only; samples live in memory per table; a test asserts no TCKN appears in a serialised scan; `/detect` responses never echo values; request bodies are never logged (ADR 0003) | Free-text column comments are copied into reports as metadata |
| I2 | Re-identification from the masked copy without the key | HMAC-SHA256 with ≥ 256-bit key; domain separation per type; a test shows a dictionary attack with a wrong key recovers nothing; special categories nulled by default | Quasi-identifier combinations (birth year + gender + province) can still narrow people down: no k-anonymity check yet |
| I3 | Re-identification by a key holder | Pseudonyms are one-way (HMAC, not FPE), so the key decrypts nothing | A key holder can mask candidate values and compare — feasible for the whole TCKN space. Treat the masked copy as **pseudonymised personal data** while the key exists; destroy or rotate the key after producing a copy meant to be anonymous |
| I4 | Scanner sees more than it should | Separate scanner account with SELECT via a role; catalog views filter by privilege; a test proves an ungranted table is invisible and a `DELETE` fails | DBAs can still grant too much |
| I5 | Key leakage | Read only from an environment variable, never from CLI arguments (shell history, process lists) or config files; never logged; `.env` is git-ignored, `.env.example` holds a clearly-labelled demo key | Environment variables are visible to the same OS user |
| D1 | Scan overloads a production database | One sampling query per table, `TABLESAMPLE` / `SAMPLE` on large tables, bounded rows, configurable parallelism (default 4) | Run against a replica where possible |
| D2 | Oversized API requests | 2 MB request limit; `/detect` limited to 1 000 samples | No authentication or rate limiting in the demo API (it must sit behind a gateway) |
| E1 | API used to scan arbitrary databases | The API can only scan databases configured server-side, with the read-only scanner account | — |

## Out of scope (stated, not solved)

- Authentication / authorisation on the API (demo service; deploy behind an identity-aware gateway).
- Encryption in transit to the databases in the demo compose file.
- Key management (KMS / HSM / Vault integration, rotation).
- Legal advice: categories, purposes, legal bases and retention periods in `samples/inventory-policy.json` are
  illustrative.
