# Changelog

All notable changes to this project are documented here. The file is maintained by hand, following
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0] - 2026-11-07

### Added

- Schema scanner for Oracle (`ALL_TAB_COLUMNS`, `ALL_TABLES`, comments, `SAMPLE (p)`) and PostgreSQL
  (`information_schema`, `pg_class`, comments, `TABLESAMPLE BERNOULLI`) behind one `IRelationalDatabase` abstraction.
- 22 personal-data detectors combining Turkish + English column-name rules with validated value samples
  (TCKN and VKN check digits, IBAN mod-97, Luhn + issuer prefix incl. TROY, Turkish phone numbering, plates,
  ICD-10, address markers, name lexicons, birth-date plausibility) into an explained confidence score.
- KVKK / VERBİS classification with special-category flag and data-subject linkage (reference tables are not
  health data); scan outputs contain metadata only, never sampled values.
- Masking engine: HMAC-SHA256 keyed pseudonyms, format-preserving TCKN / VKN / IBAN / card / phone / plate that
  still pass validation, partial masking, generalisation, synthetic names and addresses, nulling; applied as a
  copy to a separate schema with least-privilege accounts.
- Synthetic data generator (Bogus `tr`) for a labelled clinic + HR schema with valid checksums and decoys.
- VERBİS-style inventory export (JSON, CSV, Markdown) from classification × policy file, with gap reporting.
- `kvkk` CLI (`synth`, `scan`, `classify`, `mask`, `export`, `evaluate`, `demo`, `pipeline`) and a minimal API
  (`/api/v1/detect`, `/scans`, `/inventory`) with health checks, Serilog JSON and OpenTelemetry.
- `docker compose up` demo on PostgreSQL 17 and Oracle 23ai Free.
- Measured results: precision / recall / F1 0.939 on the labelled schema on both engines; full scan of 294 tables /
  2 079 columns in 0.94 s (Oracle) and 1.27 s (PostgreSQL); masked copy at 10 614 and 7 590 rows/s.

### Fixed

- Mask PostgreSQL `date` values that Npgsql returns as `DateOnly` (found by the cross-engine test).
- Keep the first-name or surname role of single-word names when synthesising.

[Unreleased]: https://github.com/hekimcan206/kvkk-data-inventory/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/hekimcan206/kvkk-data-inventory/releases/tag/v1.0.0
