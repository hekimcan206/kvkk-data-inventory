# Contributing

## Prerequisites

- .NET SDK 10.0.400 or later (`global.json` rolls forward to the latest feature band)
- Docker (integration tests start `postgres:17-alpine` and `gvenzl/oracle-free:23-slim-faststart` through
  Testcontainers; `docker compose up` runs the demo)

## Workflow

1. Open an issue first for anything larger than a small fix.
2. Branch from `main`; one concern per pull request.
3. Before pushing:

   ```bash
   dotnet build                                   # warnings are errors
   dotnet test                                    # unit + property + API + Oracle/PostgreSQL integration tests
   dotnet format --verify-no-changes
   ```

## Adding or changing a detector

- A detector is one `Recognizer` subclass in `src/KvkkInventory.Core/Detection` (name rules + a validator) and a
  line in `DetectionEngine.DefaultRecognizers`; map its type in `KvkkTaxonomy` and give it a default in
  `MaskingDefaults` (ADR 0001, ADR 0002).
- Add labelled columns for it — positives *and* decoys — to `src/KvkkInventory.Synthetic/ClinicSchema.cs`.
  `InMemoryAccuracyTests` pins the expected residual errors: update them deliberately, never to make a test pass.
- Re-run the benchmark (`dotnet run -c Release --project benchmarks/KvkkInventory.Benchmarks`) and update
  `docs/benchmarks.md` and the README results table with the new numbers.
- Regenerate the samples: `dotnet run --project src/KvkkInventory.Cli -- demo --out docs/samples`.

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/) keep the history readable and make the hand-written
CHANGELOG easy to assemble at release time
(`feat:`, `fix:`, `perf:` are user-visible; `docs:`, `test:`, `build:`, `ci:`, `refactor:`, `chore:` are not).

## Architecture decisions

A change to a decision in `docs/adr/` needs a new ADR that supersedes the old one (MADR format).
