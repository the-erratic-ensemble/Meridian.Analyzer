# Development

## First-Time Setup

1. Install the .NET 10 SDK pinned in `global.json`.
2. Restore dependencies:

```bash
dotnet restore Meridian.Analyzer.slnx
```

## Normal Edit Loop

- Change analyzer code, tests, or docs.
- Run the local checks:

```bash
dotnet test tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj -c Release
dotnet pack src/Meridian.Analyzer/Meridian.Analyzer.csproj -c Release -o artifacts
```

- Commit with Conventional Commits. `release-please` uses commit prefixes for version bumps:
  - `fix:` -> patch
  - `feat:` -> minor
  - `feat!:` or `fix!:` -> major

## Release Flow

1. Open a PR into protected `main` using Conventional Commits.
2. Wait for `pr-title` and `test-and-pack`, then squash-merge the PR so `main`
   remains linear.
3. Google Release Please opens a release PR with the next version, changelog,
   and manifest update. Review it, wait for its checks, and squash-merge it.
4. Release Please creates the GitHub tag/release, packs the analyzer, and
   publishes `Meridian.Analyzer` to NuGet.

The release workflow expects one GitHub repository secret:

- `NUGET_KEY`: nuget.org push key with `Push` scope for this package ID.

## Installing The Published Package

```bash
dotnet add package Meridian.Analyzer
```

Configure severities in the target project's `.editorconfig` or equivalent analyzer settings.

## Notes

- `nuget.org` publishes packages publicly.
- If the package ID or GitHub repository name changes, update `README.md`, `version.txt`, and the workflow files in one change.
