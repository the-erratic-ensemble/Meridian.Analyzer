# Meridian.Analyzer

`Meridian.Analyzer` is a Roslyn analyzer package for backend readability, architecture, security, reliability, and performance rules.

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

## Package Summary

- Analyzer project: `src/Meridian.Analyzer/Meridian.Analyzer.csproj`
- Test project: `tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj`
- Diagnostic prefix: `MER`
- Category namespaces: `Meridian.Readability`, `Meridian.Security`, `Meridian.Architecture`, `Meridian.Reliability`, `Meridian.Performance`
- Packaging status: published on `nuget.org`

## Maintainer Checks

- Local test run: `dotnet test tests/Meridian.Analyzer.Tests/Meridian.Analyzer.Tests.csproj -c Release`
- Local package smoke check: `dotnet pack src/Meridian.Analyzer/Meridian.Analyzer.csproj -c Release -o artifacts`

Use `dotnet test` and `dotnet pack` from this repo to validate the standalone package itself.

## Consumer Configuration

Configure analyzer severities in your consuming project's `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.MER0001.severity = warning
dotnet_diagnostic.MER0002.severity = warning
```

You can enable only the rules you want. Each rule document describes the contract it enforces and the preferred refactor shape.

## Notes

- `nuget.org` is public. The current publish lane is fine for public distribution, but it is not a private feed.
- Release automation depends on the `NUGET_KEY` GitHub secret. If that key rotates or loses push scope, the release workflow will fail at publish time.
- Some rules encode opinionated architectural conventions. Review the rule docs before enabling large sets as build warnings in an existing codebase.

## Current Rules

| Rule | Diagnostic ID | Category | Preferred refactor |
| --- | --- | --- | --- |
| [Ternary in initializer payload branches](docs/rules/MER0001.md) | `MER0001` | Readability | Stage the payload branch in a named local or helper before building the initializer |
| [Broad nested try/catch fallback flow](docs/rules/MER0002.md) | `MER0002` | Readability | Extract the inner fallback branch into a helper or flatten the exception-handling flow |
| [Unsafe output-cache boundary](docs/rules/MER0003.md) | `MER0003` | Security | Remove `[OutputCache]` or replace it with no-store caching unless a persona-safe cache policy is reviewed |
| [Controller authorization policy boundary](docs/rules/MER0004.md) | `MER0004` | Security | Declare class-level or action-level policies on admin and high-risk controllers |
| [Admin controller shape contract](docs/rules/MER0005.md) | `MER0005` | Security | Align admin controllers on `Admin*Controller`, `api/admin`, and `AdminControllerBase` |
| [Controller service locator boundary](docs/rules/MER0006.md) | `MER0006` | Architecture | Use constructor injection or `[FromServices]` instead of action-body service location |
| [Raw configuration read boundary](docs/rules/MER0007.md) | `MER0007` | Reliability | Move raw reads to typed options, startup guards, or provider adapters |
| [Startup bypass flag containment](docs/rules/MER0008.md) | `MER0008` | Security | Move `MERIDIAN_SKIP_*` reads behind `StartupGuards` or typed startup-skip options |
| [Controller cancellation boundary](docs/rules/MER0009.md) | `MER0009` | Reliability | Add `CancellationToken` to async actions and avoid `CancellationToken.None` in request code |
| [Clock and deterministic delay boundary](docs/rules/MER0010.md) | `MER0010` | Reliability | Use a clock abstraction or `TimeProvider` for runtime time or delay work |
| [Static mutable runtime state](docs/rules/MER0011.md) | `MER0011` | Reliability | Move static mutable state from controllers or handlers into injectable bounded services |
| [Health-check registration parity](docs/rules/MER0012.md) | `MER0012` | Reliability | Register every source `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck` through health-check registration |
| [Backend layer-boundary guard](docs/rules/MER0013.md) | `MER0013` | Architecture | Move dependencies to the documented layer boundary |
| [Model ownership boundary](docs/rules/MER0014.md) | `MER0014` | Architecture | Keep DTOs feature-local and entities in the database entity boundary |
| [String helper usage](docs/rules/MER0015.md) | `MER0015` | Readability | Use shared string helpers in in-memory code |
| [Shared JSON profile boundary](docs/rules/MER0016.md) | `MER0016` | Architecture | Move ad hoc JSON options into shared profiles or a named factory |
| [Unbounded EF materialisation](docs/rules/MER0017.md) | `MER0017` | Performance | Add obvious `Where`, `Take`, or `Skip` bounds before async materialisation |
| [Raw SQL boundary](docs/rules/MER0018.md) | `MER0018` | Security | Keep SQL in persistence boundaries and prefer interpolated APIs over raw APIs |
| [ProblemDetails construction boundary](docs/rules/MER0019.md) | `MER0019` | Reliability | Use shared ProblemDetails helpers from controller actions |
| [Controller data-access boundary](docs/rules/MER0020.md) | `MER0020` | Architecture | Delegate repository, DbContext, and EF work to services or facades |
| [Backend logging contract](docs/rules/MER0021.md) | `MER0021` | Reliability | Use Serilog outside framework-edge boundaries |
| [Redis keyspace scan boundary](docs/rules/MER0022.md) | `MER0022` | Performance | Route `IServer.Keys` scans through an approved bounded helper |
| [Detached runtime task boundary](docs/rules/MER0023.md) | `MER0023` | Reliability | Give background work an owned lifetime, cancellation path, and observability boundary |
| [IQueryable string-extension guard boundary](docs/rules/MER0024.md) | `MER0024` | Reliability | Replace `StringExtensions.IsNullOr*` predicates inside queryable or expression-returning methods with translatable query guards |
| [Empty is-pattern brace guard boundary](docs/rules/MER0025.md) | `MER0025` | Readability | Replace empty property-pattern braces with shared nullable helpers or explicit null checks when behavior matches |

## Rule-Addition Checklist

Before landing `MER0002+` or any later rule, update these surfaces in the same change:

1. Analyzer implementation under `src/Meridian.Analyzer/`
2. Positive and negative tests under `tests/Meridian.Analyzer.Tests/`
3. Rule documentation under `docs/rules/`
4. The rule index in this `README.md`
5. `docs/guide.md` or `docs/usage-example.md` when the maintainer workflow changed
