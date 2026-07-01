# Project rules — MeltItDown

MeltItDown is a **Unity 6 (6000.0.62f1) mobile game**, portrait-first. It is early-stage:
`Assets/` currently holds the Input System actions, Scenes, and Settings — the core
systems are still to be built. These rules auto-load for greenoke agents
(codebase-researcher, spec/plan writers, builder, qa). They start deliberately lean;
extend them as the project's stack and conventions solidify.

## Stack

- **Engine:** Unity 6 (6000.0.62f1), mobile-first (portrait). Universal Render Pipeline settings live under `Assets/Settings/`.
- **Input:** Unity Input System (`Assets/InputSystem_Actions.inputactions`) — not the legacy `Input` manager.
- **Async:** prefer async over blocking the main thread; keep I/O-bound work off the frame loop.
- **UI:** Canvas + RectTransform + TextMeshPro.
- (Fill in the chosen tween, save/persistence, and inspector-tooling libraries here once decided — until then, agents must ask rather than assume.)

## No hardcoded tunables

- Every tunable (colors/palette, sizes, animation durations + curves, audio, haptics,
  gameplay constants, balance values) lives in a ScriptableObject config asset, edited
  from the Inspector — never baked into C#.
- A feature that introduces a new tunable adds (or extends) a config asset; it never
  hardcodes the value in a script.

## Mobile / performance

- **No allocations in `Update`** or other per-frame code (no per-frame `new`, LINQ,
  boxing, or string concatenation in hot loops).
- **No `Resources.Load`** in per-frame / hot paths.
- **No synchronous network / disk I/O** on the main thread — keep it async.
- Respect portrait mobile constraints (safe areas, variable aspect ratios, mid-range
  device budgets).

## Where to look

- `Assets/Scenes/` — scenes.
- `Assets/Settings/` — render pipeline / project settings assets.
- `Assets/` — game code and assets land here as the project grows (add an assembly
  definition for game code and a matching EditMode/PlayMode test assembly).

## Conventions

- Keep game code in its own assembly definition (`.asmdef`) with a companion test
  assembly so greenoke's `verify()` / build-smoke can run EditMode tests.
- New forbidden libraries or hard constraints go under a `## Forbidden` heading here as
  they are decided, so the spec/plan verifier can enforce them.
