; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
MER0001 | Meridian.Readability | Warning | Stage conditional expressions before assigning object and anonymous-object initializer members.
MER0002 | Meridian.Readability | Warning | Keep fallback flow visible by extracting broad nested try/catch blocks from outer try blocks.
MER0003 | Meridian.Security | Warning | Apply output caching only to endpoints whose metadata is independent of tenant, entitlement, quota, plan, and policy-sensitive state.
MER0004 | Meridian.Security | Warning | Require explicit authorization policies on admin and high-risk controller surfaces.
MER0005 | Meridian.Security | Warning | Keep admin controller surfaces on the Admin*Controller, api/admin route, and AdminControllerBase shape.
MER0006 | Meridian.Architecture | Warning | Resolve services through constructor injection or action parameters in controller actions.
MER0007 | Meridian.Reliability | Warning | Contain raw configuration and environment reads behind typed options, startup guards, or provider adapters.
MER0008 | Meridian.Security | Warning | Keep MERIDIAN_SKIP_* startup bypass flag reads inside startup guard code or typed startup options.
MER0009 | Meridian.Reliability | Warning | Expose cancellation in async controller actions and forward the request token.
MER0010 | Meridian.Reliability | Warning | Use a clock abstraction or TimeProvider for system time, raw Task.Delay, and raw timers.
MER0011 | Meridian.Reliability | Warning | Keep mutable runtime state in injectable services for controllers and auth/session handlers.
MER0012 | Meridian.Reliability | Warning | Register source IHealthCheck implementations through health-check registration.
MER0013 | Meridian.Architecture | Warning | Respect documented application layer boundaries.
MER0014 | Meridian.Architecture | Info | Keep model, DTO, and entity ownership clear and reviewable.
MER0015 | Meridian.Readability | Warning | Prefer shared string helpers for in-memory string normalization.
MER0016 | Meridian.Architecture | Warning | Use shared JSON profiles for JSON option construction.
MER0017 | Meridian.Performance | Warning | Review async materialization when a visible Where/Take/Skip bound is absent.
MER0018 | Meridian.Security | Warning | Keep raw SQL APIs inside persistence code and prefer interpolated SQL APIs.
MER0019 | Meridian.Reliability | Warning | Use shared ProblemDetails helpers in controller actions.
MER0020 | Meridian.Architecture | Warning | Keep controller actions out of repository, DbContext, and EF query details.
MER0021 | Meridian.Reliability | Warning | Use Serilog in runtime code outside framework adapters and hosting edges.
MER0022 | Meridian.Performance | Warning | Route Redis keyspace scans through a dedicated bounded helper.
MER0023 | Meridian.Reliability | Warning | Await, return, aggregate, or explicitly own task-returning work.
MER0024 | Meridian.Reliability | Warning | Use query-translatable guards inside IQueryable and expression predicates.
MER0025 | Meridian.Readability | Warning | Use named helpers or explicit null checks for empty property-pattern braces such as `is { }`, `is not { }`, and tuple elements like `({ }, { })`.
MER0026 | Meridian.Readability | Warning | Extract named classification steps from deeply nested ternary chains.
MER0027 | Meridian.Readability | Warning | Extract named predicates from overly long chained boolean expressions.
MER0028 | Meridian.Readability | Warning | Move heavy multi-line initializer-member expressions into named locals or helpers.
MER0029 | Meridian.Readability | Warning | Review LINQ and EF fluent chains with eight or more chained query calls.
MER0030 | Meridian.Readability | Warning | Extract broad per-iteration try/catch blocks from nested while-loop and outer try control flow.
MER0031 | Meridian.Readability | Warning | Extract nested collection-trimming while loops into named helpers or bounded collection abstractions.
MER0032 | Meridian.Readability | Warning | Extract conditional LINQ projections that clone a record and rewrite multiple or nested members inline.
MER0033 | Meridian.Readability | Warning | Extract heavy multi-line query lambdas into named query steps or helpers.
MER0034 | Meridian.Readability | Warning | Prefer shared ordinal string comparison helpers.
MER0035 | Meridian.Reliability | Warning | Expose and forward cancellation from async route boundaries.
MER0036 | Meridian.Reliability | Warning | Require an explicit comparer for string-key ordering and sorted string collections.
MER0037 | Meridian.Reliability | Warning | Return ArrayPool rentals exactly once or transfer them to a disposable rental owner.
MER0038 | Meridian.Reliability | Warning | Pair successful SemaphoreSlim waits with one covering release owner.
MER0039 | Meridian.Reliability | Warning | Bind commands created from a transaction-scoped connection before execution.
MER0040 | Meridian.Reliability | Warning | Clone JsonElement values that escape a locally disposed JsonDocument.
MER0041 | Meridian.Reliability | Warning | Model literal null and default values as nullable state.
MER0042 | Meridian.Readability | Warning | Name positional boolean literal arguments at the call site.
MER0043 | Meridian.Reliability | Warning | Order dictionary and set data before positional selection.
MER0044 | Meridian.Readability | Warning | State equality semantics for string-key collections and equality-based LINQ operations.
MER0045 | Meridian.Reliability | Warning | Preserve OperationCanceledException through broad catch handlers.
MER0046 | Meridian.Reliability | Warning | Create TaskCompletionSource instances with RunContinuationsAsynchronously.
MER0047 | Meridian.Reliability | Warning | Invoke delegates and events after leaving a lock body.
MER0048 | Meridian.Reliability | Warning | Use exception identity for control flow.
MER0049 | Meridian.Reliability | Warning | Make array equality semantics explicit.
MER0050 | Meridian.Reliability | Warning | Keep runtime hash codes inside equality hashing.
MER0051 | Meridian.Reliability | Warning | Keep asynchronous anonymous callbacks awaitable.
MER0052 | Meridian.Reliability | Warning | Avoid invalidating standard collection enumerators during foreach.
MER0053 | Meridian.Reliability | Warning | Guard signed Math.Abs before modulo.
MER0054 | Meridian.Reliability | Warning | Consume the Boolean result of dictionary TryGetValue.
MER0055 | Meridian.Reliability | Warning | State byte order for BitConverter numeric conversions.
MER0056 | Meridian.Reliability | Warning | Validate values parsed into ordinary enums.
MER0057 | Meridian.Reliability | Warning | State the midpoint rounding mode for Math.Round and MathF.Round.
MER0058 | Meridian.Performance | Warning | Put a fixed ceiling on variable stackalloc sizes.
MER0059 | Meridian.Reliability | Warning | Guard search results before index and range use.
MER0060 | Meridian.Reliability | Warning | Slice MemoryStream buffers to their written range.
MER0061 | Meridian.Performance | Warning | Use asynchronous ZIP opening APIs inside asynchronous code.
MER0062 | Meridian.Readability | Warning | Name every argument on record-class constructions with at least three arguments.
