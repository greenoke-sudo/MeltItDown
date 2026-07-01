# Work index — MeltItDown

Live work tracker. One row per active piece of work; update Status + Active Task as it moves.
The spec/plan/build runs write under `.greenoke-run/<run-id>/` (ephemeral); this index is the
durable pointer to what's in flight.

| Type | Slug | Status | Active Task |
|---|---|---|---|
| feature | meltfall-core-loop | spec ✅ (verify_spec PASS) | plan next (`/greenoke:plan`) |

Durable spec: [`.greenoke/work/meltfall-core-loop/feature-spec.md`](meltfall-core-loop/feature-spec.md) · design ref: `.greenoke/research/2026-07-01-meltfall-design.md`
