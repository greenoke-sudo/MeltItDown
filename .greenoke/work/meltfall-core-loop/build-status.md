# Build status — meltfall-core-loop

Build driven in Cowork with a **console-error gate** (per user: no live provider verify()/screenshots; in-Editor testing done manually later). Unity 6000.0.62f1 bridge connected; every pass compiled with **0 errors, 0 warnings**.

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
