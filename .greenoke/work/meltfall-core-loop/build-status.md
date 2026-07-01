# Build status — meltfall-core-loop

Build driven in Cowork. Unity 6000.0.62f1 MCP bridge added to MeltItDown and connected
(instance `MeltItDown@d0f67c42`). Full MeltFall code compiles in the real project with
**0 errors, 0 warnings**.

## Slice 1 (Proto 1 grey-box) — PLAYABLE ✅
Scaffolded via the `MeltFall ▸ Build Slice 1 (Grey-box)` menu item and verified live:
- Assets: Water/Sand/LoopTuning/Level_01 created + wired (all asset refs resolve).
- Scene `Assets/Scenes/Meltfall_Play.unity`: camera+rig, light, ground, safe-zone volume,
  sand support (meltable), gem, liquid gun (auto-selects Water), level manager, input.
- Play-mode smoke test: LevelManager State=Surveying, TotalGems=1; LiquidGun
  CurrentLiquid=Water, CanFire=true; **0 MeltFall runtime errors**. Hold-to-melt the
  pillar drops the gem into the safe zone (win) — the core loop is live.
- Earlier "compiles clean" checks had actually hit a different open project (`throwit`);
  compilation + play are now genuinely verified against MeltItDown.

## Slices 2–5 — BUILT & smoke-tested ✅ (commit d0c1b79)
- **Slice 2 data:** Water/Acid/Solvent/Heat liquids + Sand/Metal/Stone/Ice materials (matching table §4).
- **HUD:** `Prefabs/HUD.prefab` (fuel gauge, gem tracker, liquid selector, aim indicator, result panel) + `Prefabs/LiquidButton.prefab` + runtime `SceneBootstrap` binder; EventSystem via new Input System. TMP Essential Resources imported.
- **Slice 3 — `Meltfall_Match.unity` (Proto 2):** 3 liquids [Water,Acid,Solvent]; 2-layer excavation (MetalShell→acid, StonePillar→solvent) → drop gem. Smoke: State=Surveying, TotalGems=1, defs wired, 0 errors.
- **Slice 4 — `Meltfall_Depth.unity` (Proto 3):** 4 liquids; 3 gems; safe + hazard kill-floor zones; 4 meltable layers (StonePillar_A, SandPillar_B, IceColumn_C, MetalBrace_C); star thresholds 1/2/3. Smoke: TotalGems=3, zones+meltables+gems present, 0 errors.
- **Editor builders:** menu `MeltFall/Build Everything` (+ per-slice items) regenerate assets/scenes.

### Hard-won gotchas (for future Editor automation)
- Load SO assets **after** `OpenScene` — opening a scene reimports assets and fake-nulls pre-loaded references (root cause of null wiring).
- Don't call `AssetDatabase.Refresh()` right before `LoadAssetAtPath` — the reimport makes the load return null mid-import.
- In the `execute_code` CodeDom (C# 6) sandbox: no lambdas/local-functions/generics reliably; use non-generic `GetComponent(typeof(T))` / `GetComponent("TypeName")`.
- New files need a full asset refresh (`scope=all`) to import before they compile into the assembly.

## Remaining (future features, not this slice)
Playtest for feel; run greenoke's cold QA gate + build_verdict across slices; then juice (VFX/dissolve shader/audio/slow-mo), world map/meta, monetization.


## Code — DONE (compiles clean)
Full C# layer under `Assets/MeltFall/` (namespace `MeltFall` / `MeltFall.UI`):
- **Assembly:** `MeltFall.asmdef` (refs Unity.InputSystem, Unity.TextMeshPro).
- **Data SOs:** LiquidDefinition, MaterialDefinition, LoopTuningConfig, LevelDefinition.
- **Runtime:** Enums, FuelTank, MeltableMaterial, LiquidGun (cone-cast melt, no-alloc), Gem, SafeZone/HazardZone, LevelManager (play-state machine, events), StageInputController (Input System touch), StageCameraRig.
- **HUD (event-driven views):** FuelGaugeView, GemTrackerView, LiquidButton, LiquidSelectorView, AimIndicatorView, LevelResultView.

Commits: `build: … core runtime + data layer` and `build: … HUD view layer`.

## Editor-side authoring — PENDING (yours, in-Editor)
The code is data-driven; these assets are authored in the Unity Editor and can't be created from the sandbox:
- ScriptableObject instances: Water/Acid/Solvent/Heat liquids, Sand/Metal/Stone/Ice materials, a LoopTuning asset, one LevelDefinition per slice.
- `Dissolve.shadergraph` (URP alpha-clip) + per-material dissolve materials.
- Prefabs: MeltablePiece, Gem, LiquidGun, HUD canvas (safe-area).
- Scene `Meltfall_Play.unity`: camera rig, light, HUD, LevelManager, gun; author a grey-box structure for Slice 1.
- Input: add a "Stage" action map (FireHold, PointerPosition) to `InputSystem_Actions.inputactions`.
- Bootstrap wiring: resolve liquid ids → LiquidDefinitions and Bind() the HUD views to LevelManager/FuelTank/LiquidGun.
- EditMode/PlayMode test assemblies + tests (deferred to when logic is wired).

## Reconciliation follow-ups (minor, from the HUD pass)
1. `LevelManager.MarkLost` does not fire `GemsChanged` — the tracker refreshes on `StateChanged` instead (works; optionally fire it from MarkLost for instant lost-gem updates).
2. No per-gem status event — tracker fills slots from counters (tally correct, not per-instance identity).
3. `FuelTank` has no `FuelLevel` accessor — the gauge derives Normal/Low/Empty from `Fraction` + a threshold.
4. `LevelManager` has only `RetryLevel()` — result view exposes a `ContinueRequested` event for a future level-flow layer.
5. Bootstrap must resolve `LevelDefinition.AvailableLiquidIds` → `LiquidDefinition` objects before binding the selector (no id→SO registry yet).

## .meta note
The sandbox mount doesn't surface Unity-generated `.meta` files; they exist on the Mac. In Fork, commit the `.meta` alongside these scripts (stable GUIDs), then push.
