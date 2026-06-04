# Contributing

## Repository Layout

- `src/Meridian.Analyzer/`: analyzer project, rule implementations, helper code, and Roslyn release tracking files
- `tests/Meridian.Analyzer.Tests/`: analyzer behavior tests
- `docs/`: maintainer guidance, usage notes, and rule docs
- root files: release workflow, package metadata, versioning, and contributor-facing docs

## Scope

This repository is public.

- Standalone package work should be reproducible from this repo with `dotnet test` and `dotnet pack`.
- Consumer-specific rollout or severity policy belongs in the consuming project.

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

## Rule Changes

When you add or materially change a rule:

- update tests in `tests/Meridian.Analyzer.Tests/`
- update the matching rule doc under `docs/rules/`
- update the rule table in `README.md`

## Releases

- Use Conventional Commits.
- `fix:` drives patch bumps.
- `feat:` drives minor bumps.
- `feat!:` or `fix!:` drives major bumps.
- `release-please` owns `CHANGELOG.md` and `version.txt`.

The release workflow packs the analyzer and publishes it to `nuget.org` after the release PR merges.
