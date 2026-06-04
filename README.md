# Meridian.Analyzer

`Meridian.Analyzer` is the canonical implementation surface for Meridian-owned backend Roslyn analyzers.

## Quick Start

Install the published analyzer package into a consumer project:

```bash
dotnet add package Meridian.Analyzer
```

Or add an explicit package reference:

```xml
<ItemGroup>
  <PackageReference Include="Meridian.Analyzer" Version="0.2.*" PrivateAssets="all" />
</ItemGroup>
```

This repository is public. Some rollout notes and validation examples below are Meridian-specific because the analyzers were built for the Meridian backend and are exercised there first.

## Repository Layout

- `src/Meridian.Analyzer/`: analyzer project, rule implementations, helpers, and Roslyn release tracking files
- `tests/Meridian.Analyzer.Tests/`: analyzer behavior tests
- `docs/`: maintainer guidance, examples, and per-rule documentation
- `LICENSE`: MIT license for the repository and published package metadata

## Documentation

- Usage examples: [docs/usage-example.md](docs/usage-example.md)
- Maintainer guide: [docs/guide.md](docs/guide.md)
- Rule docs: [docs/rules/MER0001.md](docs/rules/MER0001.md)
- Rule docs: [docs/rules/MER0002.md](docs/rules/MER0002.md)
- Rule docs: [docs/rules/MER0003.md](docs/rules/MER0003.md)
- Rule docs: [docs/rules/MER0004.md](docs/rules/MER0004.md)
- Rule docs: [docs/rules/MER0005.md](docs/rules/MER0005.md)
- Rule docs: [docs/rules/MER0006.md](docs/rules/MER0006.md)
- Rule docs: [docs/rules/MER0007.md](docs/rules/MER0007.md)
- Rule docs: [docs/rules/MER0008.md](docs/rules/MER0008.md)
- Rule docs: [docs/rules/MER0009.md](docs/rules/MER0009.md)
- Rule docs: [docs/rules/MER0010.md](docs/rules/MER0010.md)
- Rule docs: [docs/rules/MER0011.md](docs/rules/MER0011.md)
- Rule docs: [docs/rules/MER0012.md](docs/rules/MER0012.md)
- Rule docs: [docs/rules/MER0013.md](docs/rules/MER0013.md)
- Rule docs: [docs/rules/MER0014.md](docs/rules/MER0014.md)
- Rule docs: [docs/rules/MER0015.md](docs/rules/MER0015.md)
- Rule docs: [docs/rules/MER0016.md](docs/rules/MER0016.md)
- Rule docs: [docs/rules/MER0017.md](docs/rules/MER0017.md)
- Rule docs: [docs/rules/MER0018.md](docs/rules/MER0018.md)
- Rule docs: [docs/rules/MER0019.md](docs/rules/MER0019.md)
- Rule docs: [docs/rules/MER0020.md](docs/rules/MER0020.md)
- Rule docs: [docs/rules/MER0021.md](docs/rules/MER0021.md)
- Rule docs: [docs/rules/MER0022.md](docs/rules/MER0022.md)
- Rule docs: [docs/rules/MER0023.md](docs/rules/MER0023.md)
- Rule docs: [docs/rules/MER0024.md](docs/rules/MER0024.md)
- Rule docs: [docs/rules/MER0025.md](docs/rules/MER0025.md)

## Current Surface

- Analyzer project: `src/Meridian.Analyzer/Meridian.Analyzer.csproj`
- Test project: `tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj`
- Diagnostic prefix: `MER`
- Category namespaces: `Meridian.Readability`, `Meridian.Security`, `Meridian.Architecture`, `Meridian.Reliability`, `Meridian.Performance`
- Packaging status: packable analyzer package published through nuget.org after the release PR lands; Meridian can consume it locally through a sibling project reference during development
- Current rollout: `MER0001` stays wrapper-scoped to `apps/backend/Meridian.API/Features/Dev/Controllers/DevController.Diagnostics.cs`; `MER0002` stays wrapper-default for explicit format and inventory lanes; `MER0003`, `MER0004`, `MER0005`, `MER0006`, `MER0012`, and `MER0021` are `warning` in `apps/backend/.editorconfig` for analyzer-scope backend projects during live analysis and explicit analyzer-enabled lanes; `MER0007`, `MER0008`, `MER0009`, `MER0010`, `MER0011`, `MER0013` through `MER0020`, and `MER0022` through `MER0025` remain inventory-driven unless explicitly scoped by wrapper input

## Validation Entry Points

- Local test run: `dotnet test tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj -c Release`
- Local package smoke check: `dotnet pack src/Meridian.Analyzer/Meridian.Analyzer.csproj -c Release -o artifacts`
- Meridian consumer validation: `rtk pnpm backend:analyzers:validate`

Use `dotnet test` and `dotnet pack` from this repo to validate the standalone package itself.
Use `backend:analyzers:validate` and `backend:analyzers:inventory` from the Meridian repo only when you need Meridian consumer-side rollout or inventory evidence.
Use `backend:analyzers:validate:build` against a consumer project to prove analyzer loading during build. Building `src/Meridian.Analyzer/Meridian.Analyzer.csproj` itself is compile validation only, because the analyzer project intentionally does not consume itself.

## Risks And Current Boundaries

- `nuget.org` is public. The current publish lane is fine for public distribution, but it is not a private feed.
- Release automation depends on the `NUGET_KEY` GitHub secret. If that key rotates or loses push scope, the release workflow will fail at publish time.
- Rule rollout still depends on the Meridian monorepo’s `.editorconfig` and analyzer wrapper scripts. This repo owns implementation and package release, not the operational rollout contract by itself.
In Meridian, normal backend builds keep analyzers disabled through `apps/backend/Directory.Build.props`; warning-enabled Meridian rules surface during live analysis and explicit analyzer-enabled lanes such as `backend:analyzers:validate`, `backend:analyzers:inventory`, or `backend:analyzers:validate:build`.
Use `--diagnostics` to scope runs to specific Meridian rules; default diagnostics are `MER0001,MER0002`.

Staged candidate inventory examples:

- `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0003,MER0004,MER0005,MER0006,MER0007,MER0008,MER0009,MER0010,MER0011,MER0012,MER0019,MER0021,MER0022,MER0023,MER0024,MER0025 --project apps/backend/Meridian.API/Meridian.API.csproj`
- `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0014,MER0015,MER0016,MER0017,MER0018,MER0020 --project apps/backend/Meridian.Infrastructure/Meridian.Infrastructure.csproj`
- `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0013 --project apps/backend/Meridian.Analytics/Meridian.Analytics.csproj`

## Current Rules

| Rule | Diagnostic ID | Category | Rollout scope | Validation lane | Preferred refactor |
| --- | --- | --- | --- | --- | --- |
| [Ternary in initializer payload branches](docs/rules/MER0001.md) | `MER0001` | Readability | `warning` only on `DevController.Diagnostics.cs` | `rtk pnpm backend:analyzers:validate` for pilot, `rtk pnpm backend:analyzers:inventory` for broader scans | Stage the payload branch in a named local or helper before building the initializer |
| [Broad nested try/catch fallback flow](docs/rules/MER0002.md) | `MER0002` | Readability | wrapper-default diagnostic for explicit format/inventory lanes; broader inventory review in `Meridian.Analytics` | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0002 --project apps/backend/Meridian.Analytics/Meridian.Analytics.csproj --include apps/backend/Meridian.Analytics/Services/ClickHouseAnalyticsService.cs` | Extract the inner fallback branch into a helper or flatten the exception-handling flow |
| [Unsafe output-cache boundary](docs/rules/MER0003.md) | `MER0003` | Security | `warning` in analyzer-scope live analysis and explicit analyzer-enabled lanes | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0003 --project apps/backend/Meridian.API/Meridian.API.csproj` | Remove `[OutputCache]` or replace it with no-store caching unless a persona-safe cache policy is reviewed |
| [Controller authorization policy boundary](docs/rules/MER0004.md) | `MER0004` | Security | `warning` in analyzer-scope live analysis and explicit analyzer-enabled lanes | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0004 --project apps/backend/Meridian.API/Meridian.API.csproj` | Declare class-level or action-level policies on admin and high-risk controllers |
| [Admin controller shape contract](docs/rules/MER0005.md) | `MER0005` | Security | `warning` in analyzer-scope live analysis and explicit analyzer-enabled lanes | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0005 --project apps/backend/Meridian.API/Meridian.API.csproj` | Align admin controllers on `Admin*Controller`, `api/admin`, and `BaseAdminController` |
| [Controller service locator boundary](docs/rules/MER0006.md) | `MER0006` | Architecture | `warning` in analyzer-scope live analysis and explicit analyzer-enabled lanes | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0006 --project apps/backend/Meridian.API/Meridian.API.csproj` | Use constructor injection or `[FromServices]` instead of action-body service location |
| [Raw configuration read boundary](docs/rules/MER0007.md) | `MER0007` | Reliability | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0007 --project apps/backend/Meridian.API/Meridian.API.csproj` | Move raw reads to typed options, startup guards, or provider adapters |
| [Startup bypass flag containment](docs/rules/MER0008.md) | `MER0008` | Security | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0008 --project apps/backend/Meridian.API/Meridian.API.csproj` | Move `MERIDIAN_SKIP_*` reads behind `StartupGuards` or typed startup-skip options |
| [Controller cancellation boundary](docs/rules/MER0009.md) | `MER0009` | Reliability | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0009 --project apps/backend/Meridian.API/Meridian.API.csproj` | Add `CancellationToken` to async actions and avoid `CancellationToken.None` in request code |
| [Clock and deterministic delay boundary](docs/rules/MER0010.md) | `MER0010` | Reliability | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0010 --project apps/backend/Meridian.API/Meridian.API.csproj` | Use `IMeridianClock` or `TimeProvider` for runtime time/delay work |
| [Static mutable runtime state](docs/rules/MER0011.md) | `MER0011` | Reliability | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0011 --project apps/backend/Meridian.API/Meridian.API.csproj` | Move static mutable state from controllers/auth handlers into injectable bounded services |
| [Health-check registration parity](docs/rules/MER0012.md) | `MER0012` | Reliability | `warning` in analyzer-scope live analysis and explicit analyzer-enabled lanes | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0012 --project apps/backend/Meridian.API/Meridian.API.csproj` | Register every source `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck` through health-check registration |
| [Backend layer-boundary guard](docs/rules/MER0013.md) | `MER0013` | Architecture | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0013 --project apps/backend/Meridian.Core/Meridian.Core.csproj` | Move dependencies to the documented Clean Architecture layer boundary |
| [Model ownership boundary](docs/rules/MER0014.md) | `MER0014` | Architecture | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0014 --project apps/backend/Meridian.Shared/Meridian.Shared.csproj` | Keep DTOs feature-local and entities in the database entity boundary |
| [String helper usage](docs/rules/MER0015.md) | `MER0015` | Readability | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0015 --project apps/backend/Meridian.API/Meridian.API.csproj` | Use shared string helpers in in-memory code |
| [Shared JSON profile boundary](docs/rules/MER0016.md) | `MER0016` | Architecture | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0016 --project apps/backend/Meridian.API/Meridian.API.csproj` | Move ad hoc JSON options into `MeridianJsonProfiles` or a named factory |
| [Unbounded EF materialisation](docs/rules/MER0017.md) | `MER0017` | Performance | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0017 --project apps/backend/Meridian.Infrastructure/Meridian.Infrastructure.csproj` | Add obvious `Where`, `Take`, or `Skip` bounds before async materialisation |
| [Raw SQL boundary](docs/rules/MER0018.md) | `MER0018` | Security | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0018 --project apps/backend/Meridian.Infrastructure/Meridian.Infrastructure.csproj` | Keep SQL in persistence boundaries and prefer interpolated APIs over raw APIs |
| [ProblemDetails construction boundary](docs/rules/MER0019.md) | `MER0019` | Reliability | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0019 --project apps/backend/Meridian.API/Meridian.API.csproj` | Use shared ProblemDetails helpers from controller actions |
| [Controller data-access boundary](docs/rules/MER0020.md) | `MER0020` | Architecture | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0020 --project apps/backend/Meridian.API/Meridian.API.csproj` | Delegate repository, DbContext, and EF work to services/facades |
| [Backend logging contract](docs/rules/MER0021.md) | `MER0021` | Reliability | `warning` in analyzer-scope live analysis and explicit analyzer-enabled lanes | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0021 --project apps/backend/Meridian.API/Meridian.API.csproj` | Use Serilog outside framework-edge boundaries |
| [Redis keyspace scan boundary](docs/rules/MER0022.md) | `MER0022` | Performance | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0022 --project apps/backend/Meridian.Infrastructure/Meridian.Infrastructure.csproj` | Route `IServer.Keys` scans through an approved bounded helper |
| [Detached runtime task boundary](docs/rules/MER0023.md) | `MER0023` | Reliability | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0023 --project apps/backend/Meridian.API/Meridian.API.csproj` | Give background work an owned lifetime, cancellation path, and observability boundary |
| [IQueryable string-extension guard boundary](docs/rules/MER0024.md) | `MER0024` | Reliability | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0024 --project apps/backend/Meridian.Infrastructure/Meridian.Infrastructure.csproj` | Replace `StringExtensions.IsNullOr*` predicates inside queryable/expression-returning methods with translatable query guards |
| [Empty is-pattern brace guard boundary](docs/rules/MER0025.md) | `MER0025` | Readability | inventory-only staged candidate | `rtk pnpm backend:analyzers:inventory -- --diagnostics MER0025 --project apps/backend/Meridian.API/Meridian.API.csproj` | Replace empty property-pattern braces with shared nullable helpers or explicit null checks when behavior matches |

## Rollout State

The current replayable `Meridian.API` inventory for the narrowed payload-branch contract surfaces `13` candidate diagnostics. The analyzer remains in a bounded pilot until those cases are reviewed and remediated deliberately.

The current replayable `MER0002` inventory surfaces `1` candidate in `Meridian.Analytics` and `0` candidates in `Meridian.API`. `MER0002` remains part of the wrapper-default diagnostic set for explicit format and inventory lanes, with broader inventory review still focused on the remaining Analytics signal.

`MER0003`, `MER0004`, `MER0006`, `MER0008`, and `MER0013` are staged from the first 2026-05-07 backend custom analyzer candidate implementation. `MER0003` and `MER0004` are now `warning` in committed `.editorconfig` for analyzer-scope live analysis and explicit analyzer-enabled lanes, while `MER0008` and `MER0013` remain inventory-driven. Their first replayable inventory artifact is [docs/analysis/2026-05/2026-05-07-backend-custom-analyzer-staged-inventory.md](docs/analysis/2026-05/2026-05-07-backend-custom-analyzer-staged-inventory.md).

`MER0005`, `MER0007`, and `MER0009` through `MER0025` were added from the remaining Roslyn-suitable candidates in the same audit. `MER0005`, `MER0006`, `MER0012`, and `MER0021` are now `warning` in committed `.editorconfig` for analyzer-scope live analysis and explicit analyzer-enabled lanes; the remaining rules in this packet stay inventory-driven until their queues are classified. Their first replayable inventory artifact is [docs/analysis/2026-05/2026-05-07-backend-custom-analyzer-remaining-candidates-inventory.md](docs/analysis/2026-05/2026-05-07-backend-custom-analyzer-remaining-candidates-inventory.md).

## Ownership Notes

- Meridian owns payload-branch initializer conditionals through `MER0001`.
- Meridian owns broad nested try/catch fallback flow through `MER0002`.
- Meridian owns unsafe output-cache endpoint metadata drift through `MER0003`.
- Meridian owns high-signal controller policy presence through `MER0004`; exact policy correctness stays in tests.
- Meridian owns admin controller shape drift through `MER0005`; exact admin policy names stay in tests.
- Meridian owns controller-action service location through `MER0006`; broader runtime service-location review needs allowlists.
- Meridian owns raw configuration/environment read inventory through `MER0007`; config-file conflicts stay in validators.
- Meridian owns `MERIDIAN_SKIP_*` raw-read containment through `MER0008`.
- Meridian owns async controller cancellation exposure through `MER0009`; overload correctness remains inventory-led.
- Meridian owns deterministic time/delay source drift through `MER0010`.
- Meridian owns static mutable controller/auth state inventory through `MER0011`.
- Meridian owns source-level health-check registration parity through `MER0012`.
- Meridian owns source-level Clean Architecture boundary drift through `MER0013`; MSBuild/package graph checks stay outside Roslyn.
- Meridian owns obvious model ownership drift through `MER0014`.
- Meridian owns in-memory string helper usage inventory through `MER0015`; EF query expressions stay excluded.
- Meridian owns ad hoc JSON option construction inventory through `MER0016`.
- Meridian owns unbounded EF materialisation inventory through `MER0017`.
- Meridian owns raw SQL API placement/interpolation inventory through `MER0018`.
- Meridian owns direct ProblemDetails construction in controller actions through `MER0019`.
- Meridian owns direct controller repository/DbContext access inventory through `MER0020`.
- Meridian owns backend logging contract source drift through `MER0021`.
- Meridian owns direct Redis keyspace scan inventory through `MER0022`.
- Meridian owns detached runtime task/cancellation inventory through `MER0023`.
- Meridian owns StringExtensions-in-queryable predicate drift through `MER0024`.
- Meridian owns empty property-pattern brace guard inventory through `MER0025`.
- Sonar keeps broader nested-ternary ownership through `S3358`.
- Broader catch quality remains shared with Sonar and SDK analyzers; `MER0002` stays narrow to avoid duplicate exception-handling noise.
- Future backend rules should go through an overlap review before rollout.

## Rule-Addition Checklist

Before landing `MER0002+` or any later rule, update these four surfaces in the same change:

1. Analyzer + tests:
   - add the analyzer implementation under `src/Meridian.Analyzer/`
   - add positive and negative tests under `tests/Meridian.Analyzer.Tests/`
   - this is allowed by `docs/reference/2026-05/2026-05-07-agent-test-creation-policy.md` because analyzer tests are the rule contract, not speculative product-behavior tests
2. Operator rollout:
   - update `apps/backend/.editorconfig` in the Meridian repo
   - confirm the intended wrapper lane in `scripts/tooling/run-backend-analyzers.py` in the Meridian repo
3. Package docs:
   - add the rule file under `docs/rules/`
   - add the rule row and rollout state note in this `README.md`
   - update `docs/guide.md` or `docs/usage-example.md` if the workflow changed
4. Replayable evidence:
   - if the rule is inventory-driven or pilot-only, save one dated `docs/analysis/` artifact for the candidate queue and link it from the rule doc before any broader warning promotion
