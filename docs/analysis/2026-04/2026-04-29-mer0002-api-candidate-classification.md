# MER0002 API Candidate Classification

This note preserves the historical `Meridian.API` queue snapshot that informed the original `MER0002` rollout.

## Scope

- consumer project: `Meridian.API`
- rule: `MER0002`
- purpose: classify the old replayable queue before targeted refactors removed it

The current replayable `Meridian.API` inventory for `MER0002` is `0`. This artifact is historical context only.

## Historical Snapshot

At the time of classification, the API queue contained `10` candidates.

Classification buckets:

- `Rollout candidate`: likely true positive for the intended contract
- `Likely exemption`: probably deliberate local containment or best-effort behavior
- `Ambiguous`: plausible signal, but policy was still needed

## Strongest Rollout Candidates

- Stripe startup validation flow with degraded fallback hidden inline
- Plan configuration bulk-apply flows that dropped failed work while the outer operation continued

These were the clearest examples of degraded primary behavior being buried inside a larger exception-handling region.

## Likely Exemptions

- Redis rate-limit statistics collection
- Optional BetterAuth metadata lookup fallback

These looked closer to operational resilience or optional metadata capture than to hidden degraded core behavior.

## Policy Boundary

The unresolved line was continue-on-error bulk loops. That affected several tenant, admin, migration, and usage flows where the outer operation continued while per-item failures were recorded inline.

That uncertainty was the reason `MER0002` stayed inventory-led until the API queue was deliberately remediated.
