# Work index — MeltItDown

Live work tracker. One row per active piece of work; update Status + Active Task as it moves.
The spec/plan/build runs write under `.greenoke-run/<run-id>/` (ephemeral); this index is the
durable pointer to what's in flight.

| Type | Slug | Status | Active Task |
|---|---|---|---|
| feature | meltfall-core-loop | build — Slices 1–5 built & Play-mode smoke-tested (3 scenes, HUD, full liquid/material set) | playtest feel; final greenoke QA gate; juice/audio/monetization (future) |

Durable [spec](meltfall-core-loop/feature-spec.md) + [plan](meltfall-core-loop/feature-plan.md) + [build status](meltfall-core-loop/build-status.md) · design ref: `.greenoke/research/2026-07-01-meltfall-design.md`. Full `Assets/MeltFall/` script layer (runtime + data + HUD) written and compiling; Editor-side assets (SOs, shader, prefabs, scene, input map) pending.
