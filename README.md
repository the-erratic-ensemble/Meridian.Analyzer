# Meridian.Analyzer

`Meridian.Analyzer` is a Roslyn analyzer package for ASP.NET Core codebases with opinionated readability, architecture, security, reliability, and performance rules.

## Quick Start

Install the published analyzer package into a project:

```bash
dotnet add package Meridian.Analyzer
```

Or add an explicit package reference:

```xml
<ItemGroup>
  <PackageReference Include="Meridian.Analyzer" Version="0.2.*" PrivateAssets="all" />
</ItemGroup>
```

## Repository Layout

- `src/Meridian.Analyzer/`: analyzer project, rule implementations, helpers, and Roslyn release tracking files
- `tests/Meridian.Analyzer.Tests/`: analyzer behavior tests
- `docs/`: maintainer guidance, examples, and per-rule documentation
- `LICENSE`: MIT license for the repository and published package metadata

## Documentation

- Usage examples: [docs/usage-example.md](docs/usage-example.md)
- Maintainer guide: [docs/guide.md](docs/guide.md)
- Per-rule documentation: [docs/rules/](docs/rules/)

## Package Facts

- Analyzer project: `src/Meridian.Analyzer/Meridian.Analyzer.csproj`
- Test project: `tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj`
- Diagnostic prefix: `MER`
- Category namespaces: `Meridian.Readability`, `Meridian.Security`, `Meridian.Architecture`, `Meridian.Reliability`, `Meridian.Performance`
- Packaging status: published on `nuget.org`

## Local Checks

- Local test run: `dotnet test tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj -c Release`
- Local package smoke check: `dotnet pack src/Meridian.Analyzer/Meridian.Analyzer.csproj -c Release -o artifacts`

Run these from this repo before publishing or sending a change for review.

## Configure Severity

Configure analyzer severities in your consuming project's `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.MER0001.severity = warning
dotnet_diagnostic.MER0002.severity = warning
```

You can enable as many or as few rules as you want. Each rule doc explains what it reports and how to refactor away from it.

## Notes

- `nuget.org` is a public feed.
- Some rules encode opinionated architectural conventions. Review the rule docs before enabling large sets as build warnings in an existing codebase.

## Current Rules

| Rule | Diagnostic ID | Category | Preferred refactor |
| --- | --- | --- | --- |
| [Ternary in initializer payload branches](docs/rules/MER0001.md) | `MER0001` | Readability | Stage the payload branch in a named local or helper before building the initializer |
| [Broad nested try/catch fallback flow](docs/rules/MER0002.md) | `MER0002` | Readability | Extract the inner fallback branch into a helper or flatten the exception-handling flow |
| [Unsafe output-cache usage](docs/rules/MER0003.md) | `MER0003` | Security | Remove `[OutputCache]` or replace it with no-store caching unless you have a clearly safe cache policy |
| [Missing explicit controller policy](docs/rules/MER0004.md) | `MER0004` | Security | Declare class-level or action-level policies on admin and high-risk controllers |
| [Admin controller shape mismatch](docs/rules/MER0005.md) | `MER0005` | Security | Align admin controllers on `Admin*Controller`, `api/admin`, and `AdminControllerBase` |
| [Controller service location](docs/rules/MER0006.md) | `MER0006` | Architecture | Use constructor injection or `[FromServices]` instead of action-body service location |
| [Raw configuration reads](docs/rules/MER0007.md) | `MER0007` | Reliability | Move raw reads to typed options, startup guards, or provider adapters |
| [Startup bypass flag containment](docs/rules/MER0008.md) | `MER0008` | Security | Move `MERIDIAN_SKIP_*` reads behind `StartupGuards` or typed startup-skip options |
| [Missing controller cancellation token](docs/rules/MER0009.md) | `MER0009` | Reliability | Add `CancellationToken` to async actions and avoid `CancellationToken.None` in request code |
| [Direct time and delay APIs](docs/rules/MER0010.md) | `MER0010` | Reliability | Use a clock abstraction or `TimeProvider` for runtime time or delay work |
| [Static mutable runtime state](docs/rules/MER0011.md) | `MER0011` | Reliability | Move static mutable state from controllers or handlers into injectable bounded services |
| [Health-check registration parity](docs/rules/MER0012.md) | `MER0012` | Reliability | Register every source `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck` through health-check registration |
| [Layering violations](docs/rules/MER0013.md) | `MER0013` | Architecture | Move dependencies back to the intended layer |
| [Model ownership drift](docs/rules/MER0014.md) | `MER0014` | Architecture | Keep DTOs feature-local and entities in dedicated entity folders |
| [String helper usage](docs/rules/MER0015.md) | `MER0015` | Readability | Use shared string helpers in in-memory code |
| [Ad hoc JSON options](docs/rules/MER0016.md) | `MER0016` | Architecture | Move ad hoc JSON options into shared profiles or a named factory |
| [Unbounded EF materialisation](docs/rules/MER0017.md) | `MER0017` | Performance | Add obvious `Where`, `Take`, or `Skip` bounds before async materialisation |
| [Raw SQL outside persistence code](docs/rules/MER0018.md) | `MER0018` | Security | Keep SQL in persistence code and prefer interpolated APIs over raw APIs |
| [Direct ProblemDetails construction](docs/rules/MER0019.md) | `MER0019` | Reliability | Use shared ProblemDetails helpers from controller actions |
| [Controller data access](docs/rules/MER0020.md) | `MER0020` | Architecture | Delegate repository, DbContext, and EF work to services or facades |
| [Non-Serilog runtime logging](docs/rules/MER0021.md) | `MER0021` | Reliability | Use Serilog in runtime code and keep framework logging at the edges |
| [Direct Redis keyspace scans](docs/rules/MER0022.md) | `MER0022` | Performance | Route `IServer.Keys` scans through a dedicated bounded helper |
| [Detached runtime tasks](docs/rules/MER0023.md) | `MER0023` | Reliability | Give background work an explicit lifetime, cancellation path, and observable failure path |
| [Queryable string-extension predicates](docs/rules/MER0024.md) | `MER0024` | Reliability | Replace `StringExtensions.IsNullOr*` inside queryable or expression-returning methods with translatable query guards |
| [Empty is-pattern braces](docs/rules/MER0025.md) | `MER0025` | Readability | Replace empty property-pattern braces with shared nullable helpers or explicit null checks when behavior matches |

## Rule-Addition Checklist

Before landing `MER0002+` or any later rule, update these surfaces in the same change:

1. Analyzer implementation under `src/Meridian.Analyzer/`
2. Positive and negative tests under `tests/Meridian.Analyzer.Tests/`
3. Rule documentation under `docs/rules/`
4. The rule index in this `README.md`
5. `docs/guide.md` or `docs/usage-example.md` when the docs or maintainer flow changed
