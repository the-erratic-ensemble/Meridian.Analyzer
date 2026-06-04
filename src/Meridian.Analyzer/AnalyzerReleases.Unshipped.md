; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MER0001 | Meridian.Readability | Warning | Do not use conditional expressions directly in object and anonymous-object initializer members.
MER0002 | Meridian.Readability | Warning | Do not hide fallback flow in broad nested try/catch blocks inside another try block.
MER0003 | Meridian.Security | Warning | Do not combine output caching with tenant, entitlement, quota, plan, or policy-sensitive endpoint metadata.
MER0004 | Meridian.Security | Warning | Require explicit authorization policies on admin and high-risk controller surfaces.
MER0005 | Meridian.Security | Warning | Keep admin controller surfaces on the Admin*Controller, api/admin route, and AdminControllerBase shape.
MER0006 | Meridian.Architecture | Warning | Do not resolve services from RequestServices or IServiceProvider inside controller actions.
MER0007 | Meridian.Reliability | Warning | Contain raw configuration and environment reads behind typed options, startup guards, or provider adapters.
MER0008 | Meridian.Security | Warning | Keep MERIDIAN_SKIP_* startup bypass flag reads inside startup guard code or typed startup options.
MER0009 | Meridian.Reliability | Warning | Expose cancellation in async controller actions and avoid CancellationToken.None in request-scoped code.
MER0010 | Meridian.Reliability | Warning | Use a clock abstraction or TimeProvider instead of direct system time, raw Task.Delay, or raw timers.
MER0011 | Meridian.Reliability | Warning | Avoid static mutable state in controllers and auth/session handlers.
MER0012 | Meridian.Reliability | Warning | Register source IHealthCheck implementations through health-check registration.
MER0013 | Meridian.Architecture | Warning | Respect documented application layer boundaries.
MER0014 | Meridian.Architecture | Info | Keep model, DTO, and entity ownership clear and reviewable.
MER0015 | Meridian.Readability | Warning | Prefer shared string helpers for in-memory string normalization.
MER0016 | Meridian.Architecture | Warning | Use shared JSON profiles instead of ad hoc JSON option construction.
MER0017 | Meridian.Performance | Warning | Review async materialization without an explicit Where/Take/Skip bound.
MER0018 | Meridian.Security | Warning | Keep raw SQL APIs inside persistence code and prefer interpolated SQL APIs.
MER0019 | Meridian.Reliability | Warning | Use shared ProblemDetails helpers instead of constructing ProblemDetails inside controller actions.
MER0020 | Meridian.Architecture | Warning | Keep controller actions out of repository, DbContext, and EF query details.
MER0021 | Meridian.Reliability | Warning | Use Serilog in runtime code outside framework adapters and hosting edges.
MER0022 | Meridian.Performance | Warning | Route Redis keyspace scans through a dedicated bounded helper.
MER0023 | Meridian.Reliability | Warning | Own detached runtime tasks with explicit lifetime, cancellation, and observability.
MER0024 | Meridian.Reliability | Warning | Avoid shared string extension guards inside IQueryable and expression predicates.
MER0025 | Meridian.Readability | Warning | Avoid empty property-pattern braces such as `is { }`, `is not { }`, and tuple elements like `({ }, { })`.
