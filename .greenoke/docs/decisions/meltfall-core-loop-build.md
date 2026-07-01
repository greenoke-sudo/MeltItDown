# Decision — MELTFALL core-loop build & QA verdict

**Date:** 2026-07-01
**Status:** shipped (PASS_WITH_DEFERRALS)
**Pipeline:** greenoke spec → plan → build → QA gate, driven in Cowork against the live Unity Editor `MeltItDown@d0f67c42`.

## What shipped
The full MELTFALL core gameplay loop (design Proto 1–3) as one vertical slice:
- **Code:** `Assets/MeltFall/` — data SOs (Liquid/Material/Level/LoopTuning), runtime (MeltableMaterial, LiquidGun cone-cast melt, FuelTank, Gem, LevelManager play-state machine, StageInput, StageCameraRig, SafeZone/HazardZone, SceneBootstrap), HUD views, Editor builders. Own asmdef + `MeltFall.Editor` asmdef.
- **Data:** Water/Acid/Solvent/Heat liquids; Sand/Metal/Stone/Ice materials (matching table §4); Level_01/02/03; LoopTuning.
- **Scenes:** `Meltfall_Play` (grey-box), `Meltfall_Match` (Proto 2 excavation, 3 liquids), `Meltfall_Depth` (Proto 3 — 3 gems, safe + hazard zones, 4 layers, stars).
- **Prefabs:** HUD, LiquidButton. TMP Essential Resources imported.

## Verification
- Compile-clean in MeltItDown (0 errors/warnings); Play-mode smoke on all 3 scenes (correct init: gems, liquids, zones, defs; 0 gameplay runtime errors; timeScale not stuck).
- **Independent cold QA** (derived checks from spec §6/§7/§9) → initially NEEDS_REVIEW (4 BLOCKING). All 4 fixed:
  - B1 play-state machine now enters Spraying/PurgeDelay (from gun selector state) and CollapsingResolving (on `MeltableMaterial.Cleared`).
  - B2 firing gated while Resolved (`LiquidGun.firingBlocked`).
  - B3 landing slow-mo emphasis beat on safe landing (LoopTuning-driven coroutine).
  - B4 app background/focus pause stops the stream (`OnApplicationPause`).
  - (+ MINOR: `MarkLost` now fires `GemsChanged`.)
- **Terminal verdict:** `build_verdict.py` → **PASS_WITH_DEFERRALS** (0 open BLOCKING, 5 accepted deferrals, 0 builder-emitted). QA report: `.greenoke-run/build-20260701-120109/build/qa/qa-report.md` (ephemeral).

## Accepted deferrals (user-approved scope for this build)
Art-polished HUD; dissolve shader (melt is shrink+collider-disable for now); EditMode/PlayMode tests; multi-level flow/continue; juice/audio/monetization.

## Open MINORs (non-blocking, future)
Per-gem tracker identity; `LevelDefinition.availableLiquidIds` is currently decorative (selector driven by the gun's list); `Meltfall_Play` has no HUD (intended grey-box); camera parallax pans during firing.

## Notes / gotchas
See `../../work/meltfall-core-loop/build-status.md` for the Unity/CodeDom Editor-automation gotchas (load assets after OpenScene; avoid Refresh-before-Load; non-generic component APIs in the sandbox).
