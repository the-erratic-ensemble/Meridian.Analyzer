# Usage Example

Use these commands when validating Meridian-owned backend analyzers.

## Package Consumption

Generic consumer setup is standard NuGet usage:

```bash
dotnet add package Meridian.Analyzer
```

The rest of this document is Meridian-specific and focuses on the wrapper commands used to inventory and stage rules in the Meridian backend.

## Entry Points

- Root package command: `rtk pnpm backend:analyzers:validate`
- Root package inventory command: `rtk pnpm backend:analyzers:inventory`
- Root package fallback command: `rtk pnpm backend:analyzers:validate:build`
- Direct wrapper: `rtk proxy python3 scripts/tooling/run-backend-analyzers.py`

## Default Pilot Validation

```bash
rtk pnpm backend:analyzers:validate
```

This validates the currently enabled pilot scope.

The wrapper default diagnostics now cover the current Meridian rule set: `MER0001` and `MER0002`.

## Narrow The Pilot Scope

```bash
rtk pnpm backend:analyzers:validate -- --include apps/backend/Meridian.API/Features/Dev/Controllers/DevController.Diagnostics.cs
```

`--include` only narrows within already-enabled scope. It does not override disabled severities.

## Run A Broader Inventory

```bash
rtk pnpm backend:analyzers:inventory
```

Inventory mode temporarily writes a project-local `.editorconfig` override, scans the target project, writes a JSON report, and restores project state before exiting.

## Inventory One File Outside The Pilot

```bash
rtk pnpm backend:analyzers:inventory -- --include apps/backend/Meridian.API/Features/Council/Controllers/CouncilIntelligenceController.Helpers.cs
```

## Inventory A Specific MER0002 Candidate

```bash
rtk pnpm backend:analyzers:inventory -- --diagnostics MER0002 --project apps/backend/Meridian.Analytics/Meridian.Analytics.csproj --include apps/backend/Meridian.Analytics/Services/ClickHouseAnalyticsService.cs
```

## Build Fallback

```bash
rtk pnpm backend:analyzers:validate:build
```

Use this when you need to prove analyzer loading or compare the pilot against another analyzer-scope project.

## Example Report Output

Inventory runs write a JSON report such as:

`docs/analysis/2026-04/backend-custom-analyzers-meridian-api-inventory-report/format-report.json`

The wrapper parses that report and fails when matching diagnostics are present, even when no code fix exists.
