# MER0010 Build Warning Calibration

This note preserves the first broad warning calibration for `MER0010`.

## Scope

The calibration was based on existing backend build-output evidence rather than a fresh remediation pass.

Key signal:

- sampled rule: `MER0010`
- observed broad warning volume: `2,146`

## Decision

Keep the rule idea, but do not promote the original broad `MER0010` shape to normal build warnings.

The rule is valid architecture pressure because runtime code should avoid hidden wall-clock and real-delay dependencies. The original broad signal mixed high-value runtime timing issues with low-signal timestamping and passive defaults.

## High-Value Signal Categories

- raw `Task.Delay(...)` in runtime retry or polling code
- raw `new Timer(...)` or `new PeriodicTimer(...)` outside approved time boundaries
- direct local-time reads such as `DateTime.Now` or `DateTimeOffset.Now`
- direct wall-clock reads inside retry, expiry, TTL, lock, or queue timing logic

## Inventory-Only Categories

- audit-field timestamp stamping
- workflow started or completed timestamps
- report and export metadata timestamps
- diagnostics and snapshot timestamps
- passive property or field defaults in model-like paths

## Outcome

`MER0010` remains inventory-only until the rule is narrowed or split into smaller, higher-signal contracts.
