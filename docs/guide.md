# Guide

## Stable Pieces

These pieces should stay stable:

- Analyzer IDs stay under the `MER` namespace.
- Rule IDs should remain stable once published.
- Source lives under `src/Meridian.Analyzer/`.
- Tests live under `tests/Meridian.Analyzer.Tests/`.
- Docs live under `docs/`.
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

If another analyzer already covers the same pattern, narrow the rule instead of duplicating the diagnostic.

For `MER0002`, keep broader catch quality with Sonar/SDK analyzers and only own the specific nested broad-catch fallback shape.

## Pilot Promotion Criteria

Do not widen a pilot rule just to widen it.

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
- Keep the rule docs focused on stable behavior. Leave historical rollout notes out unless consumers need them.
