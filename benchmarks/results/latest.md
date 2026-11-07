# Benchmark results (generated)

- Date (UTC): 2026-11-07 00:51
- CPU: Intel64 Family 6 Model 186 Stepping 2, GenuineIntel, 16 logical cores
- RAM visible to .NET: 15.7 GB
- OS: Microsoft Windows 10.0.26200; .NET 10.0.12
- Docker: 29.9.1; other running containers on the host: 6
- Parameters: labelled schema 14 tables × 300 rows; wide schema 20 copies × 14 tables × 500 rows; tall table 200,000 rows; 3 runs per measurement, median reported

## Accuracy on the labelled schema

### PostgreSQL 17

| Detector | Support | TP | FP | FN | Precision | Recall | F1 |
|---|---:|---:|---:|---:|---:|---:|---:|
| NationalId | 2 | 2 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| TaxId | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
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

Errors:

```
FN Health                 randevular.bolum_adi
FP Health                 randevular.oda_no
FN Health                 receteler.ilac_adi
FP Phone                  subeler.telefon
FP Address                subeler.adres
FN Genetic                genetik_testler.sonuc
```

### Oracle 23ai Free

| Detector | Support | TP | FP | FN | Precision | Recall | F1 |
|---|---:|---:|---:|---:|---:|---:|---:|
| NationalId | 2 | 2 | 0 | 0 | 1.000 | 1.000 | 1.000 |
| TaxId | 1 | 1 | 0 | 0 | 1.000 | 1.000 | 1.000 |
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

Errors:

```
FN Health                 randevular.bolum_adi
FP Health                 randevular.oda_no
FN Health                 receteler.ilac_adi
FP Phone                  subeler.telefon
FP Address                subeler.adres
FN Genetic                genetik_testler.sonuc
```


## Ablation (micro-averaged over all detectors)

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

## Scan time

| Engine | Tables | Columns | Sample rows/table | Median scan (ms) | Columns/s |
|---|---:|---:|---:|---:|---:|
| PostgreSQL 17 | 294 | 2079 | 100 | 614 | 3386 |
| PostgreSQL 17 | 294 | 2079 | 200 | 1271 | 1636 |
| PostgreSQL 17 | 294 | 2079 | 1000 | 2431 | 855 |
| Oracle 23ai Free | 294 | 2079 | 100 | 635 | 3275 |
| Oracle 23ai Free | 294 | 2079 | 200 | 939 | 2215 |
| Oracle 23ai Free | 294 | 2079 | 1000 | 1986 | 1047 |

## Sampling cost vs table size (one table, 200-row sample)

| Engine | Table rows | Median sample query (ms) |
|---|---:|---:|
| PostgreSQL 17 | 300 | 6.7 |
| PostgreSQL 17 | 200,000 | 4.8 |
| Oracle 23ai Free | 300 | 2.3 |
| Oracle 23ai Free | 200,000 | 3.5 |

## Masking throughput (copy to masked schema)

| Engine | Tables | Rows | Median duration (ms) | Rows/s |
|---|---:|---:|---:|---:|
| PostgreSQL 17 | 295 | 344,200 | 45352 | 7,590 |
| Oracle 23ai Free | 295 | 344,200 | 32429 | 10,614 |
