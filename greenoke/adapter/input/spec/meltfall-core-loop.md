# Feature brief — MELTFALL core gameplay loop (vertical slice)

**Scope (confirmed): the whole playable vertical slice** — the full moment-to-moment loop
through the design's Proto 1–3 milestones, as ONE coherent feature. This is the core game:
melt the right supports with the right liquid, drop the gems, land them safe, before the
shared fuel tank runs dry.

Full design reference: `.greenoke/research/2026-07-01-meltfall-design.md` (authoritative).
This brief names the slice's in-scope behavior; the design doc holds the rationale.

## In scope for this slice
- **Liquid gun:** touch-and-hold to fire a continuous stream at the touched point; the
  stream tracks the finger as it sweeps; release to stop.
- **Liquids (matching keys):** Water, Acid, Solvent, Heat — selectable from a bottom row.
  Each melts exactly one material family fast; any other liquid does near-nothing but still
  burns fuel.
- **Materials (locks):** soft earth (sand/dirt/clay ← Water), metal (← Acid), stone
  (← Solvent), frozen/waxy (← Heat). One correct liquid each.
- **Melting + collapse:** matched liquid drains a piece's integrity; a fully-melted piece
  clears out of the way (shrinks, stops blocking) before it vanishes; whatever it supported
  loses support and falls under gravity.
- **Layered obstacles:** structures built in shells the player excavates inward, switching
  liquids per layer to reach the gems.
- **Shared fuel tank:** ONE tank feeds all liquids; per-liquid burn rate (Water cheap;
  Acid/Heat expensive); no mid-level refill; switching liquids triggers a short purge delay
  during which nothing sprays.
- **Goal gems (1–3 per level):** held aloft; never melted directly. A gem wins only if it
  falls a real minimum distance AND settles at rest in a safe zone. A gem that comes to rest
  in a hazard kill-floor is lost, not counted.
- **Directional collapse:** hazards under part of the structure force the player to topple
  the tower toward the safe side.
- **Resolve, stars, fail:** the level ends when every gem is resolved (landed or lost) OR
  the tank empties. Stars = gems safely landed (1→★, 2→★★, 3→★★★). Fail = the tank empties
  with zero gems landed (even one landed is a pass).
- **Presentation:** fixed 3/4 front view of a contained diorama stage, portrait, no free
  rotation, slight parallax pan. HUD: live fuel gauge (top, flashes when low), gem tracker
  (top), liquid selector (bottom).
- **Optional aim assist:** a faint impact indicator showing where the stream lands.

## Out of scope for this slice (later features)
- World map / level progression / star gates / liquid-unlock pacing.
- Special materials (armored, reactive, unstable, chain).
- Boss stages, gun upgrades / meta progression.
- Monetization (ads, IAP), cosmetics/collection.
- Real fluid or voxel/fracture simulation (the melt is a cheap dissolve-shader + shrink).

## Resolved decisions (from the drain)
1. Orientation = **portrait**.
2. Win detection = **full guard**: minimum fall distance + settle-at-rest in a safe zone.
3. Fuel = **hard budget**: fixed start, drains while spraying, no refill; empty with zero
   landed = fail.
4. Wrong-liquid behavior in this slice = **near-zero effect + wasted fuel** (no reactive
   explosions yet — that's a later special material).
5. Fuel top-ups / in-stage pickups = **out of scope** for this slice (hard budget only).
