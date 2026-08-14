# Meridian.Analyzer

`Meridian.Analyzer` is a Roslyn analyzer package for ASP.NET Core codebases with readability, architecture, security, reliability, and performance rules.

## Quick Start

Install the published analyzer package into a project:

```bash
dotnet add package Meridian.Analyzer
```

Or add an explicit package reference:

```xml
<ItemGroup>
  <PackageReference Include="Meridian.Analyzer" Version="0.5.*" PrivateAssets="all" />
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
- Local package build: `dotnet pack src/Meridian.Analyzer/Meridian.Analyzer.csproj -c Release -o artifacts`

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
- Review each rule's architectural assumptions before enabling large sets as build warnings in an existing codebase.

## Current Rules

| Rule | Diagnostic ID | Category | Preferred refactor |
| --- | --- | --- | --- |
| [Ternary in initializer payload branches](docs/rules/MER0001.md) | `MER0001` | Readability | Stage the payload branch in a named local or helper before building the initializer |
| [Broad nested try/catch fallback flow](docs/rules/MER0002.md) | `MER0002` | Readability | Extract the inner fallback branch into a helper or flatten the exception-handling flow |
| [Unsafe output-cache usage](docs/rules/MER0003.md) | `MER0003` | Security | Use no-store caching for sensitive endpoints; apply `[OutputCache]` only with a policy safe for every caller |
| [Missing explicit controller policy](docs/rules/MER0004.md) | `MER0004` | Security | Declare class-level or action-level policies on admin and high-risk controllers |
| [Admin controller shape mismatch](docs/rules/MER0005.md) | `MER0005` | Security | Align admin controllers on `Admin*Controller`, `api/admin`, and `AdminControllerBase` |
| [Controller service location](docs/rules/MER0006.md) | `MER0006` | Architecture | Use constructor injection or `[FromServices]` for action dependencies |
| [Raw configuration reads](docs/rules/MER0007.md) | `MER0007` | Reliability | Move raw reads to typed options, startup guards, or provider adapters |
| [Startup bypass flag containment](docs/rules/MER0008.md) | `MER0008` | Security | Move `MERIDIAN_SKIP_*` reads behind `StartupGuards` or typed startup-skip options |
| [Missing controller cancellation token](docs/rules/MER0009.md) | `MER0009` | Reliability | Add `CancellationToken` to async actions and forward request cancellation |
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
| [Unowned runtime tasks](docs/rules/MER0023.md) | `MER0023` | Reliability | Await, return, aggregate, or explicitly own task-returning work |
| [Queryable string-extension predicates](docs/rules/MER0024.md) | `MER0024` | Reliability | Replace `StringExtensions.IsNullOr*` inside queryable or expression-returning methods with translatable query guards |
| [Empty is-pattern braces](docs/rules/MER0025.md) | `MER0025` | Readability | Replace empty property-pattern braces with shared nullable helpers or explicit null checks when behavior matches |
| [Nested ternary chains](docs/rules/MER0026.md) | `MER0026` | Readability | Extract long ternary decision trees into named steps |
| [Long boolean chains](docs/rules/MER0027.md) | `MER0027` | Readability | Split long `&&` and `||` chains into named predicates |
| [Heavy initializer expressions](docs/rules/MER0028.md) | `MER0028` | Readability | Stage large multi-line initializer member expressions before object construction |
| [Long LINQ and EF chains](docs/rules/MER0029.md) | `MER0029` | Readability | Break long fluent query chains into named intermediate steps |
| [Per-iteration loop try/catch nesting](docs/rules/MER0030.md) | `MER0030` | Readability | Extract iteration work into a helper or result-returning boundary before outer loop shutdown handling |
| [Nested collection-trimming while loops](docs/rules/MER0031.md) | `MER0031` | Readability | Move trimming loops into a named helper or bounded collection abstraction |
| [Inline conditional record rewrites](docs/rules/MER0032.md) | `MER0032` | Readability | Move conditional record clones with multiple or nested member updates into named helpers |
| [Heavy inline query lambdas](docs/rules/MER0033.md) | `MER0033` | Readability | Move large query lambdas into named query steps or helpers |
| [Direct ordinal string comparisons](docs/rules/MER0034.md) | `MER0034` | Readability | Use shared ordinal string comparison helpers |
| [Missing route cancellation](docs/rules/MER0035.md) | `MER0035` | Reliability | Expose and forward cancellation from async route boundaries |
| [Implicit string ordering comparer](docs/rules/MER0036.md) | `MER0036` | Reliability | Pass an explicit comparer to string-key ordering and sorted string collections |
| [Unowned ArrayPool rentals](docs/rules/MER0037.md) | `MER0037` | Reliability | Return the rental to the same pool or transfer it to a disposable rental owner |
| [Unpaired SemaphoreSlim acquisition](docs/rules/MER0038.md) | `MER0038` | Reliability | Release the acquired capacity in a covering finally block or releaser owner |
| [Unbound database transaction](docs/rules/MER0039.md) | `MER0039` | Reliability | Assign the active transaction before executing the command |
| [Escaping JsonElement lifetime](docs/rules/MER0040.md) | `MER0040` | Reliability | Clone the element before returning or storing it beyond the document scope |
| [Literal null suppression](docs/rules/MER0041.md) | `MER0041` | Reliability | Model literal null and default values as nullable state |
| [Named boolean literal arguments](docs/rules/MER0042.md) | `MER0042` | Readability | Add the parameter name at positional boolean literals |
| [Unordered collection selection](docs/rules/MER0043.md) | `MER0043` | Reliability | Order dictionary and set data before positional selection |
| [String equality comparers](docs/rules/MER0044.md) | `MER0044` | Readability | Pass an explicit equality comparer for string operations |
| [Cancellation through broad catches](docs/rules/MER0045.md) | `MER0045` | Reliability | Preserve OperationCanceledException across broad catch handlers |
| [Asynchronous TaskCompletionSource continuations](docs/rules/MER0046.md) | `MER0046` | Reliability | Create task-completion sources with asynchronous continuations |
| [Delegate invocation outside locks](docs/rules/MER0047.md) | `MER0047` | Reliability | Invoke delegates and events after leaving a lock body |
| [Exception identity control flow](docs/rules/MER0048.md) | `MER0048` | Reliability | Branch on exception type or structured identity |
| [Array reference equality](docs/rules/MER0049.md) | `MER0049` | Reliability | Compare array contents explicitly or use `ReferenceEquals` for identity |
| [Runtime hash code boundary](docs/rules/MER0050.md) | `MER0050` | Reliability | Keep runtime hashes inside equality code and use stable hashes for durable values |
| [Awaitable anonymous callbacks](docs/rules/MER0051.md) | `MER0051` | Reliability | Use task-returning delegates for asynchronous callbacks |
| [Collection enumeration mutation](docs/rules/MER0052.md) | `MER0052` | Reliability | Enumerate a snapshot or separate mutation from the active `foreach` |
| [Signed Abs before modulo](docs/rules/MER0053.md) | `MER0053` | Reliability | Handle the signed minimum before applying `Math.Abs` in modulo arithmetic |
| [TryGetValue result](docs/rules/MER0054.md) | `MER0054` | Reliability | Consume the Boolean result or use an explicit default-on-missing operation |
| [Binary byte order](docs/rules/MER0055.md) | `MER0055` | Reliability | Use an explicit little-endian or big-endian conversion |
| [Parsed enum values](docs/rules/MER0056.md) | `MER0056` | Reliability | Validate ordinary enum values with `Enum.IsDefined` |
| [Midpoint rounding](docs/rules/MER0057.md) | `MER0057` | Reliability | Pass `MidpointRounding` explicitly |
| [Bounded variable stackalloc](docs/rules/MER0058.md) | `MER0058` | Performance | Keep runtime stack allocations under a fixed ceiling |
| [Search result index guard](docs/rules/MER0059.md) | `MER0059` | Reliability | Check search misses before index or range use |
| [MemoryStream buffer range](docs/rules/MER0060.md) | `MER0060` | Reliability | Slice backing buffers to the written range before they escape |

## Rule-Addition Checklist

For every new or materially changed rule, update these surfaces in the same change:

1. Analyzer implementation under `src/Meridian.Analyzer/`
2. Reporting and non-reporting tests under `tests/Meridian.Analyzer.Tests/`
3. Rule documentation under `docs/rules/`
4. The rule index in this `README.md`
5. `docs/guide.md` or `docs/usage-example.md` when the docs or maintainer flow changed
