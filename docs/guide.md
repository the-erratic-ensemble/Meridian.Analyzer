# Guide

## Package Contracts

Keep these contracts stable unless you are executing an intentional analyzer review:

- Analyzer IDs stay under the `MER` namespace.
- `MER0001` is the current pilot contract for direct initializer conditionals that branch into payload construction.
- `MER0002` is the current wrapper-default contract for broad nested try/catch fallback flow inside another try block.
- `MER0003`, `MER0004`, `MER0006`, `MER0008`, and `MER0013` are the first staged contracts from the 2026-05-07 backend analyzer candidate audit. `MER0003` and `MER0004` are currently warning-enabled in committed `.editorconfig`; `MER0008` and `MER0013` stay inventory-driven.
- `MER0005`, `MER0007`, and `MER0009` through `MER0025` are remaining-candidate contracts from the same audit. `MER0005`, `MER0006`, `MER0012`, and `MER0021` are currently warning-enabled in committed `.editorconfig`; most of the rest remain inventory-driven because they depend on path/naming heuristics or current-code queue classification.
- `Meridian.Analyzers.csproj` is the canonical implementation surface.
- `tests/Meridian.Analyzers.Tests/` is the canonical behavior test surface.
- `apps/backend/.editorconfig` in the Meridian repo is the operator severity surface.
- `docs` is the canonical documentation surface.
- `version.txt` is the standalone release version surface and is managed by `release-please`.

## Adding A Rule

1. Decide whether the rule belongs in Meridian-owned backend analyzers.
2. Pick the next stable `MERxxxx` ID.
3. Add the analyzer implementation and keep the project dependency-light.
4. Add positive and negative tests.
5. Add or update rollout entries in `apps/backend/.editorconfig`.
6. Update `README.md`, add or update the rule file under `docs/rules/`, and revise `docs/usage-example.md` or this guide if the workflow changed.

### Minimum Maintenance Contract For `MER0002+`

When adding the second or later rule, treat these updates as mandatory in the same patch:

- implementation file at the repo root beside `Meridian.Analyzers.csproj`
- behavior tests in `tests/Meridian.Analyzers.Tests/`
- severity/rollout entry in `apps/backend/.editorconfig`
- rule doc in `docs/rules/`
- `README.md` rule table + rollout state
- one replayable dated `docs/analysis/` artifact if the rule launches with an inventory queue or pilot scope

## Overlap Review

Before shipping a new rule, check for overlap with:

- Sonar
- Roslynator
- StyleCop
- SDK / NetAnalyzers

If another analyzer already owns the same refactor pressure, narrow the Meridian rule instead of normalizing duplicate reporting.

For `MER0002`, keep broader catch quality with Sonar/SDK analyzers and only own the specific nested broad-catch fallback shape.

For the 2026-05-07 staged candidates:

- `MER0003` owns only Meridian endpoint metadata combinations where output-cache can skip tenant, entitlement, quota, plan, or policy behavior.
- `MER0004` owns only policy presence and obvious `[AllowAnonymous]` drift; exact policy-to-operation correctness remains with controller policy tests.
- `MER0005` owns admin class/route/base-controller shape only; exact admin policy correctness remains with controller policy tests.
- `MER0006` owns only controller action service location in this phase; runtime services, middleware, filters, handlers, and composition roots need later allowlists before enforcement.
- `MER0007` owns raw source reads of environment/configuration outside option/startup/provider boundaries; config-file conflict detection stays outside Roslyn.
- `MER0008` owns literal `MERIDIAN_SKIP_*` reads outside startup guard boundaries; runtime safety still needs startup tests.
- `MER0009` owns token exposure and `CancellationToken.None` in controller actions; async overload forwarding needs inventory classification before enforcement.
- `MER0010` owns direct system time, raw `Task.Delay`, and raw timers outside approved time/profile boundaries; passive model/entity defaults are the only broad model-path exemption.
- `MER0011` owns static mutable state in controllers and auth/session handlers; bounded cache/lifecycle policy belongs in injectable services.
- `MER0012` owns API-local and non-public library `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck` registration parity; public library checks may be registered by the host project.
- `MER0013` owns source-level using/type boundary drift; project-reference and package-graph rules stay in MSBuild validation or architecture tests.
- `MER0014` owns obvious DTO/entity placement inventory; nuanced cross-feature contracts need review.
- `MER0015` owns in-memory string helper usage inventory and intentionally excludes likely query-expression lambdas.
- `MER0016` owns ad hoc JSON option construction outside `MeridianJsonProfiles` or named factory/profile classes.
- `MER0017` owns async materialisation sites without visible inline or locally composed `Where`, `Take`, or `Skip`; deliberate maintenance flows need classification.
- `MER0018` owns raw SQL API use and interpolated SQL outside persistence boundaries; dynamic identifier allowlists belong in targeted tests.
- `MER0019` owns direct `new ProblemDetails` inside controller actions only.
- `MER0020` owns direct repository/DbContext/EF calls inside controller actions as inventory, using repository and DbContext receiver/type signals rather than generic `context` names.
- `MER0021` owns `ILogger<T>` and `Console` source drift outside framework-edge boundaries.
- `MER0022` owns semantic `StackExchange.Redis.IServer.Keys(...)` Redis keyspace scans outside a named keyspace scanner/helper.
- `MER0023` owns `Task.Run`, fire-and-forget async discards, and broad `CancellationToken.None` outside owned runtime boundaries.
- `MER0024` owns `StringExtensions.IsNullOr*` usage inside queryable/expression predicate boundaries.
- `MER0025` owns empty property-pattern brace inventory such as `is { }`, `is not { }`, and tuple elements containing `{ }`, outside tests/analyzer internals.

For inventory-only rules with a non-trivial candidate set, save one dated analysis artifact for the replayed queue and link it from the rule doc before any warning promotion work.

In Meridian, `apps/backend/Directory.Build.props` currently keeps analyzers off for normal builds (`RunAnalyzersDuringBuild=false`) while leaving live analysis on. Treat warning-enabled Meridian rules as active in editor/live-analysis surfaces and in explicit analyzer-enabled lanes such as `backend:analyzers:validate`, `backend:analyzers:inventory`, and `backend:analyzers:validate:build`.

## Pilot Promotion Criteria

Do not widen a pilot rule just because it exists.

Promote only when:

- the message points toward one clear refactor shape
- the tests cover intended edge cases
- the validation wrapper is deterministic
- the docs explain rollout and suppression clearly
- the pilot did not show noisy false positives

## Validation Commands

Run these before handoff:

```bash
dotnet restore Meridian.Analyzers.slnx
dotnet test tests/Meridian.Analyzers.Tests/Meridian.Analyzers.Tests.csproj -c Release
dotnet pack Meridian.Analyzers.csproj -c Release -o artifacts
```

From the Meridian repo, run the consumer-side checks that match the rollout work:

```bash
rtk pnpm backend:analyzers:validate
rtk pnpm backend:analyzers:inventory -- --report-dir output/analyzers/backend-custom-analyzers-maintainer-smoke
rtk pnpm backend:analyzers:validate:build -- --project apps/backend/Meridian.Shared/Meridian.Shared.csproj
```

Run `backend:analyzers:validate:build` against a consumer project. A direct `Meridian.Analyzers.csproj` build is compile validation only because the analyzer project is excluded from consuming itself.

To run only Meridian custom analyzer diagnostics, pass `--diagnostics` explicitly (defaults are `MER0001,MER0002`):

```bash
rtk pnpm backend:analyzers:inventory -- --diagnostics MER0002
```

For staged candidate scans, scope by rule and project:

```bash
rtk pnpm backend:analyzers:inventory -- --diagnostics MER0003,MER0004,MER0006,MER0008 --project apps/backend/Meridian.API/Meridian.API.csproj
rtk pnpm backend:analyzers:inventory -- --diagnostics MER0013 --project apps/backend/Meridian.Analytics/Meridian.Analytics.csproj
rtk pnpm backend:analyzers:inventory -- --diagnostics MER0005,MER0007,MER0009,MER0010,MER0011,MER0012,MER0019,MER0021,MER0022,MER0023,MER0024,MER0025 --project apps/backend/Meridian.API/Meridian.API.csproj
rtk pnpm backend:analyzers:inventory -- --diagnostics MER0014,MER0015,MER0016,MER0017,MER0018,MER0020 --project apps/backend/Meridian.Infrastructure/Meridian.Infrastructure.csproj
```

## Rule Index Workflow

- Keep the table in `README.md` current.
- Give each rule its own file under `docs/rules/`.
- When a rule stays inventory-only because of a candidate queue, keep the stable contract in the rule doc and move queue classification into one dated `docs/analysis/` artifact.
- If the rule inventory grows enough to justify generation, document that generator command here before relying on it.
