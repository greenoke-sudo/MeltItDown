# Work index — MeltItDown

Live work tracker. One row per active piece of work; update Status + Active Task as it moves.
The spec/plan/build runs write under `.greenoke-run/<run-id>/` (ephemeral); this index is the
durable pointer to what's in flight.

| Type | Slug | Status | Active Task |
|---|---|---|---|
| feature | meltfall-core-loop | plan ✅ (verify_spec + verify_plan PASS) | build next (`/greenoke:build`) |

Durable [spec](meltfall-core-loop/feature-spec.md) + [plan](meltfall-core-loop/feature-plan.md) · design ref: `.greenoke/research/2026-07-01-meltfall-design.md`. Build order = 5 slices (Proto 1 core → SO data → Proto 2 matching → Proto 3 depth → HUD/feel polish).
