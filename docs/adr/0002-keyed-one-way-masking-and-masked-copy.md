# ADR 0002: Keyed one-way masking (HMAC-SHA256) into a separate schema

- Status: accepted
- Date: 2026-11-07

## Context

The masked database is for development, testing and analytics. It must:

1. not let its users recover the people behind the data;
2. stay **joinable**: the same patient TCKN in `hastalar`, `faturalar` and an export from another system must
   map to the same pseudonym, or every foreign key and test scenario breaks;
3. stay **valid**: applications validate TCKN check digits, IBAN mod-97 and Luhn on input; a masked TCKN that
   fails the checksum breaks the very screens the test copy exists to test;
4. be produced without risking production data.

Techniques considered (KVKK Board's guide on deletion, destruction and anonymisation lists masking,
generalisation, aggregation, noise, k-anonymity and others as anonymisation techniques):

- **Random substitution** — irreversible but not deterministic (breaks joins and repeatable test runs).
- **Unkeyed hashing** (SHA-256 of the TCKN) — deterministic but trivially reversible: the TCKN space is ≈ 9×10⁸
  values with valid check digits, enumerable in seconds.
- **Format-preserving encryption** (NIST SP 800-38G FF1, FF3-1) — deterministic and format-preserving, but
  *reversible* by design for whoever holds the key; FF3 had to be revised to FF3-1 after published attacks,
  and small domains (a 9-digit space) are where NIST's minimum-domain-size guidance for FPE bites.
- **Keyed one-way pseudonyms (HMAC)** — deterministic, not reversible even with the key, and the digits can be
  shaped to any format afterwards.
- **Generalisation / suppression** (birth date → year, IP → /24, address → province, nulling) — the right
  tool for quasi-identifiers and special categories; reduces linkage risk in the k-anonymity sense.

And two ways to apply them:

- **In-place `UPDATE`** of a cloned database — simple, but a mistake in the connection string masks
  production, and a failed run leaves a half-masked database.
- **Copy-to-masked-schema** — read the source, write masked rows into a separate schema.

## Decision

- Every keyed strategy derives a stream from `HMAC-SHA256(key, type ‖ 0x1F ‖ normalise(value))`:
  - *domain separation* by type: the same number as a TCKN and as a phone number gets unrelated pseudonyms;
  - *normalisation* first: `0532 111 22 33`, `+90 532 111 22 33` and `(0532) 111-2233` are one person;
  - digits are drawn by rejection sampling (no modulo bias), extended in counter mode for long values.
- **Format-preserving** pseudonyms are then shaped and re-validated: TCKN gets fresh check digits, VKN its
  check digit, IBAN keeps country + bank code and gets new mod-97 check digits, card keeps its BIN (first 6)
  and a new Luhn digit, phone keeps prefix/operator and separators, plates keep the province code.
- Defaults per type (`MaskingDefaults`, overridable per type or per column): identifiers format-preserving;
  names and addresses synthetic (keyed choice from lexicons); birth date, salary, IP generalised; card partial
  (last 4, PCI DSS style); gender kept; **special categories nulled** — a test copy rarely needs them, and
  nulling is the only option that cannot leak.
- The key is ≥ 256 bits, read from an environment variable (`KVKK_MASKING_KEY`), never from the command line,
  never logged.
- Masking runs **copy-to-masked-schema** only, as an account that can read the source and write only its own
  schema; the runner refuses `source == target`. Tables without findings are copied unchanged so the target is
  a complete database. NOT NULL is relaxed only on columns being nulled.

## Consequences

- Deterministic across tables, runs and engines — proven by integration tests (Oracle and PostgreSQL produce
  identical pseudonyms for the same patients).
- **Not anonymisation while the key exists.** A key holder cannot decrypt, but can *confirm a guess*
  (mask a candidate TCKN, compare) and can enumerate the TCKN space. Under KVKK the masked copy is therefore
  pseudonymised personal data, still in scope; destroying the key after the copy is made is what moves it
  towards anonymised data. The threat model records this.
- Format-preserving TCKNs map a ≈ 10¹⁰ input space onto 9×10⁸ outputs: collisions follow the birthday bound
  (≈ 5.6 expected per 100 000 people). The rate is measured in a test; a unique constraint on a masked TCKN
  column would need collision handling (listed under next steps).
- Generalisation is per column; there is no k-anonymity check across quasi-identifier combinations
  (birth year + gender + province). Listed as a next step.
- Synthetic names come from the folded (ASCII) lexicon, so "Yıldız" can become "Yildiz".
