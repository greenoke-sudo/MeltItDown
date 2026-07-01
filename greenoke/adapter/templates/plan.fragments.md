<!--
ADAPTER PLAN FRAGMENTS — MeltItDown.
Spliced into greenoke/core/templates/feature-plan.core.md at its
`<!-- INJECT: adapter plan_fragments here -->` marker (inside §7 Cross-System
Integration). These are the live-system integration points a MeltItDown feature's
plan must address. For each, name the concrete config asset / script / scene the
feature plugs into and what it registers or changes. Use "N/A" where a system is
genuinely untouched.

Lightweight STARTER headings — fill per feature.
-->

### ScriptableObject config touched
- <which config asset(s) this feature reads or extends; new tunables go here, never hardcoded in C#>

### Scene / prefab impact
- <scenes and prefabs added or modified; what is wired in the Inspector>

### Input (Unity Input System)
- <which actions / action maps in InputSystem_Actions this feature binds or adds>

### Animation / feedback
- <animations, transitions, haptics, audio cues; durations + curves sourced from config assets>

### Save / persistence
- <state persisted, and by what mechanism, if any>

### EditMode / PlayMode test
- <which automated tests cover this — EditMode for pure logic, PlayMode for runtime/scene behavior>

### Capability-provider verbs used
- <which provider verbs the build/QA phases drive: inspect / build / verify / screenshot (health is always available)>
