# Guide

## Package Contracts

Keep these contracts stable unless you are executing an intentional analyzer review:

- Analyzer IDs stay under the `MER` namespace.
- Rule IDs should remain stable once published.
- `src/Meridian.Analyzer/Meridian.Analyzer.csproj` is the canonical implementation surface.
- `tests/Meridian.Analyzer.Tests/` is the canonical behavior test surface.
- `docs` is the canonical documentation surface.
- `version.txt` is the standalone release version surface and is managed by `release-please`.

## Adding A Rule

1. Decide whether the rule belongs in this package.
2. Pick the next stable `MERxxxx` ID.
3. Add the analyzer implementation and keep the project dependency-light.
4. Add positive and negative tests.
5. Update `README.md`, add or update the rule file under `docs/rules/`, and revise `docs/usage-example.md` or this guide if the workflow changed.

### Minimum Maintenance Contract For `MER0002+`

When adding the second or later rule, treat these updates as mandatory in the same patch:

- implementation file under `src/Meridian.Analyzer/`
- behavior tests in `tests/Meridian.Analyzer.Tests/`
- rule doc in `docs/rules/`
- `README.md` rule table

## Overlap Review

Before shipping a new rule, check for overlap with:

- Sonar
- Roslynator
- StyleCop
- SDK / NetAnalyzers

If another analyzer already owns the same refactor pressure, narrow the rule instead of normalizing duplicate reporting.

For `MER0002`, keep broader catch quality with Sonar/SDK analyzers and only own the specific nested broad-catch fallback shape.

## Pilot Promotion Criteria

Do not widen a pilot rule just because it exists.

Promote only when:

- the message points toward one clear refactor shape
- the tests cover intended edge cases
- the docs explain configuration and suppression clearly
- the pilot did not show noisy false positives

## Validation Commands

Run these before handoff:

```bash
dotnet restore Meridian.Analyzer.slnx
dotnet test tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj -c Release
dotnet pack src/Meridian.Analyzer/Meridian.Analyzer.csproj -c Release -o artifacts
```

## Rule Index Workflow

- Keep the table in `README.md` current.
- Give each rule its own file under `docs/rules/`.
- Keep the stable contract in the rule doc and keep historical rollout notes out of the public package surface unless they materially help consumers.
