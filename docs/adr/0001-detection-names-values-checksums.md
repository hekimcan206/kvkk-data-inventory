# ADR 0001: Detect personal data from column names and validated value samples, combined into a confidence

- Status: accepted
- Date: 2026-11-07

## Context

The tool must find personal data (*kişisel veri*, KVKK Art. 3) in Oracle and PostgreSQL schemas it has never
seen, and classify it into VERBİS data categories. Real schemas make this hard in both directions:

- Names lie. `ilac_adi` (drug name) and `bolum_adi` (department name) end in *adı* like `hasta_adi`;
  `email_verified` contains "email"; `kayit_no` holds 11-digit numbers that are not TCKNs.
- Values lie. Any `DATE` column looks like a date of birth; any 10-digit number could be a phone number or a
  VKN; a hire date spans decades like a birth date.
- Some data is visible only in names (a `sendika_uyeligi` column holding "Yok"), some only in values (a
  `notlar` free-text column with phone numbers in it), and binary biometric templates have no readable values.

Prior art:

- **Microsoft Presidio** (analyzer) runs a registry of recognizers over text. Pattern recognizers start from a
  base score (a bare five-digit pattern gets 0.01), optional validation (checksums such as Luhn for cards or
  mod-97 for IBAN) confirms or rejects a match, and a context enhancer raises the score when context words
  appear near the match. Presidio explicitly accepts request-level context such as column names. Results
  carry a confidence and are filtered by a threshold.
- Commercial data-discovery tools (Amazon Macie, Google Cloud DLP / Sensitive Data Protection, Microsoft
  Purview) combine "infoType" detectors (regex + checksum + dictionaries) with likelihood levels, and scan
  databases by **sampling** rather than full reads.
- Column-name-only scanners are common in open source; they are cheap but blind to data hidden under neutral
  names, and fooled by the decoys above.

## Options considered

1. **Column-name heuristics only.** Fast, no data access needed. Measured on the labelled schema: lower
   precision *and* recall (see the ablation in `docs/benchmarks.md`).
2. **Value patterns only (regex).** Finds data under neutral names, but "11 digits" or "a date" is weak evidence;
   cannot see name-only categories (union membership, religion when coded).
3. **ML / NER model on values** (spaCy, transformer). Good for free text in English; Turkish models are scarce,
   heavy to ship, hard to explain to an auditor, and still need the checksum layer for identifiers.
4. **Per-type recognizers that combine name evidence and validated value evidence into one confidence**
   (Presidio's idea applied to columns).

## Decision

Option 4. Each PII type has a `Recognizer` with:

- **Name rules** in Turkish and English, matched on folded tokens (`DOĞUM_TARİHİ`, `dogumTarihi`,
  `DOGUMTARIHI` all reach the same rule; Turkish possessive/plural suffixes are accepted, so `adres` matches
  `adresi`). Column comments count as context at 80 % weight. Tokens that point to a non-person entity
  (`ilac`, `bolum`, `sube`, `kullanici`) or a derived value (`hash`, `verified`, `count`, `tipi`) cut name
  evidence to 30 %.
- **Value validation**, not just patterns: TCKN check digits, VKN check digit, IBAN mod-97, Luhn + issuer
  prefix (incl. TROY 9792), Turkish phone numbering plan, plate format (province 01–81, letter/digit
  combinations), ICD-10 shape, address markers + province names, a curated first-name/surname lexicon,
  plausible-and-spread birth dates, both sexes present for gender codes.
- **Column-level value score** = strength × share of samples that validate (saturating at 80 % for structured
  columns, at 25 % for free text where embedded matches count).
- **Noisy-OR combination**: `confidence = 1 − (1 − value) × (1 − 0.6 × name)`. Name-only types use
  `0.9 × name`. A strong name alone (0.9 → 0.54) or strong values alone (TCKN 0.97) can each cross the 0.5
  threshold; together they reinforce.
- **Contradiction**: when a value-checkable type has ≥ 10 samples and ≤ 2 % validate, the name evidence is
  multiplied by 0.35. A column named `tc_kimlik_no` full of invalid numbers is not reported as TCKN.

Weights are hand-set and documented in code, not learned: they must be explainable to a DPO, and every
`Evidence` carries a human-readable reason (`name=0.95 ('tckn'); values=200/200 → 0.97`) — never the values.

## Consequences

- Precision/recall are measured, per detector, on a labelled synthetic schema with deliberate decoys, on both
  engines (`docs/benchmarks.md`). The residual errors are kept visible and pinned by a test, not tuned away:
  contextual health data (`bolum_adi` = Psychiatry, `ilac_adi`) and neutral-named genetic results are missed;
  an `oda_no` value like "B12" looks like ICD-10; business phone/address of the organisation's own branches
  are reported (they are contact data, but not personal data).
- Adding a type is one class with rules + a validator; the engine and the evaluation need no change.
- The lexicons are small and deliberately independent of the synthetic generator, so name recall reflects
  coverage honestly (the generator mixes common names with Bogus' rare ones).
- No ML dependency: the detector runs anywhere .NET runs, in milliseconds per column.
