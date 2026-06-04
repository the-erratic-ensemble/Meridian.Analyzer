# Development

## First-Time Setup

1. Install the .NET 10 SDK pinned in `global.json`.
2. Restore dependencies:

```bash
dotnet restore Meridian.Analyzer.slnx
```

## Normal Edit Loop

- Change analyzer code, tests, or docs.
- Run the local checks that matter:

```bash
dotnet test tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj -c Release
dotnet pack src/Meridian.Analyzer/Meridian.Analyzer.csproj -c Release -o artifacts
```

- If you changed rule behavior, also run the Meridian consumer-side analyzer wrapper before you cut a release when Meridian rollout behavior is part of the change.
- Commit with Conventional Commits. `release-please` uses commit prefixes for version bumps:
  - `fix:` -> patch
  - `feat:` -> minor
  - `feat!:` or `fix!:` -> major

## Meridian Local Development Contract

Meridian is expected to consume this repo through a sibling project reference at:

```text
/home/matthias/projects/Meridian.Analyzer
```

That keeps local analyzer development direct while package publishing stays a release concern.

## Release Flow

`main` is the release branch.

When you push conventional commits to `main`, `.github/workflows/release-please.yml` does two jobs:

1. Opens or updates a Release Please PR with the next version and changelog changes.
2. After that release PR is merged, packs `Meridian.Analyzer` and publishes it to `nuget.org`.

`nuget.org` is public. If you later need a private package flow, use a different feed.

The release workflow expects one GitHub repository secret:

- `NUGET_KEY`: nuget.org push key with `Push` scope for this package ID.

## Installing The Published Package

```bash
dotnet add package Meridian.Analyzer
```

External consumers can stop there.

The Meridian-specific wrapper commands in this repo exist for the internal rollout contract that originally produced these analyzers. They are not required to consume the published NuGet package.

## Current Risks

- `nuget.org` publication is public by default.
- Meridian rollout behavior still depends on the Meridian repo’s analyzer wrapper and `.editorconfig`.
- If you rename the package ID or GitHub repo again later, update `README.md`, `version.txt`, the workflow files, and the Meridian sibling-project reference in one change.
