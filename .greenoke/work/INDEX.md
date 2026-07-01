# Work index — MeltItDown

Live work tracker. One row per active piece of work; update Status + Active Task as it moves.
The spec/plan/build runs write under `.greenoke-run/<run-id>/` (ephemeral); this index is the
durable pointer to what's in flight.

| Type | Slug | Status | Active Task |
|---|---|---|---|
| feature | meltfall-core-loop | shipped — QA PASS_WITH_DEFERRALS; + tests (11/11 green) + dissolve shader | juice/audio, level-flow, monetization (future features) |

Decision: [meltfall-core-loop-build](../docs/decisions/meltfall-core-loop-build.md). QA verdict PASS_WITH_DEFERRALS. Follow-ups since: EditMode 9/9 + PlayMode 2/2 tests passing; URP dissolve shader + material wired to all meltables. Remaining deferrals: level-flow/continue, juice/audio, monetization.

Durable [spec](meltfall-core-loop/feature-spec.md) + [plan](meltfall-core-loop/feature-plan.md) + [build status](meltfall-core-loop/build-status.md) · design ref: `.greenoke/research/2026-07-01-meltfall-design.md`. Full `Assets/MeltFall/` script layer (runtime + data + HUD) written and compiling; Editor-side assets (SOs, shader, prefabs, scene, input map) pending.
