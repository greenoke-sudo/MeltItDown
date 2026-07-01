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

## Next: Slices 2–5
Matching puzzle (multi-liquid), SO-authoring polish, depth (layers/hazards/stars), HUD wiring.


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
