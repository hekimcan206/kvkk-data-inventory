# Security policy

## Supported versions

Only the latest release receives fixes.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub's private vulnerability reporting
(**Security → Report a vulnerability**). Expect an acknowledgement within 5 working days and a fix or
mitigation plan within 30 days of confirmation.

## Data

This project contains **synthetic data only**. Never submit real personal data — TCKNs, names, health
records, IBANs, scan outputs of real databases — in issues, pull requests, test fixtures or sample files.
See `docs/threat-model.md`.

## Security-relevant defaults

| Area | Default |
|---|---|
| Database accounts | Owner (seeding only), read-only scanner (SELECT via role, sees only granted tables), masker (reads source, writes only its own schema). Created by `deploy/*/init`, the same scripts the tests use |
| Scan output | Metadata, scores, counts and reasons only — never sampled values; `/detect` never echoes values; request bodies are not logged |
| Masking | HMAC-SHA256, ≥ 256-bit key from an environment variable (never CLI args or files), one-way pseudonyms; copy to a separate schema, source never updated; special categories nulled by default |
| SQL | Values are always parameters; identifiers come from the catalog and are always quoted with embedded quotes doubled |
| API | Scans only server-configured databases; 2 MB request cap; max 1 000 samples per `/detect`; RFC 9457 problem details; no authentication in the demo — deploy behind a gateway |
| Container | Chiseled runtime image, non-root UID 1654, no shell |
| Supply chain | Central package versions; vulnerable transitive `System.Security.Cryptography.Pkcs` pinned to a patched version; updated by hand; CI fails on known-vulnerable direct or transitive packages (`dotnet list package --vulnerable --include-transitive`); CodeQL `security-and-quality` |
| Secrets | None in the repository; compose defaults and the demo masking key are development-only and overridable via `.env` |
