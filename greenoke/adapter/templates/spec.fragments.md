<!--
ADAPTER SPEC FRAGMENTS — MeltItDown.
Spliced into greenoke/core/templates/feature-spec.core.md at its
`<!-- INJECT: adapter spec_fragments here -->` marker.

DESIGN LANGUAGE ONLY. No implementation nouns (class names, method names, file
paths, asset GUIDs) — those belong in the plan, not the spec. Obeys the same
EARS + banned-token discipline as the core sections. Section numbers use §A* so
they never collide with the fixed core sections (1-11).

These are lightweight STARTER headings — a MeltItDown feature spec fills the ones
that apply and writes "N/A" for the rest.
-->

## A1. Player-facing behavior

- <what the player sees / feels — screens, interactions, feedback, juice — in design language>

## A2. Tunables surfaced to the Inspector (no hardcoding)

- <which colors / sizes / durations / curves / audio / haptics / balance values this feature
  exposes as ScriptableObject config — never hardcoded in C#>

## A3. Platform / mobile constraints

- <portrait mobile-first, touch input, safe-area / aspect-ratio handling, and the device
  performance budget the feature must respect>
