# Backend Empty Is-Pattern Brace Candidates Inventory

This note preserves the first inventory snapshot for `MER0025` after the rule was narrowed to empty property-pattern braces only.

## Source

- rule: `MER0025`
- contract: empty property-pattern braces only
- evidence source: backend analyzer build-capture output

## Result

- total `MER0025` hits in the captured output: `22`
- non-empty property-pattern hits: `0`

## Pattern Breakdown

- `({ }, { })`: `9`
- `{ } validationFailure`: `6`
- `{ } tenantId`: `2`
- `{ } declaringType`: `1`
- `{ } featureLimit`: `1`
- `{ } planId`: `1`
- `{ } expMonth`: `1`
- `{ } expYear`: `1`

## Project Breakdown

- `Meridian.API`: `15`
- `Meridian.Analytics`: `6`
- `Meridian.CLI`: `1`

## Interpretation

- The narrowed rule only reported empty-brace shapes.
- The captured output did not show populated property patterns such as `{ Count: > 0 }` or `{ IsTrial: true, TrialEndsAt: not null }`.
- The early queue was dominated by tuple-centroid guards and nullable extraction aliases, so the rule stayed inventory-only pending classification and cleanup.
