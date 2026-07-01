# MELTFALL — Game Design & Technical Spec
*Working title. Mobile puzzle-destruction game. Unity. Captured 2026-07-01 as the foundational design reference for MeltItDown.*

---

## 1. One-line pitch
You hold a liquid gun that **melts** materials. Structures hold gems aloft; melt the right supports with the right liquid and gravity drops the gems. A gem that lands safely is a win. Your shared fuel tank is the clock.

**Elevator:** *PowerWash Simulator's satisfying spray, but you're dissolving a layered fortress to drop treasure — and you only have so much fuel, so the puzzle is what to melt, in what order, with which liquid.*

---

## 2. Core fantasy & feel
The player should feel two things at once:

- **Satisfaction** — the tactile, almost ASMR pleasure of watching solid material dissolve into nothing under a stream. This is the "toy" the player comes back for.
- **Cleverness** — the "aha" of reading a structure and realizing you don't melt the *whole* tower, you melt the *one* pillar that drops all three gems at once. This is the "game" that gives it depth.

The failure state we are designing *away* from: a fidget toy where you mindlessly dissolve everything and nice VFX play. The fuel tank and the fall are what stop that.

---

## 3. Core loop (moment-to-moment)
1. **Survey** the structure: what materials, where the supports are, where the gems sit, where the hazards are.
2. **Select** a liquid matched to the material you want to attack.
3. **Aim + hold** to fire a continuous stream; sweep it across the target.
4. **Melt** — matched liquid dissolves that material fast; wrong liquid does almost nothing *and still burns fuel*.
5. **Collapse** — a melted support can no longer hold weight, so the structure above it falls.
6. **Drop** — gems fall. A gem that lands in a safe zone counts; one that lands in a hazard is lost.
7. **Resolve** — level ends when all gems are resolved (landed or lost) OR the tank runs dry.
8. **Score** — stars awarded by gems safely landed (see §6).

The whole game is: **spray the right liquid → dissolve the right support → drop the gem → land it safe → before the fuel runs out.**

---

## 4. System: liquids & materials (the matching puzzle)
Every material has exactly **one** correct liquid. Correct = fast dissolve. Wrong = negligible effect + wasted fuel. This turns each structure into a set of little locks, and liquids into keys.

### Core matching table (launch set)
| Liquid | Melts | Intuition | Burn rate (fuel/sec) |
|---|---|---|---|
| **Water** | Sand, dirt, clay | Washes soft earth away | Low (cheap) |
| **Acid** | Metal, steel, iron | Corrodes metal | High (expensive) |
| **Solvent** | Stone, concrete, rock | Dissolves masonry | Medium |
| **Heat / Molten** | Ice, wax, resin, snow | Melts anything frozen or waxy | High (expensive) |

Design rules for matchups:
- **Guessable, not memorized.** Every pairing should feel obvious in hindsight so getting it right feels smart. Avoid stretches (e.g. we deliberately do *not* use "lava melts stone," since lava *is* molten stone — solvent-on-stone reads cleaner).
- **One correct key per material.** No material accepts two liquids (keeps the puzzle honest). Exceptions are deliberate "special materials" (§8).
- **Wrong liquid = soft fail, not hard.** It chips almost nothing and drains fuel — the punishment is economic, automatic, and needs no separate warning UI.

---

## 5. System: the shared fuel tank (the economy = the clock)
This is the spine of the game. **One shared tank feeds every liquid.** You do not carry separate tanks per liquid.

Why one tank (this is the load-bearing design decision):
- It **merges the puzzle and the economy into one system.** Spraying acid where water would've worked isn't just slow — it burns shared, expensive fuel on the wrong job. Wrong choices get punished automatically by the economy. No separate resource bars to track.
- It's **clean on mobile:** one fuel bar top of screen, a row of liquid buttons at the bottom. Separate tanks would clutter screen and brain.

Rules:
- **No mid-level refill.** The tank empties and does not come back (except designed pickups or a rewarded-ad top-up — see §10). Running dry before you've dropped the gems is the entire tension.
- **Per-liquid burn rate.** Water is cheap, acid/heat are expensive (see table). Rationing the expensive liquids is a core skill.
- **Swap delay / purge.** Switching liquid mid-stream triggers a short reload (~0.4–0.6s) where nothing sprays. This stops spam-switching and forces you to plan an attack *order*, not toggle frantically.
- Levels are authored to be **beatable on the tank if played efficiently.** A perfect line leaves fuel to spare (that's your 3-star flair); a sloppy line runs dry.

---

## 6. System: goal gems — win, fail, stars
- Each level has **1–3 gems** (goal items) held aloft inside/atop the structure. You never melt the gems — you melt what holds them.
- **Win (per gem):** the gem falls, settles, and comes to rest in a **safe zone** (the ground / a safe platform). "Touched the ground" is refined to **"fell a real distance and settled safely"** — see the guard below.
- **Stars = gems safely landed.** 3 landed = ★★★, 2 = ★★, 1 = ★. (Optional polish: a "perfect" flourish if you also finish with fuel to spare.)
- **Fail = the tank empties (or timer ends) with ZERO gems landed.** Even one gem landed is a pass.

### The "touched the ground" guard (important)
If a gem wins just by *touching* ground, a gem resting low wins with a single drip, and easy/hard levels feel identical. Fix:
- Gems must **start above a minimum height and fall a real distance** to count, OR land in a designated target zone.
- **Hazard kill-floors** (spikes / lava pit / acid pool / void) sit under some gems. A gem that lands *there* is **lost, not counted**. Now *where* it falls matters, not just *that* it fell — which forces directional collapse (melt the supports so the tower topples toward the safe side).

---

## 7. Craft rules: melting & physics readability
Real physics will occasionally wedge a gem sideways or balance it on debris, and the player will feel cheated ("it fell — why didn't I win?"). Satisfying collapse beats realistic collapse, every time:

- **Melted material must clear out of the way.** As a piece dissolves it shrinks and disables its collider *before* it's fully gone, so it can't pile up and catch a falling gem.
- **Bias gems to fall clean.** Slightly reduced angular drag / gentle guidance so gems don't snag on ledges. Tune until falls read as intentional.
- **Keep it near-deterministic.** Fixed timestep, authored masses/centers-of-mass, constrained randomness — so the same plan produces the same result and puzzles stay fair.
- **Sell the moment.** Slow-mo pinch + camera nudge + sound sting when a gem lands safe. This is the payoff beat; juice it.

---

## 8. Content variety (so it never goes stale)
Same core, escalating levers:

- **Layered obstacles (the primary depth lever).** Structures built in shells: e.g. steel skin → stone pillar → sand core. You *excavate inward* — acid the steel, switch to solvent for the stone, water for the core — to reach and drop the gem. "Big obstacle needs different liquids" becomes a satisfying dig.
- **Multiple gems** with conflicting solutions (the melt that drops gem A endangers gem B).
- **Hazard kill-floors + forced collapse direction** (§6).
- **Special materials** (break the one-key rule on purpose):
  - *Armored* — needs two liquids in sequence (acid to expose, then solvent).
  - *Reactive* — spraying the WRONG liquid triggers an explosion/hazard (punishes mismatches hard; teaches caution).
  - *Unstable* — melts on its own timer once touched; race against it.
  - *Chain* — melting one piece triggers neighbors to give way (dominoes from one good shot).
- **Boss stages** — a giant multi-layer fortress with several gems, tight fuel, and hazards on multiple sides.
- **Gun upgrades** — wider spray cone, higher pressure (faster melt), bigger tank capacity. Meta progression that also gates harder content.

---

## 9. Controls, camera, UI
**Camera:** fixed **3/4 front view** of a contained diorama stage (a sealed "tank"/shadow-box). Slight parallax pan allowed; **no free rotation** — keeps it readable and one-thumb friendly. The 3/4 downward tilt is deliberate: it reads structure depth *and* the vertical fall of the gems.

**Orientation:** **Portrait** (default). The stage is a tall vertical tower; gems fall down the screen; HUD top + bottom; one-handed play. (See open decisions — landscape is viable for wide fortresses.)

**Controls (mobile):**
- **Liquid selector:** row of buttons along the bottom; tap to set the active liquid. Selected liquid is highlighted; locked liquids show as unlocked-later.
- **Aim + fire:** touch-and-hold on the stage; the stream fires toward the touch point and **tracks your finger** as you drag to sweep. Release to stop.
- **Fuel gauge:** top of screen, color-coded, drains live. Flashes when low.
- **Gem tracker:** top — icons for each gem, filling as they land safe.

**Optional aim assist for casual audience:** a faint trajectory/impact indicator showing where the stream lands.

---

## 10. Progression & meta
- **Level-based world map.** Levels grouped into **worlds**, each world introducing one new material + its matched liquid (paces the teaching).
- **Liquid unlocks pace content:** start with Water only → unlock Acid → Solvent → Heat, each unlock opening new material puzzles.
- **Difficulty curve** (same mechanic, infinite ceiling):
  - *Early:* 1 gem, 1 material, generous fuel, no hazards.
  - *Mid:* 2 gems, 2–3 materials, matching matters, tighter fuel.
  - *Late:* 3 gems, layered towers, hazards + forced collapse direction, expensive liquids, very tight fuel.
- **Star gates:** need X total stars to unlock the next world (light gating; encourages replay for 3-star).

---

## 11. Monetization (default: F2P casual — tunable)
- **Rewarded ads:** top up fuel to save a near-win, reveal a hint (which support to melt), skip a hard level.
- **Interstitials:** between levels (standard casual cadence).
- **IAP:** remove ads; gun cosmetic skins; liquid VFX skins; permanent tank-capacity / pressure upgrades.
- **Collection layer (optional):** unlock/collect cosmetic gun & liquid effects for retention.

If you'd rather go **premium** (one-time paid, no ads), the level design is identical — you'd just drop the ad hooks and lean harder on a long crafted campaign.

---

## 12. Technical architecture (Unity)
Target: **Unity + URP, mobile-optimized.** Design is **ScriptableObject-driven** so you can author materials, liquids, and levels as data without touching code — ideal for a small/solo team.

### 12.1 Data (ScriptableObjects / presets)
```
LiquidDefinition (SO)
  id, displayName, color
  burnRate            // fuel per second while spraying
  meltPower           // integrity removed per tick on matched material
  matchedMaterialIds  // which materials this correctly melts
  streamVFX, sfx
MaterialDefinition (SO)
  id, type
  maxIntegrity        // "HP" of a piece of this material
  correctLiquidId
  dissolveColor, dissolveShaderParams
  onWrongLiquid       // enum: Ignore | Chip | React(hazard)
LevelDefinition (SO / scene)
  structurePrefab / scene
  startingFuel
  availableLiquids[]
  gemCount, gemSpawns[]
  hazardZones[], safeZones[]
  starThresholds      // gems landed → stars
```

### 12.2 Core components
**`MeltableMaterial`** (on every dissolvable piece; requires `Rigidbody` + `Collider`)
- Fields: `MaterialDefinition def`, `float integrity` (starts at `def.maxIntegrity`).
- `ApplyMelt(LiquidDefinition liquid, float amount)`:
  - If `liquid.matchedMaterialIds` contains `def.id`: `integrity -= amount * liquid.meltPower;` drive dissolve shader (`_Cutoff = 1 - integrity/max`).
  - Else: apply `def.onWrongLiquid` behavior (usually near-zero).
  - When `integrity <= 0`: **shrink + disable collider first**, then remove — so it can't catch falling gems. Pieces it was supporting are now unsupported and fall via normal physics.

**`LiquidGun`**
- Holds `currentLiquid`. On hold-fire, each `FixedUpdate`:
  - Cast a short **cone/spherecast** from the nozzle along the aim direction (a few rays or one `OverlapSphere`); for each `MeltableMaterial` hit in range → `ApplyMelt(currentLiquid, meltPerTick)`.
  - `FuelTank.Consume(currentLiquid.burnRate * Time.fixedDeltaTime)`.
- **Particles are purely cosmetic** — never drive melting off per-particle collisions (too expensive). The raycast cone is the source of truth.
- Swapping liquid starts a purge timer that blocks firing briefly.

**`FuelTank`**
- `float current, max; Consume(amount); bool IsEmpty;` no auto-refill (pickups/ads only).

**`Gem`** (requires `Rigidbody`)
- On collision, checks the surface tag + settle velocity:
  - Safe zone + settled → `LevelManager.MarkLanded(this)`.
  - Hazard zone → `LevelManager.MarkLost(this)`.

**`LevelManager`**
- Tracks `total / landed / lost`.
- **Win check:** all gems resolved OR fuel empty.
- **Stars:** `landedCount` mapped through `starThresholds`.
- **Fail:** fuel empty AND `landed == 0`.
- Triggers end screen, slow-mo on gem landings, star tally.

### 12.3 Melting visual — recommended approach
Start with the **cheap fake, not a real fluid/voxel sim**:
- **Dissolve shader** (alpha-clip threshold rising with lost integrity) + **mesh scale-down** as it melts. Reads as "melting" at almost no cost.
- Skip runtime Voronoi fracture and true fluid — you don't need them and they'll wreck mobile perf. (If you later want chunkier melting, a pre-chunked mesh where the stream removes chunks is the next step up — but prove the game first.)

### 12.4 Determinism / fairness
- Fixed timestep; author masses and centers-of-mass on structure pieces.
- Constrain gem physics (reduced angular drag, optional slight fall guidance) so drops read clean.
- Same plan → same outcome. Puzzles must be fair to be satisfying.

---

## 13. MVP / prototype milestones
Prove the fun before building content pipelines.

**Proto 1 — "does the core feel good?" (grey-box)**
One liquid (water), one material (sand), one gem, hold-to-spray, a support that melts, gem falls, win detection, a fuel bar. Nothing else. If melting a support and watching the gem drop isn't satisfying here, stop and fix it.

**Proto 2 — "is the matching puzzle fun?"**
Add a second liquid + material + the matching rule + swap delay + differing burn rates + the shared tank. Prove that *choosing the right liquid to conserve fuel* creates real decisions.

**Proto 3 — "does it have depth?"**
Add a layered obstacle (excavate inward), a hazard kill-floor, and 3 gems with a star rating. Prove directional collapse and the "drop all three on half a tank" skill ceiling.

**Then:** SO-driven content pipeline → world map / meta → juice pass (VFX, sound, slow-mo, camera) → monetization hooks.

---

## 14. Open decisions to confirm
Strong default calls were made on these — flip any as needed:

1. **Orientation** — defaulted to **portrait** (tall towers, one-handed). Landscape suits wide multi-side fortresses.
2. **Theme / fiction** — defaulted to **gem excavation** (free trapped treasure from layered mineral/industrial obstacles). Swappable skin — could be sabotage, sci-fi, cute/cartoon, abstract.
3. **Business model** — defaulted to **F2P casual** (ads + IAP). Premium campaign is a one-flag change.
4. **Wrong-liquid behavior** — defaulted to **near-zero effect + wasted fuel**. Optionally some materials *react* (explode/hazard) on the wrong liquid.
5. **Fuel top-ups** — should any levels have **in-stage fuel pickups**, or is the tank always a hard budget?

---
*End of spec.*
