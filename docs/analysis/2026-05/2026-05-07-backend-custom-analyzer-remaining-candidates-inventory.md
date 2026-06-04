# Backend Custom Analyzer Remaining Candidates Inventory

This note preserves the first replayable inventory snapshot for the remaining Roslyn-suitable backend analyzer candidates implemented in this package.

## Scope

Implemented rules:

- `MER0005`, `MER0007`, `MER0009`, `MER0010`, `MER0011`, `MER0012`
- `MER0014`, `MER0015`, `MER0016`, `MER0017`, `MER0018`, `MER0019`, `MER0020`, `MER0021`, `MER0022`, `MER0023`

Out-of-scope candidates stayed outside Roslyn. Those included recurring-job `CancellationToken.None`, full controller policy correctness, build/package graph rules, config-file conflicts, runtime storage or security contracts, and source-size hotspot reporting.

## Findings

- The skipped candidates were feasible; rollout quality was the limiting factor.
- `MER0005` originally over-reported admin partial classes because route and base-type data were split across declarations. Partial-declaration merging fixed that and the rule passed cleanly across `Meridian.API`.
- `MER0010` originally reported passive entity, DTO, and value-object defaults too broadly. The rule was narrowed to stay focused on runtime code.
- `MER0014`, `MER0015`, `MER0017`, `MER0018`, `MER0021`, and `MER0022` had active queues and stayed inventory-only.

## Validation Snapshot

| Command | Result |
| --- | --- |
| `rtk pnpm backend:analyzers:validate` | Passed |
| `rtk pnpm backend:analyzers:validate:build -- --diagnostics MER0005 --project apps/backend/Meridian.API/Meridian.API.csproj` | Passed after the partial-class fix |
| `rtk pnpm backend:analyzers:validate:build -- --diagnostics MER0014,MER0015,MER0016 --project apps/backend/Meridian.Shared/Meridian.Shared.csproj` | Expected positive inventory: `19` errors |
| `rtk pnpm backend:analyzers:validate:build -- --diagnostics MER0017,MER0018,MER0022 --project apps/backend/Meridian.Infrastructure/Meridian.Infrastructure.csproj` | Historical positive inventory: `86` errors before later queue cleanup |

Consumer loading validation must use a consumer project such as `Meridian.API`, `Meridian.Shared`, or `Meridian.Infrastructure`. Building the analyzer project itself is compile validation only.

## Current Queue Evidence

`MER0014`, `MER0015`, and `MER0016` on `Meridian.Shared`:

- `MER0014`: shared request, response, entity, and DTO naming surfaces including `Requests/*`, `Responses/*`, and `Entities/*`
- `MER0015`: shared raw string checks in helpers and observability code
- `MER0016`: no current `Meridian.Shared` hits in the focused pass

`MER0017`, `MER0018`, and `MER0022` on `Meridian.Infrastructure`:

- `MER0017`: broad async materialization queue across repositories and data-access services
- `MER0018`: raw SQL and interpolated SQL queue across data-access services and repositories
- `MER0022`: initial direct Redis scan usage later moved behind a dedicated keyspace-scanner helper

## Promotion Notes

- Best near-term promotion candidate: `MER0005`
- Possible targeted candidate after a small queue review: `MER0019`
- Keep inventory-only: `MER0010`, `MER0014`, `MER0015`, `MER0017`, `MER0018`, `MER0021`, `MER0022`, and `MER0023`
- Do not promote `MER0012` until health-check registrations through feature extension methods are classified

## Post-Audit Hardening Notes

- `MER0012` matches `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck` by namespace and ignores unrelated same-name interfaces.
- `MER0022` resolves the receiver type and only reports semantic `StackExchange.Redis.IServer.Keys(...)` calls outside the approved helper boundary.
- `MER0010` only exempts passive property and field default initializers in model-like paths.
- `MER0017` treats local variables initialized from visible `Where`, `Take`, or `Skip` chains as bounded when materialized later.
- `MER0020` no longer treats a generic `context` receiver name as DbContext evidence by itself.
