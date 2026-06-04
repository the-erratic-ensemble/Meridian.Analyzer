# Development

## First-Time Setup

1. Install the .NET 10 SDK pinned in `global.json`.
2. Restore dependencies:

```bash
dotnet restore Meridian.Analyzers.slnx
```

## Normal Edit Loop

- Change analyzer code, tests, or docs.
- Run the local checks that matter:

```bash
dotnet test tests/Meridian.Analyzers.Tests/Meridian.Analyzers.Tests.csproj -c Release
dotnet pack Meridian.Analyzers.csproj -c Release -o artifacts
```

- If you changed rule behavior, also run the Meridian consumer-side analyzer wrapper from the Meridian repo before you cut a release.
- Commit with Conventional Commits. `release-please` uses commit prefixes for version bumps:
  - `fix:` -> patch
  - `feat:` -> minor
  - `feat!:` or `fix!:` -> major

## Meridian Local Development Contract

Meridian is expected to consume this repo through a sibling project reference at:

```text
/home/matthias/projects/Meridian.Analyzers
```

That keeps local analyzer development direct while package publishing stays a release concern.

## Release Flow

`main` is the release branch.

When you push conventional commits to `main`, `.github/workflows/release-please.yml` does two jobs:

1. Opens or updates a Release Please PR with the next version and changelog changes.
2. After that release PR is merged, packs `Meridian.Analyzers` and publishes it to `nuget.org`.

`nuget.org` is public. If you later need a private package flow, use a different feed.

The release workflow expects one GitHub repository secret:

- `NUGET_KEY`: nuget.org push key with `Push` scope for this package ID.

## Installing The Published Package

```bash
dotnet add package Meridian.Analyzers
```
