# Benchmarks

All numbers below were measured on the machine described under *Environment* by
`benchmarks/KvkkInventory.Benchmarks`, which starts its own PostgreSQL 17 and Oracle 23ai Free containers
through Testcontainers, seeds the synthetic schemas and writes [`benchmarks/results/latest.md`](../benchmarks/results/latest.md).
Nothing here is estimated or rounded up. All data is synthetic.

## Method

- **Accuracy.** The labelled "clinic + HR" schema (`ClinicSchema`, 14 tables, 99 columns, 300 rows each) is
  loaded on each engine. Every column carries the label a human reviewer would give it (49 positive labels on 48 columns
  over 22 detector types; 51 columns with no personal data, including deliberate decoys). The schema is scanned
  through the **read-only scanner account** with the default 200-row sample, classified at threshold 0.5, and
  compared column by column (multi-label) with the ground truth. Micro averages weight every column decision
  equally.
- **Ablation.** Same scan with name evidence only, value evidence only, and without the data-subject linkage
  rule (ADR 0001, ADR 0003).
- **Scan time.** A wide schema: the labelled schema replicated 20 times (280 tables) plus the 14 originals →
  294 tables / 2 079 columns, 500 rows per replicated table, statistics gathered. Full scan (catalog + one
  sample query per table + all detectors), parallelism 4, sample sizes 100 / 200 / 1 000. One warm-up run, then
  3 runs; median reported.
- **Sampling cost.** One 200-row sample query on a 300-row table vs a 200 000-row table (statistics gathered,
  so the large table uses `TABLESAMPLE BERNOULLI` / `SAMPLE`). Two warm-ups, 3 runs, median.
- **Masking throughput.** Full copy-to-masked-schema of everything above (295 tables, 344 200 rows) with the
  default plan, as the masker account. 3 runs, median.

## Environment

| | |
|---|---|
| CPU | Intel64 Family 6 Model 186 (13th-gen Core, mobile), 16 logical cores |
| RAM | 15.7 GB visible to .NET; Docker Desktop VM 7.6 GB |
| OS | Windows 11 (10.0.26200), Docker Desktop 29.9.1 (WSL 2) |
| .NET | 10.0.12, Release build |
| Databases | `postgres:17-alpine`, `gvenzl/oracle-free:23-slim-faststart`, default settings, same host |
| Host load | 6 other containers running during the run (unrelated projects) |

Laptop numbers on a shared Docker VM: treat them as relative, not as server capacity.

## Results

### Accuracy (both engines identical)

Per-detector table (PostgreSQL and Oracle produced exactly the same classification; the integration suite
asserts that both equal the in-memory reference):

| Detector | Support | TP | FP | FN | Precision | Recall | F1 |
|---|---:|---:|---:|---:|---:|---:|---:|
| NationalId (TCKN) | 2 | 2 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| TaxId (VKN) | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| PersonName | 6 | 6 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| DateOfBirth | 2 | 2 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| Gender | 2 | 2 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| Email | 3 | 3 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| Phone | 5 | 5 | 1 | 0 | 0.833 | 1.000 | 0.909 |
| Address | 2 | 2 | 1 | 0 | 0.667 | 1.000 | 0.800 |
| Iban | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| PaymentCard | 2 | 2 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| Salary | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| IpAddress | 2 | 2 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| VehiclePlate | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| Health | 9 | 7 | 1 | 2 | 0.875 | 0.778 | 0.824 |
| Biometric | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| Genetic | 2 | 1 | 0 | 1 | 1.000 | 0.500 | 0.667 |
| ReligionBelief | 2 | 2 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| PoliticalOpinion | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| EthnicOrigin | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| UnionMembership | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| AssociationMembership | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| CriminalRecord | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| **micro avg** | 49 | 46 | 3 | 3 | **0.939** | **0.939** | **0.939** |
| macro F1 | | | | | | | 0.964 |

The six errors, all by design of the labelled schema and pinned by a unit test:

| Error | Column | Why |
|---|---|---|
| FN Health | `randevular.bolum_adi` | Department of a patient's appointment (e.g. Psychiatry) is health data by context; no column-level signal |
| FN Health | `receteler.ilac_adi` | A patient's medication; drug names are not in the clinical-term lexicon |
| FN Genetic | `genetik_testler.sonuc` | Genetic result under the neutral name "sonuç" (result) |
| FP Health | `randevular.oda_no` | Room numbers like "B12" have the shape of ICD-10 codes, in a patient-linked table |
| FP Phone | `subeler.telefon` | The organisation's own branch phone: contact data, not personal data |
| FP Address | `subeler.adres` | Branch address, same reason |

Small support per detector (1–9 columns) means single errors move per-detector numbers a lot; the micro
average over 49 labels is the headline. This is a synthetic benchmark built by the author: real schemas will
be messier (legacy abbreviations, mixed encodings, JSON columns).

### Ablation

| Engine | Mode | Precision | Recall | F1 |
|---|---|---:|---:|---:|
| PostgreSQL 17 | names + values + linkage (default) | 0.939 | 0.939 | 0.939 |
| PostgreSQL 17 | names + values, no linkage rule | 0.902 | 0.939 | 0.920 |
| PostgreSQL 17 | column names only | 0.905 | 0.776 | 0.835 |
| PostgreSQL 17 | sampled values only | 0.919 | 0.694 | 0.791 |
| Oracle 23ai Free | names + values + linkage (default) | 0.939 | 0.939 | 0.939 |
| Oracle 23ai Free | names + values, no linkage rule | 0.902 | 0.939 | 0.920 |
| Oracle 23ai Free | column names only | 0.905 | 0.776 | 0.835 |
| Oracle 23ai Free | sampled values only | 0.917 | 0.673 | 0.776 |

Combining both evidence sources gains +0.10 F1 over names alone and +0.15 over values alone; the linkage rule
removes the ICD-10 catalog false positives (+0.04 precision). In *values-only* mode one column classifies
differently on the two engines (33 vs 34 true positives). Neither engine was given an `ORDER BY`, so the
"first 200 rows" of a small table may differ; the most likely cause is a borderline column whose score crosses
0.5 on one sample and not the other. It has not been investigated further; the default mode is identical on
both engines and that is asserted by the integration tests.

### Scan time (294 tables, 2 079 columns)

| Engine | Sample rows/table | Median full scan | Columns/s |
|---|---:|---:|---:|
| PostgreSQL 17 | 100 | 614 ms | 3 386 |
| PostgreSQL 17 | 200 (default) | 1 271 ms | 1 636 |
| PostgreSQL 17 | 1 000 | 2 431 ms | 855 |
| Oracle 23ai Free | 100 | 635 ms | 3 275 |
| Oracle 23ai Free | 200 (default) | 939 ms | 2 215 |
| Oracle 23ai Free | 1 000 | 1 986 ms | 1 047 |

### Sampling cost vs table size

| Engine | Table rows | Median 200-row sample query |
|---|---:|---:|
| PostgreSQL 17 | 300 | 6.7 ms |
| PostgreSQL 17 | 200 000 | 4.8 ms |
| Oracle 23ai Free | 300 | 2.3 ms |
| Oracle 23ai Free | 200 000 | 3.5 ms |

The sample cost does not grow with the table (≈ 670× more rows, same milliseconds), which is the point of
using the engines' sampling clauses instead of reading the table.

### Masking throughput (295 tables, 344 200 rows)

| Engine | Median duration | Rows/s |
|---|---:|---:|
| PostgreSQL 17 (multi-row INSERT, 500/batch) | 45.4 s | 7 590 |
| Oracle 23ai Free (array binding, 5 000/round trip) | 32.4 s | 10 614 |

Throughput includes reading, masking and writing every row of every table, one table at a time. PostgreSQL's
`COPY BINARY` and table-level parallelism are the obvious next steps (see README).

## Reproduce

```bash
dotnet run -c Release --project benchmarks/KvkkInventory.Benchmarks
# options: -- --copies 20 --rows 500 --tall 200000 --runs 3
```

Requires Docker. Takes about 7 minutes on the machine above, most of it loading data.
