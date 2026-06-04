# Contributing

## Repository Layout

- `src/Meridian.Analyzer/`: analyzer project, rule implementations, helper code, and Roslyn release tracking files
- `tests/Meridian.Analyzer.Tests/`: analyzer behavior tests
- `docs/`: maintainer guidance, usage notes, and rule docs
- root files: release workflow, package metadata, versioning, and contributor-facing docs

## Local Edit Loop

1. Restore:

```bash
dotnet restore Meridian.Analyzer.slnx
```

2. Run the standalone checks:

```bash
dotnet test tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj -c Release
dotnet pack src/Meridian.Analyzer/Meridian.Analyzer.csproj -c Release -o artifacts
```

3. If rule behavior changed, also run the Meridian consumer-side check from the Meridian repo:

```bash
rtk pnpm backend:analyzers:validate:build -- --project apps/backend/Meridian.Shared/Meridian.Shared.csproj --diagnostics MER0001
```

## Rule Changes

When you add or materially change a rule:

- update tests in `tests/Meridian.Analyzer.Tests/`
- update the matching rule doc under `docs/rules/`
- update the rule table and rollout notes in `README.md`
- update the committed rollout surface in the Meridian repo when the operational contract changed

## Releases

- Use Conventional Commits.
- `fix:` drives patch bumps.
- `feat:` drives minor bumps.
- `feat!:` or `fix!:` drives major bumps.
- `release-please` owns `CHANGELOG.md` and `version.txt`.

The release workflow packs the analyzer and publishes it to `nuget.org` after the release PR merges.
