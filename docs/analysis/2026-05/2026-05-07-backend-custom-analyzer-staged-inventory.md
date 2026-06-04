# Backend Custom Analyzer Staged Inventory

This note preserves the first replayable smoke inventory for the staged rules in this package.

## Scope

- `MER0003`: unsafe output-cache boundary
- `MER0004`: controller authorization policy boundary
- `MER0006`: controller action service locator boundary
- `MER0008`: startup bypass flag containment
- `MER0013`: backend layer-boundary guard

These scans were run against Meridian consumer projects to prove the rules loaded through the wrapper and produced representative signal before warning promotion.

## Wrapper Note

The Meridian wrapper expands inventory folder scopes to concrete `.cs` files in batches and serializes temporary `.editorconfig` overrides per project. That prevents `dotnet format` inventory passes from missing analyzer results or clobbering rule severity state during parallel runs.

## Replay Commands And Results

### `MER0003` and `MER0004` targeted API smoke

```bash
rtk pnpm backend:analyzers:inventory -- --diagnostics MER0003,MER0004 --project apps/backend/Meridian.API/Meridian.API.csproj --include apps/backend/Meridian.API/Features/Reports/Controllers/ReportStatusController.cs --include apps/backend/Meridian.API/Features/Admin/Controllers/AdminPlatformController.cs --include apps/backend/Meridian.API/Features/Greenspace/Controllers/GreenspaceController.cs
```

Result: `0` matching diagnostics.

Interpretation:

- `ReportStatusController` already had report-policy metadata.
- `AdminPlatformController` already had admin-policy metadata.
- `GreenspaceController` used output caching without the sensitive metadata shape covered by `MER0003`.

### `MER0006` targeted API smoke

```bash
rtk pnpm backend:analyzers:inventory -- --diagnostics MER0006 --project apps/backend/Meridian.API/Meridian.API.csproj --include apps/backend/Meridian.API/Features/Reports/Controllers/ReportStatusController.cs --include apps/backend/Meridian.API/Features/Admin/Controllers/AdminPlatformController.cs
```

Result: `11` matching diagnostics.

Representative hits:

- `apps/backend/Meridian.API/Features/Admin/Controllers/AdminPlatformController.cs`: `10` action-body `HttpContext.RequestServices.GetRequiredService(...)` calls
- `apps/backend/Meridian.API/Features/Reports/Controllers/ReportStatusController.cs`: `1` action-body `HttpContext.RequestServices.GetRequiredService(...)` call

Interpretation:

- The rule caught the intended service-location shape.
- This queue needed refactoring or classification before promotion.

### `MER0008` targeted API smoke

```bash
rtk pnpm backend:analyzers:inventory -- --diagnostics MER0008 --project apps/backend/Meridian.API/Meridian.API.csproj --include apps/backend/Meridian.API/Infrastructure/Startup/Extensions/BusinessServicesBuilderExtensions.cs --include apps/backend/Meridian.API/Features/Reports/ReportsServiceRegistrationExtensions.cs
```

Result: `2` matching diagnostics.

Hits:

- `apps/backend/Meridian.API/Features/Reports/ReportsServiceRegistrationExtensions.cs`: `MERIDIAN_SKIP_CHROMIUM_STARTUP`
- `apps/backend/Meridian.API/Features/Reports/ReportsServiceRegistrationExtensions.cs`: `MERIDIAN_SKIP_BACKGROUND_SERVICES`

Interpretation:

- The rule caught raw `MERIDIAN_SKIP_*` reads outside `StartupGuards`.
- Promotion depended on introducing a typed startup-skip boundary or accepting an explicit exception.

### `MER0013` targeted Analytics smoke

```bash
rtk pnpm backend:analyzers:inventory -- --diagnostics MER0013 --project apps/backend/Meridian.Analytics/Meridian.Analytics.csproj --include apps/backend/Meridian.Analytics/Services/AnalyticsService.cs --include apps/backend/Meridian.Analytics/Services/EpcAnalyticsService.cs
```

Result: `2` matching diagnostics.

Hits:

- `apps/backend/Meridian.Analytics/Services/AnalyticsService.cs`: `using Meridian.Infrastructure.Database`
- `apps/backend/Meridian.Analytics/Services/EpcAnalyticsService.cs`: `using Meridian.Infrastructure.Database`

Interpretation:

- The rule caught the intended Analytics-to-Infrastructure boundary drift.
- Promotion depended on an explicit storage-boundary decision in the consumer repo.

## Promotion Notes

- `MER0003` and `MER0004` were the cleanest early promotion candidates.
- `MER0006`, `MER0008`, and `MER0013` started as visible work queues rather than immediate warnings.
- Full-project inventories were still required before any warning promotion.
