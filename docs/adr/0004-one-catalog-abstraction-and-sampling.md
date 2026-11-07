# ADR 0004: One catalog abstraction over Oracle and PostgreSQL, with engine-native sampling

- Status: accepted
- Date: 2026-11-07

## Context

The same detection, classification, masking and inventory logic must run on Oracle and PostgreSQL (the two
engines the tool targets), and the scan must stay cheap on large tables: reading every row of a 100-million-row
table to classify its columns is neither necessary nor acceptable on a production replica.

## Options considered

- An ORM / EF Core model: needs a model of the schema up front — the opposite of discovery.
- A dialect switch inside every component.
- A narrow interface (`IRelationalDatabase`) implemented once per engine with raw ADO.NET; everything above it
  dialect-free.
- Sampling: first N rows (`LIMIT` / `FETCH FIRST`), random order (`ORDER BY random()` — a full sort), or the
  engines' block/row sampling clauses (`TABLESAMPLE BERNOULLI` / `SAMPLE (p)`).

## Decision

- `IRelationalDatabase` has eight members: catalog, sample, stream all rows, create table, drop, create empty
  copy, insert, grant. Catalog SQL is engine-native: `information_schema.columns` + `pg_class` (row estimate,
  base tables) + `col_description` on PostgreSQL; `ALL_TAB_COLUMNS` + `ALL_TABLES` + `ALL_COL_COMMENTS` +
  `ALL_TAB_COMMENTS` on Oracle. Comments are read because they are context for the detectors.
- Every value goes through one `ValueFormatter`, so an Oracle `NUMBER(11)` TCKN, a PostgreSQL `varchar`
  TCKN and an in-memory string reach the detectors identically; a cross-engine test asserts the classification
  is the same as the in-memory reference on both engines.
- Sampling: one query per table for all sampleable columns. When optimizer statistics say the table is more
  than 10× the sample size, `TABLESAMPLE BERNOULLI (p)` / `SAMPLE (p)` with `p` sized for ~3× the sample,
  bounded by `LIMIT` / `FETCH FIRST`; small tables are simply read up to the sample size. LOB/binary columns
  are never sampled.
- Inserts are batched natively: multi-row parameterised `INSERT … VALUES` (500 rows) on PostgreSQL, ODP.NET
  array binding (5 000 rows per round trip) on Oracle. Identifiers are always quoted (embedded quotes doubled);
  values are always parameters.
- An `InMemoryDatabase` implements the same interface for unit tests and the sample generator.

## Consequences

- Adding SQL Server would be one class; nothing above the interface changes.
- First-N sampling bias on small tables is accepted (they are read almost entirely anyway); on large tables
  the sample cost stays flat as the table grows (measured in `docs/benchmarks.md`). Without statistics
  (fresh tables) the scanner falls back to first-N.
- Engine differences that surfaced were fixed at the boundary, not in the logic: Npgsql returns `date` as
  `DateOnly` (masking handled only `DateTime` until the cross-engine test caught it); Oracle 23ai's
  `DROP TABLE IF EXISTS` is used, so Oracle < 23 is not supported as is.
