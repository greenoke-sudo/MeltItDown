# Feature Spec: MELTFALL core gameplay loop (vertical slice)

## 1. Overview

MELTFALL is a portrait mobile puzzle-destruction game where the player holds a liquid gun that dissolves materials. Layered structures hold treasure gems aloft; the player picks the liquid that matches a material, holds and sweeps a continuous stream to melt the supports, and gravity drops the gems. A gem that falls a real distance and settles in a safe zone is won; one that comes to rest in a hazard kill-floor is lost. One shared fuel tank feeds every liquid and cannot refill mid-level, so the puzzle is choosing what to melt, in what order, with which liquid, before the fuel runs dry. This vertical slice delivers the complete moment-to-moment loop — survey, spray, collapse, drop, resolve, score — for a casual mobile audience.

## 2. Goals

- Deliver the full core loop end to end: survey a structure, select a liquid, hold-and-sweep to melt matched supports, collapse the tower, drop the gems, resolve the level, and award stars.
- Make matching feel guessable-in-hindsight: each material has exactly one correct liquid that dissolves it fast; every other liquid does near-nothing and still burns fuel.
- Make the shared fuel tank the tension and the clock: a hard budget that drains only while spraying, punishes wrong-liquid choices economically, and ends the level when empty.
- Make collapse and falls read as intentional and fair, so a landed gem feels earned and a lost gem feels like the player's own mis-aim, not physics noise.
- Make where a gem lands matter, not just that it fell: hazard kill-floors under part of the structure force the player to topple the tower toward the safe side, excavating inward through layered materials to reach buried gems.
- Present a satisfying, tactile spray and dissolve on a fixed 3/4 diorama stage that runs smoothly one-handed in portrait on mid-range phones.

## 3. Non-Goals

- No world map, level progression, star gates, or liquid-unlock pacing across levels.
- No special materials (armored, reactive, unstable, chain) — wrong liquid in this slice does near-zero and never triggers a reaction.
- No boss stages, gun upgrades, or meta progression.
- No monetization hooks (ads, in-app purchases), cosmetics, or collection layer.
- No in-stage fuel top-ups or pickups — the tank is a strict hard budget.
- No real fluid, voxel, or fracture simulation — the melt is a cheap dissolve-and-shrink effect, not a physical fluid model.

## 4. User Journey

The player opens a level and sees a contained diorama stage in portrait: a tall structure of layered materials with one to three gems held aloft inside or atop it, sitting over a mix of safe ground and hazard kill-floors. A fuel gauge sits at the top, a gem tracker beside it, and a row of liquid buttons runs along the bottom. The player surveys the scene — reading which materials form which supports, where the gems rest, and which side is safe — before touching anything.

The player taps a liquid to make it active, then touches and holds a point on the structure. A continuous stream fires toward that point and tracks the finger as it sweeps, so the player can paint across a target. When the active liquid matches the material under the stream, that piece dissolves quickly: it visibly shrinks and stops blocking before it fully vanishes. When the liquid is wrong, the piece barely changes yet the fuel still drains — the punishment is automatic and needs no warning popup. To dig through a layered shell, the player switches liquids and excavates inward layer by layer; each switch costs a short purge delay during which nothing sprays, so the player plans an attack order rather than toggling frantically.

As supports melt away, the structure above loses its footing and collapses under gravity. Gems tumble free and fall down the screen. A gem that falls far enough and settles at rest on safe ground counts as landed and fills its slot in the gem tracker with a satisfying landing beat; a gem that comes to rest in a hazard kill-floor is lost. Because hazards sit under one side, the player must melt the supports so the tower topples toward the safe side.

The level plays against the fuel clock. If the player is efficient, fuel is left to spare; if sloppy, the tank runs dry. The level ends when every gem is resolved — landed or lost — or when the tank empties. The player then sees a result: stars equal to the gems safely landed (one, two, or three), or a failed result if the tank emptied with zero gems landed. From the result the player can retry the level or move on.

## 5. Surface Catalog

| Surface | Role | States |
|---|---|---|
| Stage | The play field — the diorama where the player surveys, sprays, melts, and watches gems fall | 5 |
| Fuel gauge | Top-of-screen readout of remaining shared fuel | 3 |
| Gem tracker | Top-of-screen readout of each gem's resolution status | 3 |
| Liquid selector | Bottom row of liquid buttons and active-liquid indication | 3 |
| Aim indicator | Faint impact marker showing where the stream lands | 2 |
| Level result | End-of-level outcome with stars or failure and next actions | 2 |

## 6. State Specifications

### 6.1 Stage — Surveying

- **Reference:** design reference "Core loop" survey step and "Controls, camera, UI" (fixed 3/4 diorama, portrait); brief "Presentation"
- **Entry condition:** The level has loaded and no stream is currently firing.
- **Visible elements:** The full diorama structure with its layered materials, all gems held aloft, safe zones, and hazard kill-floors; the fuel gauge, gem tracker, liquid selector, and aim indicator overlays.
- **Hidden elements:** —
- **Available actions:** Pan the view slightly (parallax), select a liquid, begin a touch-and-hold to fire.
- **Data displayed:** Current structure layout, gem positions, remaining fuel, per-gem status, and the active liquid.
- **Acceptance Criteria (EARS):**
  - THE game SHALL present the stage as a fixed 3/4 front view of the contained diorama in portrait with no free rotation.
  - WHILE surveying THE game SHALL keep every material piece, gem, safe zone, and hazard kill-floor visible and at rest.
  - WHEN the player drags without holding a firing touch on the structure THE game SHALL allow only a slight parallax pan and SHALL NOT rotate the stage.

### 6.2 Stage — Spraying

- **Reference:** design reference "Aim + hold", "Melt", and "shared fuel tank"; brief "Liquid gun" and "Shared fuel tank"
- **Entry condition:** The player touches and holds a point on the stage while a liquid is active and no purge delay is in progress and fuel remains.
- **Visible elements:** The active liquid's stream flowing from the gun toward the touched point, the impact of the stream on the target, dissolving material shrinking under a matched stream, the fuel gauge draining, and the aim indicator at the landing point.
- **Hidden elements:** —
- **Available actions:** Sweep the touch to move the stream across targets, release to stop spraying, switch to a different liquid.
- **Data displayed:** Remaining fuel draining live, the active liquid, and the visible melt progress on the targeted piece.
- **Acceptance Criteria (EARS):**
  - WHILE the player holds a firing touch THE game SHALL fire a continuous stream toward the touched point and SHALL move the stream to track the finger as it sweeps.
  - WHILE spraying a matched material THE game SHALL drain that piece's integrity and SHALL shrink the piece as its integrity falls.
  - IF the active liquid does not match the sprayed material THEN THE game SHALL apply near-zero melt to that piece and SHALL still drain fuel at the active liquid's burn rate.
  - WHILE spraying THE game SHALL reduce the shared fuel at the active liquid's per-liquid burn rate for as long as the stream fires.
  - WHEN the player releases the firing touch THE game SHALL stop the stream and SHALL stop draining fuel.

### 6.3 Stage — Purge delay

- **Reference:** design reference "Swap delay / purge"; brief "switching liquids triggers a short purge delay during which nothing sprays"
- **Entry condition:** The player selects a different liquid than the currently active one.
- **Visible elements:** The newly selected liquid shown as active-but-loading, no stream in flight, and the structure at rest apart from any collapse already in motion.
- **Hidden elements:** The stream is hidden for the duration of the purge.
- **Available actions:** Wait for the purge to finish, survey, or pan the view.
- **Data displayed:** The newly selected liquid, the purge progress, and remaining fuel.
- **Acceptance Criteria (EARS):**
  - WHEN the player switches to a different liquid THE game SHALL begin a short purge delay during which no stream sprays.
  - WHILE the purge delay is in progress THE game SHALL block firing and SHALL NOT drain fuel.
  - WHEN the purge delay completes THE game SHALL allow the newly selected liquid to fire on the next firing touch.

### 6.4 Stage — Collapsing-resolving

- **Reference:** design reference "Collapse", "Drop", "craft rules: melting & physics readability"; brief "Melting + collapse", "Directional collapse", "Goal gems"
- **Entry condition:** A support has fully melted and the structure it held has lost support, or one or more gems are in free fall or settling.
- **Visible elements:** Unsupported pieces and gems falling under gravity, melted pieces cleared out of the way so they do not catch falling gems, and gems settling onto safe ground or into hazard kill-floors.
- **Hidden elements:** Fully melted pieces are removed from view after they have shrunk and stopped blocking.
- **Available actions:** Continue spraying other supports, switch liquids, survey the falling result.
- **Data displayed:** The live physical outcome of the collapse, remaining fuel, and per-gem status as each gem resolves.
- **Acceptance Criteria (EARS):**
  - WHEN a piece's integrity reaches empty THE game SHALL shrink the piece and stop it blocking before removing it so it cannot catch a falling gem.
  - WHEN a piece is removed THE game SHALL let whatever it supported fall under gravity.
  - WHEN a gem falls at least the minimum win fall distance and comes to rest in a safe zone THE game SHALL mark that gem as landed.
  - IF a gem comes to rest in a hazard kill-floor THEN THE game SHALL mark that gem as lost and SHALL NOT count it.
  - WHEN a gem lands safely THE game SHALL play a landing emphasis beat and SHALL fill that gem's slot in the gem tracker.

### 6.5 Stage — Resolved

- **Reference:** design reference "Resolve" and "goal gems — win, fail, stars"; brief "Resolve, stars, fail"
- **Entry condition:** Every gem has been resolved as landed or lost, or the shared fuel tank has emptied.
- **Visible elements:** The final settled state of the diorama with all falls complete and the level result about to appear.
- **Hidden elements:** Firing and liquid switching are no longer available.
- **Available actions:** None on the stage; control passes to the level result.
- **Data displayed:** The final count of gems landed and lost and the remaining fuel at the moment of resolution.
- **Acceptance Criteria (EARS):**
  - WHEN every gem is resolved as landed or lost THE game SHALL end the play phase and SHALL present the level result.
  - WHEN the shared fuel tank empties THE game SHALL end the play phase after any in-flight falls settle and SHALL present the level result.
  - WHILE the level is resolved THE game SHALL disable firing and liquid switching.

### 6.6 Fuel gauge — Normal

- **Reference:** design reference "shared fuel tank" and "Controls, camera, UI"; brief "live fuel gauge (top, flashes when low)"
- **Entry condition:** The level is in play and remaining fuel is above the low-fuel threshold.
- **Visible elements:** A fuel bar at the top of the screen showing the remaining shared fuel level.
- **Hidden elements:** —
- **Available actions:** None directly; the gauge is a readout.
- **Data displayed:** Remaining shared fuel as a proportion of the starting fuel.
- **Acceptance Criteria (EARS):**
  - WHILE the level is in play THE game SHALL show remaining shared fuel on the gauge and SHALL update it live as fuel drains.
  - WHILE remaining fuel is above the low-fuel threshold THE game SHALL show the gauge in its normal appearance.

### 6.7 Fuel gauge — Low

- **Reference:** design reference "Fuel gauge ... Flashes when low"; brief "flashes when low"
- **Entry condition:** Remaining fuel has fallen to or below the low-fuel threshold and is above empty.
- **Visible elements:** The fuel bar at a low level, flashing to draw attention.
- **Hidden elements:** —
- **Available actions:** None directly; the gauge is a readout.
- **Data displayed:** The remaining shared fuel, now in a low-warning presentation.
- **Acceptance Criteria (EARS):**
  - WHEN remaining fuel falls to or below the low-fuel threshold THE game SHALL flash the fuel gauge to warn the player.
  - WHILE fuel is low THE game SHALL keep the gauge flashing until fuel rises above the threshold or reaches empty.

### 6.8 Fuel gauge — Empty

- **Reference:** design reference "No mid-level refill" and "Fail"; brief "Shared fuel tank", "Fail"
- **Entry condition:** Remaining fuel has reached empty.
- **Visible elements:** The fuel bar shown as fully drained.
- **Hidden elements:** —
- **Available actions:** None; spraying is no longer possible.
- **Data displayed:** An empty fuel reading.
- **Acceptance Criteria (EARS):**
  - WHEN remaining fuel reaches empty THE game SHALL show the gauge as fully drained.
  - WHILE fuel is empty THE game SHALL prevent any further spraying regardless of the active liquid.

### 6.9 Gem tracker — Pending

- **Reference:** design reference "Gem tracker"; brief "gem tracker (top)"
- **Entry condition:** The level has loaded and at least one gem is not yet resolved.
- **Visible elements:** One slot per gem in the level, each unresolved slot shown as pending.
- **Hidden elements:** —
- **Available actions:** None directly; the tracker is a readout.
- **Data displayed:** The count of gems in the level and which remain unresolved.
- **Acceptance Criteria (EARS):**
  - THE game SHALL show one tracker slot for each gem in the level.
  - WHILE a gem is unresolved THE game SHALL show its slot as pending.

### 6.10 Gem tracker — Landed

- **Reference:** design reference "Stars = gems safely landed"; brief "gem tracker (top)", "Stars = gems safely landed"
- **Entry condition:** A gem has fallen the minimum distance and settled at rest in a safe zone.
- **Visible elements:** The landed gem's slot filled to mark a safe landing, alongside any other slots in their own states.
- **Hidden elements:** —
- **Available actions:** None directly; the tracker is a readout.
- **Data displayed:** The number of gems safely landed so far.
- **Acceptance Criteria (EARS):**
  - WHEN a gem is marked landed THE game SHALL fill that gem's tracker slot as a safe landing.
  - THE game SHALL keep a landed slot filled for the remainder of the level.

### 6.11 Gem tracker — Lost

- **Reference:** design reference "A gem that lands there is lost, not counted"; brief "A gem that comes to rest in a hazard kill-floor is lost, not counted"
- **Entry condition:** A gem has come to rest in a hazard kill-floor.
- **Visible elements:** The lost gem's slot marked as lost, distinct from pending and landed.
- **Hidden elements:** —
- **Available actions:** None directly; the tracker is a readout.
- **Data displayed:** Which gems have been lost.
- **Acceptance Criteria (EARS):**
  - WHEN a gem is marked lost THE game SHALL mark that gem's tracker slot as lost.
  - THE game SHALL NOT count a lost gem toward stars.

### 6.12 Liquid selector — Idle

- **Reference:** design reference "Liquid selector"; brief "liquid selector (bottom)"
- **Entry condition:** The level is in play and no liquid switch is currently purging.
- **Visible elements:** A bottom row of buttons for the level's available liquids, with the active liquid highlighted.
- **Hidden elements:** —
- **Available actions:** Tap a different liquid to make it active.
- **Data displayed:** The available liquids and which one is active.
- **Acceptance Criteria (EARS):**
  - WHILE the level is in play THE game SHALL show a bottom row of buttons for the level's available liquids and SHALL highlight the active liquid.
  - WHEN the player taps a liquid that is not active THE game SHALL make it the active liquid.
  - WHEN the player taps a liquid that is already active THE game SHALL leave the active liquid unchanged and SHALL NOT begin a purge delay.

### 6.13 Liquid selector — Purging

- **Reference:** design reference "Swap delay / purge"; brief "switching liquids triggers a short purge delay"
- **Entry condition:** The player has just tapped a different liquid and the purge delay is running.
- **Visible elements:** The newly selected liquid highlighted as active but shown as still loading for the purge duration.
- **Hidden elements:** —
- **Available actions:** Wait for the purge to finish, or tap yet another liquid.
- **Data displayed:** The newly selected liquid and the purge progress.
- **Acceptance Criteria (EARS):**
  - WHILE a purge delay is in progress THE game SHALL show the newly selected liquid as active-but-loading.
  - IF the player taps a further different liquid during a purge THEN THE game SHALL restart the purge delay for the newest selection.

### 6.14 Liquid selector — Active-firing

- **Reference:** design reference "Aim + hold" and "Liquid selector"; brief "Liquid gun"
- **Entry condition:** A liquid is active, the purge delay has completed, and the player is holding a firing touch.
- **Visible elements:** The active liquid highlighted while its stream fires from the gun on the stage.
- **Hidden elements:** —
- **Available actions:** Release to stop firing, or tap a different liquid to switch (which begins a purge).
- **Data displayed:** The active, firing liquid.
- **Acceptance Criteria (EARS):**
  - WHILE the active liquid is firing THE game SHALL keep that liquid highlighted in the selector.
  - WHEN the player taps a different liquid while firing THE game SHALL stop the current stream and SHALL begin a purge delay for the new selection.

### 6.15 Aim indicator — Hidden

- **Reference:** design reference "Optional aim assist ... a faint impact indicator"; brief "Optional aim assist"
- **Entry condition:** No firing touch is in progress.
- **Visible elements:** —
- **Hidden elements:** The impact marker is not shown while the player is not firing.
- **Available actions:** Begin a firing touch to reveal the indicator.
- **Data displayed:** None while hidden.
- **Acceptance Criteria (EARS):**
  - WHILE no firing touch is in progress THE game SHALL hide the impact indicator.

### 6.16 Aim indicator — Showing

- **Reference:** design reference "a faint trajectory/impact indicator showing where the stream lands"; brief "a faint impact indicator showing where the stream lands"
- **Entry condition:** The player is holding a firing touch and the stream is landing on the stage.
- **Visible elements:** A faint marker at the point where the stream lands, moving with the finger as it sweeps.
- **Hidden elements:** —
- **Available actions:** Sweep the touch to move the indicator, release to hide it.
- **Data displayed:** The current landing point of the stream.
- **Acceptance Criteria (EARS):**
  - WHILE a firing touch is in progress THE game SHALL show a faint impact marker at the point where the stream lands.
  - WHILE the finger sweeps THE game SHALL move the impact marker to follow the stream's landing point.

### 6.17 Level result — Cleared

- **Reference:** design reference "goal gems — win, fail, stars" and "Sell the moment"; brief "Resolve, stars, fail"
- **Entry condition:** The level has resolved with at least one gem safely landed.
- **Visible elements:** A result panel showing the earned stars (one, two, or three), the count of gems landed, and actions to retry or continue.
- **Hidden elements:** The stage controls (firing, liquid selector) are no longer active.
- **Available actions:** Retry the level or continue past it.
- **Data displayed:** Stars earned and gems landed.
- **Acceptance Criteria (EARS):**
  - WHEN the level resolves with at least one gem landed THE game SHALL show a cleared result with stars equal to the number of gems safely landed.
  - THE game SHALL award one star for one gem landed, two stars for two, and three stars for three.
  - WHEN the cleared result is shown THE game SHALL offer to retry the level or continue.

### 6.18 Level result — Failed

- **Reference:** design reference "Fail = the tank empties ... with ZERO gems landed"; brief "Fail = the tank empties with zero gems landed"
- **Entry condition:** The level has resolved with zero gems safely landed and the tank empty.
- **Visible elements:** A result panel showing the failure and an action to retry.
- **Hidden elements:** The stage controls are no longer active; no stars are shown.
- **Available actions:** Retry the level.
- **Data displayed:** The failed outcome with zero gems landed.
- **Acceptance Criteria (EARS):**
  - WHEN the level resolves with zero gems landed and the tank empty THE game SHALL show a failed result with no stars.
  - WHEN the failed result is shown THE game SHALL offer to retry the level.

## 7. Transitions

| From | To | Trigger | Effect |
|---|---|---|---|
| Stage.Surveying | Stage.Spraying | WHEN the player holds a firing touch with an active liquid and fuel remaining and no purge running | THE game SHALL fire the active liquid's stream toward the touched point. |
| Stage.Spraying | Stage.Surveying | WHEN the player releases the firing touch | THE game SHALL stop the stream and return the stage to survey. |
| Stage.Surveying | Stage.Purge delay | WHEN the player selects a different liquid | THE game SHALL begin a purge delay during which nothing sprays. |
| Stage.Spraying | Stage.Purge delay | WHEN the player selects a different liquid while firing | THE game SHALL stop the stream and begin a purge delay for the new liquid. |
| Stage.Purge delay | Stage.Surveying | WHEN the purge delay completes | THE game SHALL allow the newly selected liquid to fire on the next firing touch. |
| Stage.Spraying | Stage.Collapsing-resolving | WHEN a sprayed support's integrity reaches empty | THE game SHALL clear the piece and let the structure above it fall. |
| Stage.Collapsing-resolving | Stage.Surveying | WHEN falling pieces and gems have settled and gems remain unresolved and fuel remains | THE game SHALL return the stage to survey so the player can continue. |
| Stage.Collapsing-resolving | Stage.Resolved | WHEN every gem is resolved as landed or lost | THE game SHALL end the play phase. |
| Stage.Spraying | Stage.Resolved | WHEN the shared fuel tank empties | THE game SHALL stop spraying and, after in-flight falls settle, end the play phase. |
| Stage.Resolved | Level result.Cleared | IF at least one gem landed safely THEN | THE game SHALL present the cleared result with stars equal to gems landed. |
| Stage.Resolved | Level result.Failed | IF zero gems landed and the tank is empty THEN | THE game SHALL present the failed result with no stars. |
| Level result.Cleared | Stage.Surveying | WHEN the player chooses retry | THE game SHALL reset the level to its authored starting state and return to survey. |
| Level result.Failed | Stage.Surveying | WHEN the player chooses retry | THE game SHALL reset the level to its authored starting state and return to survey. |
| Fuel gauge.Normal | Fuel gauge.Low | WHEN remaining fuel falls to or below the low-fuel threshold | THE game SHALL flash the fuel gauge. |
| Fuel gauge.Low | Fuel gauge.Empty | WHEN remaining fuel reaches empty | THE game SHALL show the gauge fully drained and prevent further spraying. |
| Gem tracker.Pending | Gem tracker.Landed | WHEN a gem falls the minimum distance and settles in a safe zone | THE game SHALL fill that gem's tracker slot as landed. |
| Gem tracker.Pending | Gem tracker.Lost | WHEN a gem comes to rest in a hazard kill-floor | THE game SHALL mark that gem's tracker slot as lost. |
| Liquid selector.Idle | Liquid selector.Purging | WHEN the player taps a liquid that is not active | THE game SHALL make it active and begin a purge delay. |
| Liquid selector.Purging | Liquid selector.Idle | WHEN the purge delay completes and no firing touch is held | THE game SHALL show the selector idle with the new liquid ready. |
| Liquid selector.Idle | Liquid selector.Active-firing | WHEN the player holds a firing touch with the active liquid ready | THE game SHALL fire the active liquid and highlight it. |
| Liquid selector.Active-firing | Liquid selector.Idle | WHEN the player releases the firing touch | THE game SHALL stop firing and return the selector to idle. |
| Aim indicator.Hidden | Aim indicator.Showing | WHEN the player begins a firing touch | THE game SHALL show a faint marker at the stream's landing point. |
| Aim indicator.Showing | Aim indicator.Hidden | WHEN the player releases the firing touch | THE game SHALL hide the impact marker. |

## 8. Business Rules

### Progression
- Within this slice a single level is the unit of play; there is no across-level progression, star gate, or unlock. WHEN a level ends THE game SHALL offer only to retry or continue, and cross-level progression is out of scope for this slice.
- Within a level, the player progresses by excavating inward through layered materials, switching liquids per layer to reach the gems.
- Stars within a level are determined solely by gems safely landed: one landed earns one star, two earn two, three earn three; zero landed is a failure.

### Economy
- One shared fuel tank feeds every liquid; there are no separate per-liquid tanks. Fuel drains only while a stream is firing, at the active liquid's per-liquid burn rate (cheaper liquids drain slower, more expensive liquids drain faster).
- A wrong-liquid choice is punished economically only: it melts near-nothing yet still drains fuel at the active liquid's burn rate. There is no reactive hazard or explosion in this slice.
- The tank is a hard budget: fuel does not regenerate, and there are no in-stage top-ups or pickups. A level is authored to be beatable on its starting fuel when played efficiently, leaving fuel to spare on a clean line.

### Timing
- Firing is continuous while the player holds a touch and stops the instant the touch is released. Fuel drains only during firing.
- Switching liquids incurs a short purge delay during which nothing sprays and no fuel drains; selecting yet another liquid mid-purge restarts the delay for the newest selection.
- A gem is judged only after it has settled at rest; a fully melted piece shrinks and clears before it fully vanishes so it cannot catch a falling gem.
- Collapse and falls play out under gravity in near-deterministic physics so the same plan produces the same outcome; the level resolves once all gems have settled or the tank has emptied and any in-flight falls settle.

### Competition
- N/A — this slice is single-player with no leaderboards, multiplayer, or head-to-head competition.

### Gating
- A gem counts as won only if it clears the full win guard: it must fall at least the minimum win fall distance and come to rest in a safe zone. A gem resting in a hazard kill-floor is lost and never counts.
- The level result is gated on the resolution outcome: a cleared result requires at least one gem safely landed; a failed result requires zero gems landed with the tank empty.
- Firing is gated: it is blocked while a purge delay runs, while fuel is empty, and while the level is resolved.

## 9. Edge Cases & Defaults

### Interruptions
- If the app loses focus or is backgrounded mid-level (incoming call, notification, app switch), the game pauses the level in place — the stream stops, the fuel drain halts, and physics freezes — and resumes from the same state when the player returns.
- If the player lifts the firing touch unexpectedly (finger slips off the stage or off-screen), the stream stops immediately as if released, and no fuel drains until firing resumes; any partly melted piece keeps the integrity it has already lost and does not regenerate.
- If the player taps a liquid button that is already active, nothing changes and no purge delay is triggered.
- If a second touch begins while one firing touch is already held, the game ignores the extra touch and keeps tracking the original firing touch until it is released.

### State Recovery
- If the level is paused and resumed, the game restores the exact remaining fuel, the active liquid, any in-progress purge, per-piece melt progress, and per-gem status so play continues seamlessly.
- If the player chooses to retry a level, the game resets the structure, gems, fuel, active liquid, and gem tracker to the level's authored starting state, and returns the view to its default framing with parallax pan re-enabled.
- The game does not persist a partially played level across app restarts in this slice; a fresh launch begins the selected level from its authored starting state.

### Boundary Conditions
- If a gem falls but comes to rest without having travelled the minimum win fall distance, the game does not count it as landed even if it rests on safe ground; it remains pending until it either settles safely after a real fall or comes to rest in a hazard.
- If a gem settles exactly on the border between a safe zone and a hazard kill-floor, the game counts it safe only if its resting point is within the safe zone; otherwise it is lost.
- If a gem ends the level neither settled in a safe zone nor in a hazard (for example, wedged in place when the tank empties), the game treats that gem as lost for scoring purposes at resolution.
- If a level is authored with the maximum of three gems and all three land safely, the game awards three stars; the game never awards more than three stars or fewer than one on a cleared result.
- If the tank empties at the exact moment the last needed gem lands safely, the game counts that gem as landed and resolves the level as cleared.

### Error States
- If the player holds a firing touch while fuel is empty, the game shows no stream and drains no fuel; firing simply does nothing until the level resolves.
- If the player holds a firing touch during an active purge delay, the game shows no stream and waits for the purge to complete before any firing can occur.
- If a melted piece would otherwise linger and block a falling gem, the game clears the piece (shrinks it and stops it blocking) before removal so it cannot catch or wedge a gem, keeping collapses fair.
- If the stream reaches a piece that has already been fully melted and cleared, the strike has no effect and no fuel is charged for the piece that is already gone.
- If a gem's fall stalls on debris and cannot resolve, the game biases the gem to fall clean (reduced snagging) so drops read as intentional; a gem still unresolved at level end is treated as lost.
- If the level cannot present its structure, gems, safe zone, or hazard kill-floor, the game does not begin play and shows a neutral unavailable notice with a retry option rather than an unplayable stage.

## A1. Player-facing behavior

- The player sees a fixed 3/4 front view of a sealed diorama stage in portrait, with a slight parallax pan and no free rotation. A tall layered structure holds one to three gems aloft over a mix of safe ground and hazard kill-floors.
- The player selects a liquid from a bottom row, then touches and holds the stage to fire a continuous stream that tracks the finger as it sweeps; releasing stops the stream. A faint impact marker shows where the stream lands.
- Matched liquid on a material dissolves it fast — the piece visibly shrinks and clears before vanishing — while a wrong liquid barely dents it yet still drains fuel, so the mismatch is felt through the fuel bar rather than a popup.
- The fuel gauge at the top drains live while spraying and flashes when low; the gem tracker at the top fills a slot each time a gem lands safely and marks a slot lost when a gem falls into a hazard.
- Collapses and falls are juiced to read as intentional: melted supports clear out of the way, gems fall clean, and a safe landing gets an emphasis beat (slow-mo pinch, camera nudge, and a sound sting) as the payoff moment.
- Switching liquids triggers a brief purge where nothing sprays, nudging the player to plan an attack order. The level ends with a stars result when gems are resolved or a failure result when the tank empties with nothing landed.

## A2. Tunables surfaced to the Inspector (no hardcoding)

- Per-liquid values: display color, stream and impact effects, per-liquid burn rate (fuel per second), melt strength on the matched material, and which material family each liquid correctly melts.
- Per-material values: starting integrity, the one correct liquid, dissolve color and dissolve appearance parameters, and the near-zero wrong-liquid response.
- Per-level values: starting fuel amount, which liquids are available, gem count and gem starting positions, safe-zone and hazard-kill-floor placements, and the stars-per-gems-landed mapping.
- Loop timing values: the purge delay duration, the low-fuel warning threshold, and the minimum win fall distance and settle-at-rest thresholds for the win guard.
- Feel and juice values: stream cone width and reach, gem fall-guidance and angular-drag bias, camera framing and parallax pan amount, and the landing emphasis parameters (slow-mo amount, camera nudge, and audio sting).

## A3. Platform / mobile constraints

- Portrait mobile-first with one-handed touch play: all controls (liquid selector at the bottom, fuel gauge and gem tracker at the top) sit within thumb reach and outside the device safe-area insets.
- Touch input drives everything: a single hold-and-drag fires and aims; the game tracks one firing touch at a time and ignores extra simultaneous touches, and the stream stops cleanly on release or on a lost touch.
- The stage and HUD must remain readable across common phone aspect ratios without clipping the diorama or overlapping notches and rounded corners.
- The melt is a cheap dissolve-and-shrink effect, not a fluid or fracture simulation, and stream visuals are cosmetic only, so the loop holds a smooth frame rate on mid-range phones under continuous spraying and multi-piece collapse.
- Physics runs on a fixed timestep with authored masses so collapses stay near-deterministic and fair while staying within the mobile performance budget.

## 10. Open Questions

## 11. Glossary

| Term | Definition |
|---|---|
| Liquid | One of the four attack fluids the gun can fire — Water, Acid, Solvent, or Heat — each of which correctly melts exactly one material family. |
| Material | A dissolvable substance a structure piece is made of — soft earth, metal, stone, or frozen/waxy — each melted fast by only its one matching liquid. |
| Integrity | A material piece's remaining resistance to melting; matched liquid drains it, and when it reaches empty the piece clears and vanishes. |
| Support | A structure piece that holds weight above it; melting a support lets whatever it held fall under gravity. |
| Collapse | The falling of unsupported structure and gems under gravity after their supports melt away. |
| Directional collapse | Melting supports on a chosen side so the tower topples toward the safe side and away from hazards. |
| Excavate inward | Melting through the layered shells of a structure, switching liquids per layer, to reach a buried gem. |
| Gem | A goal treasure item held aloft in a level; never melted directly, it is won by dropping it into a safe zone. |
| Safe zone | Ground or a platform where a gem coming to rest, after a real fall, counts as safely landed. |
| Hazard kill-floor | A dangerous area (spikes, pit, pool, or void) under part of the structure where a gem coming to rest is lost, not counted. |
| Win guard | The full requirement for a gem to count: it must fall at least the minimum win fall distance and settle at rest in a safe zone. |
| Minimum win fall distance | The least distance a gem must fall before settling for its landing to count, so a gem resting low cannot win on a trivial drop. |
| Purge delay | A short interval after switching liquids during which nothing sprays and no fuel drains, forcing the player to plan an attack order. |
| Fuel tank | The single shared reserve that feeds every liquid; it drains only while spraying, never refills mid-level, and running dry ends the level. |
| Burn rate | The fuel a liquid consumes per second while firing; cheaper liquids drain slowly and expensive liquids drain fast. |
| Star | A score mark awarded per gem safely landed — one, two, or three — shown on a cleared level result. |
| Diorama stage | The contained, sealed play field shown in a fixed 3/4 front view where the whole level takes place. |
| Fuel gauge | The top-of-screen readout of remaining shared fuel that drains live and flashes when low. |
| Gem tracker | The top-of-screen readout showing one slot per gem, filling on a safe landing and marking lost on a hazard rest. |
| Liquid selector | The bottom row of buttons for choosing the active liquid, with the active one highlighted. |
| Aim indicator | The faint impact marker showing where the current stream lands, following the finger as it sweeps. |
| Layered obstacle | A structure built in nested shells of different materials that the player excavates inward, switching liquids per layer to reach the gems. |
