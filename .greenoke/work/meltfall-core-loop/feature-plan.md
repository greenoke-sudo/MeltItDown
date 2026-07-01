# Feature Plan: MELTFALL core gameplay loop (vertical slice)

Reference: spec at `.greenoke/work/meltfall-core-loop/feature-spec.md`. Authoritative architecture: `.greenoke/research/2026-07-01-meltfall-design.md` §12.

## 1. Overview

This plan implements the spec's full survey → spray → collapse → drop → resolve → score loop as a greenfield Unity 6 / URP portrait mobile slice. The strategy follows design §12: all tunables live in ScriptableObject definitions (`LiquidDefinition`, `MaterialDefinition`, `LevelDefinition`, `LoopTuningConfig`), the melt is a raycast/overlap-cone source of truth (`LiquidGun` on FixedUpdate) driving a dissolve shader plus mesh shrink (`MeltableMaterial`), and 3D Rigidbody pieces collapse under a fixed timestep with a `LevelManager` state machine arbitrating play states, fuel, gems, and result. All game code lives in a new `MeltFall.asmdef` with companion EditMode and PlayMode test assemblies.

## 2. Structure & File Layout

Assemblies:
- `Assets/MeltFall/MeltFall.asmdef` — runtime game assembly (references Unity Input System, TextMeshPro, URP).
- `Assets/MeltFall/Tests/EditMode/MeltFall.Tests.asmdef` — EditMode test assembly (references MeltFall + nunit + Unity TestRunner).
- `Assets/MeltFall/Tests/PlayMode/MeltFall.PlayTests.asmdef` — PlayMode test assembly for runtime/scene behavior.

Config / data (ScriptableObjects, design §12.1):
- `Assets/MeltFall/Scripts/Data/LiquidDefinition.cs` — SO: per-liquid tunables (color, burn rate, melt power, matched materials, VFX/SFX refs).
- `Assets/MeltFall/Scripts/Data/MaterialDefinition.cs` — SO: per-material tunables (integrity, correct liquid, dissolve params, wrong-liquid response).
- `Assets/MeltFall/Scripts/Data/LevelDefinition.cs` — SO: per-level tunables (starting fuel, available liquids, gem spawns, safe/hazard zones, star thresholds, win-guard distances).
- `Assets/MeltFall/Scripts/Data/LoopTuningConfig.cs` — SO: shared loop timing + feel tunables (purge delay, low-fuel threshold, min-win-fall-distance, settle thresholds, cone width/reach, gem fall-guidance, camera/parallax, landing emphasis).
- `Assets/MeltFall/Data/Liquids/*.asset` — Water, Acid, Solvent, Heat instances.
- `Assets/MeltFall/Data/Materials/*.asset` — Sand/dirt, Metal, Stone, Ice/wax instances.
- `Assets/MeltFall/Data/Levels/*.asset` — one authored level per vertical slice.
- `Assets/MeltFall/Data/LoopTuning.asset` — the shared tuning instance.

Runtime scripts (design §12.2):
- `Assets/MeltFall/Scripts/Runtime/MeltableMaterial.cs` — dissolvable piece; integrity, `ApplyMelt`, shrink+disable-collider-before-removal.
- `Assets/MeltFall/Scripts/Runtime/LiquidGun.cs` — hold-fire cone cast on FixedUpdate; consumes fuel; owns active liquid + purge timer.
- `Assets/MeltFall/Scripts/Runtime/FuelTank.cs` — shared fuel value; `Consume`, `IsEmpty`, `IsLow`, `ResetToStart`, change/emptied events.
- `Assets/MeltFall/Scripts/Runtime/Gem.cs` — Rigidbody goal item; tracks fall distance, settle, zone at rest.
- `Assets/MeltFall/Scripts/Runtime/LevelManager.cs` — play-state machine, gem tally, resolution, stars, retry/reset.
- `Assets/MeltFall/Scripts/Runtime/StageInputController.cs` — Input System touch → aim ray + hold-fire; single-touch tracking.
- `Assets/MeltFall/Scripts/Runtime/StageCameraRig.cs` — fixed 3/4 portrait camera + parallax pan clamp.
- `Assets/MeltFall/Scripts/Runtime/Zones/SafeZone.cs`, `HazardZone.cs` — trigger volumes tagging resting outcome.
- `Assets/MeltFall/Scripts/Runtime/Enums.cs` — `PlayState`, `WrongLiquidResponse`, `GemStatus`, `LiquidSelectorState`, `AimIndicatorState`, `FuelLevel`.

HUD scripts:
- `Assets/MeltFall/Scripts/UI/FuelGaugeView.cs`, `GemTrackerView.cs`, `LiquidSelectorView.cs`, `LiquidButton.cs`, `AimIndicatorView.cs`, `LevelResultView.cs`.

Shader / material:
- `Assets/MeltFall/Shaders/Dissolve.shadergraph` — URP alpha-clip dissolve (`_Cutoff`, `_DissolveColor`).
- `Assets/MeltFall/Materials/*.mat` — per-material-family dissolve materials.

Prefabs:
- `Assets/MeltFall/Prefabs/MeltablePiece.prefab` — Rigidbody + Collider + MeltableMaterial + dissolve material.
- `Assets/MeltFall/Prefabs/Gem.prefab` — Rigidbody + Collider + Gem.
- `Assets/MeltFall/Prefabs/LiquidGun.prefab` — nozzle + LiquidGun + cosmetic ParticleSystem.
- `Assets/MeltFall/Prefabs/HUD.prefab` — safe-area-aware Canvas with fuel gauge, gem tracker, liquid selector, aim indicator, result panel.

Scene:
- `Assets/Scenes/Meltfall_Play.unity` — play scene: camera rig, directional light, HUD, LevelManager, gun; loads the active LevelDefinition.

Input:
- `Assets/InputSystem_Actions.inputactions` — add/extend a "Stage" action map (FireHold, PointerPosition).

## 3. Surface Decisions

| Surface | Decision (Reuse / Modify / New) | Component | Rationale |
|---|---|---|---|
| Stage | New | `LevelManager` (play-state machine) + `Meltfall_Play.unity` scene + `StageCameraRig` | reuse.md verdict: greenfield, no play scene or state driver exists; state ownership per design §12.2. |
| Fuel gauge | New | `FuelGaugeView` bound to `FuelTank` | reuse.md: no HUD exists; readout of the shared fuel value (design §5). |
| Gem tracker | New | `GemTrackerView` (one slot per gem) driven by `LevelManager` | reuse.md: new HUD readout; slot count from LevelDefinition gem count. |
| Liquid selector | New | `LiquidSelectorView` + `LiquidButton` row | reuse.md: new HUD button row over the level's available liquids (design §9). |
| Aim indicator | New | `AimIndicatorView` driven by the aim raycast hit point | reuse.md: new faint impact marker from the gun aim ray (design §9 aim assist). |
| Level result | New | `LevelResultView` overlay (stars / failed + retry/continue) | reuse.md: new end-of-level overlay; result gated by LevelManager resolution. |

## 4. State Machine Implementation

Play states (`Stage`, `Level result`) are owned by `LevelManager`; `Fuel gauge` states by `FuelGaugeView`+`FuelTank`; `Gem tracker` by `Gem`+`LevelManager`; `Liquid selector` by `LiquidGun`+`LiquidSelectorView`; `Aim indicator` by `StageInputController`+`AimIndicatorView`.

| From | To | Trigger (impl) | Effect (impl) | Owner |
|---|---|---|---|---|
| Stage.Surveying | Stage.Spraying | `StageInputController` FireHold performed with `LiquidGun.CanFire` (active liquid, `!purging`, `!FuelTank.IsEmpty`) | `LiquidGun.BeginFire(aimRay)`; `LevelManager.SetState(Spraying)` | LevelManager |
| Stage.Spraying | Stage.Surveying | FireHold canceled (touch release) | `LiquidGun.StopFire()`; `LevelManager.SetState(Surveying)` | LevelManager |
| Stage.Surveying | Stage.Purge delay | `LiquidSelectorView.OnLiquidTapped(other)` → `LiquidGun.SelectLiquid` | start `purgeTimer = LoopTuning.purgeDelay`; block firing | LevelManager |
| Stage.Spraying | Stage.Purge delay | `OnLiquidTapped(other)` while firing | `LiquidGun.StopFire()`; start `purgeTimer`; block firing | LevelManager |
| Stage.Purge delay | Stage.Surveying | `purgeTimer` elapsed in `LiquidGun.Tick` | clear purge; mark new liquid ready; `SetState(Surveying)` | LevelManager |
| Stage.Spraying | Stage.Collapsing-resolving | `MeltableMaterial.OnIntegrityEmptied` event | `MeltableMaterial.ClearOut()` (shrink+disable collider→remove); dependents fall; `SetState(CollapsingResolving)` | LevelManager |
| Stage.Collapsing-resolving | Stage.Surveying | `LevelManager.OnBodiesSettled()` with unresolved gems and `!FuelTank.IsEmpty` | `SetState(Surveying)` to allow continued play | LevelManager |
| Stage.Collapsing-resolving | Stage.Resolved | `LevelManager.AllGemsResolved()` after settle | `SetState(Resolved)`; end play phase | LevelManager |
| Stage.Spraying | Stage.Resolved | `FuelTank.IsEmpty` while firing, after in-flight settle | `LiquidGun.StopFire()`; await settle; `SetState(Resolved)` | LevelManager |
| Stage.Resolved | Level result.Cleared | `LevelManager.Resolve()` with `landedCount >= 1` | compute stars via `LevelDefinition.starThresholds`; show `LevelResultView.ShowCleared(stars)` | LevelManager |
| Stage.Resolved | Level result.Failed | `LevelManager.Resolve()` with `landedCount == 0 && FuelTank.IsEmpty` | `LevelResultView.ShowFailed()` | LevelManager |
| Level result.Cleared | Stage.Surveying | `LevelResultView.OnRetry` | `LevelManager.ResetToAuthored()`; `SetState(Surveying)` | LevelManager |
| Level result.Failed | Stage.Surveying | `LevelResultView.OnRetry` | `LevelManager.ResetToAuthored()`; `SetState(Surveying)` | LevelManager |
| Fuel gauge.Normal | Fuel gauge.Low | `FuelTank.Fraction <= LoopTuning.lowFuelThreshold` (via `FuelTank.Changed` in `FuelGaugeView`) | start flash tween; `SetFuelLevel(Low)` | FuelGaugeView |
| Fuel gauge.Low | Fuel gauge.Empty | `FuelTank.IsEmpty` (via `FuelTank.Emptied`) | render fully drained; stop flash; firing blocked via `LiquidGun.CanFire` | FuelGaugeView |
| Gem tracker.Pending | Gem tracker.Landed | `Gem.ResolveSafe()` (fell ≥ min distance + settled in SafeZone) → `LevelManager.MarkLanded` | fill slot as safe landing; play landing emphasis beat | LevelManager |
| Gem tracker.Pending | Gem tracker.Lost | `Gem.ResolveLost()` (settled in HazardZone) → `LevelManager.MarkLost` | mark slot lost; exclude from stars | LevelManager |
| Liquid selector.Idle | Liquid selector.Purging | `LiquidButton.onClick` on non-active liquid | `LiquidGun.SelectLiquid`; `LiquidSelectorView.SetState(Purging)` | LiquidSelectorView |
| Liquid selector.Purging | Liquid selector.Idle | `LiquidGun.OnPurgeComplete` with no firing touch held | `LiquidSelectorView.SetState(Idle)`; new liquid highlighted ready | LiquidSelectorView |
| Liquid selector.Idle | Liquid selector.Active-firing | FireHold performed with liquid ready | highlight active liquid; `SetState(ActiveFiring)` | LiquidSelectorView |
| Liquid selector.Active-firing | Liquid selector.Idle | FireHold canceled (release) | `SetState(Idle)` | LiquidSelectorView |
| Aim indicator.Hidden | Aim indicator.Showing | FireHold performed → `StageInputController` begins aim ray | `AimIndicatorView.Show(hitPoint)`; `SetState(Showing)` | AimIndicatorView |
| Aim indicator.Showing | Aim indicator.Hidden | FireHold canceled (release) | `AimIndicatorView.Hide()`; `SetState(Hidden)` | AimIndicatorView |

## 5. Data Structures

### LiquidDefinition (ScriptableObject) — `Data/Liquids/*.asset`
- `string id` — stable liquid identifier (Water, Acid, Solvent, Heat).
- `string displayName` — selector label.
- `Color color` — button + stream tint.
- `float burnRate` — fuel per second consumed while firing (spec §8 Economy; cheaper < expensive).
- `float meltPower` — integrity removed per melt tick on a matched material.
- `List<string> matchedMaterialIds` — the material family this liquid correctly melts (design §4 table; one key per material).
- `GameObject streamVFX` / `AudioClip sfx` — cosmetic-only references (design §12.2: never drive melt).

### MaterialDefinition (ScriptableObject) — `Data/Materials/*.asset`
- `string id`, `string type` — family identifier.
- `float maxIntegrity` — starting integrity of a piece (spec §8 timing; integrity glossary).
- `string correctLiquidId` — the one liquid that dissolves it fast.
- `Color dissolveColor` / `DissolveParams dissolveShaderParams` — appearance while melting.
- `WrongLiquidResponse onWrongLiquid` — enum `Ignore | Chip`; this slice authors near-zero responses only (qa.md, spec §3).
- `float chipFraction` — near-zero integrity fraction removed on a wrong-liquid hit (default 0; keeps the near-zero response data-driven, never hardcoded).

### LevelDefinition (ScriptableObject) — `Data/Levels/*.asset`
- `GameObject structurePrefab` — the authored diorama root.
- `float startingFuel` — hard budget for the shared tank (spec §8 Economy; no refill).
- `List<LiquidDefinition> availableLiquids` — selector contents.
- `int gemCount` + `List<Transform>/Vector3[] gemSpawns` — 1–3 gems and starting positions.
- `List<Bounds>/List<Transform> safeZones`, `hazardZones` — zone placements.
- `int[] starThresholds` — gems-landed → stars mapping (1→★,2→★★,3→★★★; spec §8 Gating).

### LoopTuningConfig (ScriptableObject) — `Data/LoopTuning.asset`
- `float purgeDelay` — swap purge duration (design §5, ~0.4–0.6s, tunable).
- `float lowFuelThreshold` — fraction at which the gauge flashes.
- `float minWinFallDistance` — least fall distance for a win (spec §8 Gating win guard).
- `float settleLinearVelocity`, `settleAngularVelocity`, `settleTime` — at-rest thresholds.
- `float coneWidthDegrees`, `coneReach` — melt cone geometry (design §12.2).
- `float gemAngularDrag`, `gemFallGuidance` — clean-fall bias (design §7/§12.4).
- `float cameraTilt`, `parallaxPanAmount` — 3/4 portrait framing.
- `float slowMoScale`, `slowMoDuration`, `cameraNudge`; `AudioClip landingSting` — landing emphasis.

### FuelTank (runtime, non-persistent) — `Runtime/FuelTank.cs`
- `float current`, `float max` — remaining and starting fuel (max from `LevelDefinition.startingFuel`).
- `bool IsEmpty => current <= 0`; `bool IsLow(threshold)`; `float Fraction => current/max`.
- `void Consume(float amount)` — decrement, clamp at 0; no auto-refill.
- `void ResetToStart()` — restore `current = max` on retry.
- `event Action Changed`, `event Action Emptied` — fired on drain and on reaching empty so `FuelGaugeView` binds without per-frame polling of the tank.

### GemState (runtime, per gem) — field on `Gem.cs` reported to `LevelManager`
- `GemStatus status` — `Pending | Landed | Lost`.
- `float startHeight`, `float fallDistance` — track fall for the win guard.
- `int trackerSlotIndex` — bound HUD slot.
- Resolution recorded in `LevelManager` counters `total / landed / lost` (non-persistent; reset on retry).

## 6. Surface Specifications

### 6.1 Stage — `LevelManager` (states: Surveying, Spraying, Purge delay, Collapsing-resolving, Resolved)
- Source reference: spec §6.1–§6.5; design §3 core loop, §12.2 components. Destination: `Meltfall_Play.unity` + `LevelManager` play-state machine driving the gun, physics, and HUD.
- **Surveying** — full diorama at rest, all HUD overlays visible; actions: parallax pan, select liquid, begin fire; no stream firing.
- **Spraying** — active stream toward touch, matched piece shrinking, fuel draining, aim indicator shown; release stops.
- **Purge delay** — newly selected liquid active-but-loading, stream hidden, firing blocked, no fuel drain.
- **Collapsing-resolving** — unsupported pieces/gems fall, melted pieces cleared before removal, gems settle into safe/hazard zones; per-gem status updates.
- **Resolved** — final settled diorama; firing and liquid switching disabled; control passes to Level result.
- State-visibility: stream + aim indicator visible only in Spraying; dissolve shrink visible in Spraying/Collapsing-resolving; falling bodies in Collapsing-resolving; all HUD overlays visible Surveying→Collapsing-resolving; interaction disabled in Resolved.

### 6.2 Fuel gauge — `FuelGaugeView` (states: Normal, Low, Empty)
- Source reference: spec §6.6–§6.8; design §5, §9. Destination: `HUD.prefab` top bar bound to `FuelTank`.
- **Normal** — bar above low-fuel threshold, standard appearance; readout only.
- **Low** — bar at/below threshold and above empty, flashing to warn.
- **Empty** — bar fully drained; spraying prevented regardless of active liquid.
- State-visibility: flash tween active only in Low; drained fill only in Empty; live fraction shown in all three.

### 6.3 Gem tracker — `GemTrackerView` (states: Pending, Landed, Lost)
- Source reference: spec §6.9–§6.11; design §6, §9. Destination: `HUD.prefab` top bar, one slot per `LevelDefinition.gemCount`.
- **Pending** — unresolved slot shown pending.
- **Landed** — slot filled as safe landing, stays filled for the level.
- **Lost** — slot marked lost, distinct from pending/landed, not counted toward stars.
- State-visibility: fill icon in Landed; lost mark in Lost; empty/pending glyph otherwise; one slot rendered per gem.

### 6.4 Liquid selector — `LiquidSelectorView` (states: Idle, Purging, Active-firing)
- Source reference: spec §6.12–§6.14; design §5 purge, §9 selector. Destination: `HUD.prefab` bottom row of `LiquidButton` over `LevelDefinition.availableLiquids`.
- **Idle** — button row with active liquid highlighted; tap non-active to switch, tapping the active one does nothing.
- **Purging** — newly selected liquid highlighted active-but-loading for the purge duration; re-tap restarts purge.
- **Active-firing** — active liquid highlighted while its stream fires; tap another liquid stops stream + begins purge.
- State-visibility: loading indicator only in Purging; firing highlight only in Active-firing; active highlight persists across all three.

### 6.5 Aim indicator — `AimIndicatorView` (states: Hidden, Showing)
- Source reference: spec §6.15–§6.16; design §9 aim assist. Destination: `HUD.prefab`/world marker driven by `StageInputController` aim-ray hit point.
- **Hidden** — no impact marker while no firing touch is in progress.
- **Showing** — faint marker at the stream's landing point, following the finger as it sweeps.
- State-visibility: marker rendered only in Showing; hidden otherwise.

### 6.6 Level result — `LevelResultView` (states: Cleared, Failed)
- Source reference: spec §6.17–§6.18; design §6 stars/fail. Destination: `HUD.prefab` result overlay driven by `LevelManager.Resolve()`.
- **Cleared** — panel with earned stars (1–3), gems-landed count, retry and continue actions; stage controls inactive.
- **Failed** — panel with failure and a retry action, no stars; stage controls inactive.
- State-visibility: star row + continue button in Cleared; failure message (no stars) in Failed; retry present in both.

## 7. Cross-System Integration

### Bootstrap & Registration
- `LevelManager.Awake` reads the active `LevelDefinition` + `LoopTuningConfig` (assigned in the Inspector, not `Resources.Load`), instantiates `structurePrefab`, spawns gems at `gemSpawns`, sets `FuelTank.max = startingFuel`, populates the `LiquidSelectorView` from `availableLiquids`, and sizes the `GemTrackerView` to `gemCount`.
- `LiquidGun`, `FuelTank`, HUD views, and `StageInputController` are wired via serialized references on the `Meltfall_Play` scene objects; `MeltableMaterial` pieces self-register their `OnIntegrityEmptied` handler with `LevelManager` on enable.

### Persistence
- No cross-restart persistence in this slice (spec §9 State Recovery). Runtime state (fuel, active liquid, purge, per-piece integrity, per-gem status) lives in-memory only; retry rebuilds from the authored `LevelDefinition`.

### ScriptableObject config touched
- Reads/creates `LiquidDefinition`, `MaterialDefinition`, `LevelDefinition`, `LoopTuningConfig` assets. Every new tunable (burn rate, melt power, chip fraction, purge delay, low-fuel threshold, min-win-fall distance, settle thresholds, cone geometry, gem drag, camera/parallax, landing emphasis) is authored on these assets — never hardcoded in C# (adapter rule).

### Scene / prefab impact
- Adds `Meltfall_Play.unity` (camera rig, directional light, HUD, LevelManager, gun). Adds prefabs `MeltablePiece`, `Gem`, `LiquidGun`, `HUD` (safe-area-aware Canvas so the selector/gauge/tracker stay within thumb reach and outside device insets, spec §A3). Inspector wiring: LevelManager ← LevelDefinition + LoopTuning + FuelTank + views; MeltablePiece ← MaterialDefinition + dissolve material; Gem ← Rigidbody constraints from tuning.

### Input (Unity Input System)
- Extends `Assets/InputSystem_Actions.inputactions` with a "Stage" action map: `FireHold` (touch press-and-hold, Button) and `PointerPosition` (Value/Vector2). `StageInputController` binds these, tracks a single firing touch and ignores extra simultaneous touches; selector/result buttons use uGUI.

### Animation / feedback
- Fuel gauge low-fuel flash, dissolve `_Cutoff` ramp + mesh shrink, and the landing emphasis beat (slow-mo pinch, camera nudge, audio sting) all read durations/curves/amounts from `LoopTuningConfig` and per-material `dissolveShaderParams` — no baked constants.

### Save / persistence
- None persisted in this slice (matches Bootstrap & Registration → Persistence above). A fresh launch begins the selected level from its authored starting state.

### EditMode / PlayMode test
- EditMode (`MeltFall.Tests`): pure logic — `FuelTank.Consume`/`IsEmpty`/`IsLow`/`ResetToStart`, matched-vs-wrong melt in `MeltableMaterial.ApplyMelt` (including `chipFraction` near-zero drain), star mapping via `starThresholds`, purge-restart timing, win-guard min-fall evaluation.
- PlayMode (`MeltFall.PlayTests`): runtime/scene — gun cone hit applies melt on FixedUpdate, piece clears collider before removal, gem fall→settle→landed/lost resolution, resolve→result transition.

### Capability-provider verbs used
- `verify` (compile + run EditMode/PlayMode tests), `build` (build-smoke of the play scene), `inspect` (confirm SO wiring / scene references), `screenshot` (HUD + stage visual checks per slice); health is always available.

## 8. Pattern Conformance Map

| Pattern | Reference (existing) | Key unit to study | Adaptation |
|---|---|---|---|
| SO-driven data (no hardcoded tunables) | design §12.1 (binding) | `LiquidDefinition` / `MaterialDefinition` / `LevelDefinition` / `LoopTuningConfig` | all balance + feel values authored in assets; C# reads them. |
| Raycast/overlap-cone melt (source of truth) | design §12.2 (binding) | `LiquidGun` FixedUpdate cone cast → `MeltableMaterial.ApplyMelt` | particles cosmetic only; melt evaluated per fixed step. |
| Dissolve shader + shrink | design §12.2/§12.3 (binding) | `Dissolve.shadergraph` `_Cutoff` + `MeltableMaterial` mesh scale-down | clear collider + shrink before removal so it can't catch a gem. |
| Deterministic physics | design §7/§12.4 (binding) | fixed timestep, authored mass/COM, constrained `Gem` | reduced angular drag + fall guidance for clean, repeatable drops. |
| Shared-fuel economy | design §5 (binding) | `FuelTank` single value + per-liquid `burnRate` + purge delay | one tank feeds all liquids; wrong liquid still burns fuel; no refill. |

## 9. Edge Case Implementation

### Interruptions
- App focus lost/backgrounded: `LevelManager.OnApplicationPause(true)` stops the stream, halts fuel drain, and sets `Time.timeScale = 0` / freezes Rigidbodies; resume restores from in-memory state.
- Unexpected touch lift (finger off stage/off-screen): `FireHold` cancel handler stops the stream immediately; no fuel drains until firing resumes; a partly melted piece keeps its lost integrity (never regenerates).
- Tap on already-active liquid: `LiquidGun.SelectLiquid` early-returns when the id matches; no state change, no purge.
- Second concurrent touch: `StageInputController` locks onto the first firing touch id and ignores others until it is released.

### State Recovery
- Pause/resume: no serialization; the in-memory `FuelTank.current`, `LiquidGun.currentLiquid`, active `purgeTimer`, per-`MeltableMaterial.integrity`, and per-`Gem.status` persist across the pause and continue seamlessly.
- Retry: `LevelManager.ResetToAuthored()` destroys spawned pieces/gems, re-instantiates from `LevelDefinition`, calls `FuelTank.ResetToStart()`, resets active liquid and the gem tracker, and re-centers the camera with parallax pan re-enabled.
- No cross-restart persistence: a fresh app launch starts the selected level from its authored state (spec §9; matches §7 Persistence).

### Boundary Conditions
- Gem settles without meeting `minWinFallDistance`: `Gem` leaves status `Pending` even on safe ground until it either falls a real distance and settles safe or comes to rest in a hazard.
- Gem on a safe/hazard border: resolution uses the resting-point containment test — counted safe only if the settle point is inside a `SafeZone`, else lost.
- Gem unresolved at level end (wedged when tank empties, neither zone): `LevelManager.Resolve()` treats it as lost for scoring.
- Three gems all landed: stars clamped through `starThresholds` to exactly 3; never more than 3 or fewer than 1 on a cleared result.
- Tank empties as the last needed gem lands: `MarkLanded` is processed before `Resolve()` checks fuel, so that gem counts and the level clears.

### Error States
- Fire held while fuel empty: `LiquidGun.CanFire` is false when `FuelTank.IsEmpty`; no stream shown, no fuel charged, firing no-ops until resolution.
- Fire held during purge: `CanFire` is false while `purgeTimer > 0`; no stream, firing waits for purge completion.
- Melted piece lingering: `MeltableMaterial.ClearOut()` shrinks + disables the collider before removal so a falling gem can't be caught or wedged.
- Stream hits an already-cleared piece: the cone cast returns no active `MeltableMaterial` for a removed piece; the strike has no effect and no fuel is charged for the absent piece.
- Gem fall stalls on debris: `Gem` uses the tuning's reduced angular drag / fall guidance to bias a clean fall; any gem still unresolved at level end is treated as lost.
- Level cannot present structure/gems/zones: `LevelManager` validates `LevelDefinition` on bootstrap; on missing pieces it does not begin play and shows a neutral unavailable notice with a retry option.

## 10. Vertical Slice Plan

### Slice 1: Core melt→drop→win grey-box (Proto 1)
- **Deliverable:** grey-box `Meltfall_Play` scene with Water liquid, one Sand material support, one gem, hold-to-spray cone melt, dissolve+shrink, a gem that falls and is detected as landed, and a working fuel bar.
- **Files touched:** `MeltFall.asmdef`; `Enums.cs`; `FuelTank.cs`; `MeltableMaterial.cs`; `LiquidGun.cs`; `Gem.cs`; `LevelManager.cs`; `StageInputController.cs`; `StageCameraRig.cs`; `Zones/SafeZone.cs`; `Dissolve.shadergraph`; `MeltablePiece.prefab`, `Gem.prefab`, `LiquidGun.prefab`; `Meltfall_Play.unity`; `FuelGaugeView.cs`; `InputSystem_Actions.inputactions` (Stage map).
- **Live-system surfaces involved:** verify, build, inspect, screenshot.
- **Dependencies:** none (first slice).
- **Definition of done:** proves §6.2 "WHILE holds a firing touch … fire a continuous stream … track the finger" and "WHILE spraying a matched material … drain integrity … shrink"; §6.4 "WHEN a piece's integrity reaches empty … shrink … stop it blocking before removing" and "WHEN a gem falls at least the minimum win fall distance and comes to rest in a safe zone … mark landed"; §6.6 fuel gauge updates live.

### Slice 2: Data / SO authoring layer
- **Deliverable:** `LiquidDefinition`, `MaterialDefinition`, `LevelDefinition`, `LoopTuningConfig` SO types + authored assets (Water/Acid/Solvent/Heat, Sand/Metal/Stone/Ice-wax, one LevelDefinition, LoopTuning); Slice 1 code refactored to read all tunables from these assets (nothing hardcoded).
- **Files touched:** `Data/LiquidDefinition.cs`, `Data/MaterialDefinition.cs`, `Data/LevelDefinition.cs`, `Data/LoopTuningConfig.cs`; `Data/Liquids/*.asset`, `Data/Materials/*.asset`, `Data/Levels/*.asset`, `Data/LoopTuning.asset`; refactors in `LiquidGun.cs`, `MeltableMaterial.cs`, `FuelTank.cs`, `LevelManager.cs`; `MeltFall.Tests.asmdef` + EditMode tests.
- **Live-system surfaces involved:** verify, inspect.
- **Dependencies:** Slice 1.
- **Definition of done:** proves §6.2 "IF the active liquid does not match … apply near-zero melt … still drain fuel" and "WHILE spraying … reduce shared fuel at the active liquid's per-liquid burn rate" driven by SO data; adapter rule (all tunables in config assets) enforced; EditMode tests green for melt, fuel, and star mapping.

### Slice 3: Matching puzzle — multiple liquids, shared tank, purge, burn rates (Proto 2)
- **Deliverable:** `LiquidSelectorView` + `LiquidButton` row, purge delay on switch, per-liquid burn rates against the one shared tank, matched-vs-wrong melt across multiple materials.
- **Files touched:** `LiquidSelectorView.cs`, `LiquidButton.cs`; purge logic in `LiquidGun.cs`; `LevelManager.cs` play-state transitions; `HUD.prefab` selector row; a two-liquid/two-material `LevelDefinition` asset; EditMode purge-restart test.
- **Live-system surfaces involved:** verify, inspect, screenshot.
- **Dependencies:** Slice 2.
- **Definition of done:** proves §6.3 purge ("begin a short purge delay … block firing … SHALL NOT drain fuel … allow firing on next touch"); §6.12–§6.14 selector (Idle tap-to-activate, tapping active does nothing, Purging active-but-loading with restart, Active-firing highlight); §6.2 shared-tank burn-rate drain.

### Slice 4: Depth — layered obstacles, hazards, directional collapse, 1–3 gems + stars (Proto 3)
- **Deliverable:** layered nested-shell structure (excavate inward), `HazardZone` kill-floors, directional collapse, 1–3 gems, `LevelResultView` with star tally / failed result, retry/continue.
- **Files touched:** `Zones/HazardZone.cs`; `Gem.cs` win-guard + hazard resolution; `LevelManager.cs` resolve/stars/retry; `LevelResultView.cs`; `GemTrackerView.cs`; layered `LevelDefinition` asset with hazard/safe zones + star thresholds; `HUD.prefab` result panel; PlayMode resolution tests.
- **Live-system surfaces involved:** verify, build, inspect, screenshot.
- **Dependencies:** Slice 3.
- **Definition of done:** proves §6.4 "IF a gem comes to rest in a hazard kill-floor … mark lost … SHALL NOT count"; §6.5 resolve ("WHEN every gem is resolved … end play phase … present the level result"; disable firing/switching); §6.9–§6.11 tracker Pending/Landed/Lost; §6.17 cleared with stars = gems landed and retry/continue; §6.18 failed with no stars and retry.

### Slice 5: HUD polish + feedback pass
- **Deliverable:** fuel-gauge low-fuel flash, aim indicator show/hide + finger tracking, landing emphasis beat (slow-mo, camera nudge, sting), parallax pan clamp — all driven by `LoopTuningConfig`.
- **Files touched:** `AimIndicatorView.cs`; flash logic in `FuelGaugeView.cs`; emphasis + slow-mo in `LevelManager.cs`; `StageCameraRig.cs` parallax; `LoopTuning.asset` feel values; `HUD.prefab`.
- **Live-system surfaces involved:** verify, screenshot.
- **Dependencies:** Slice 4.
- **Definition of done:** proves §6.7 fuel gauge low flash ("WHEN remaining fuel falls to or below the low-fuel threshold … flash"); §6.15–§6.16 aim indicator ("WHILE no firing touch … hide"; "WHILE a firing touch … show a faint impact marker … move to follow"); §6.4 landing emphasis beat on a safe landing.

## 11. Open Questions
