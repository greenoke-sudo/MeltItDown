<!--
STRICT CORE TEMPLATE for feature-spec.md (greenoke, domain-neutral).

Sections, headings, and order are mandatory. Do not rename, reorder, omit, or insert
core sections. Use exact text for headings. The spec is DESIGN LANGUAGE only — what the
feature does and how a user moves through it, never how it is built. The planner translates
to implementation.

CORE BANNED TOKENS (the verifier rejects these — they are cross-section implementation
leakage that does not belong in a design spec, regardless of project):
- Class / type names — identifiers in `PascalCase` used as a thing being built, and the
  suffixes `*Controller`, `*Manager`, `*Service`, `*Handler`, `*Event`, `*State`, `*View`,
  `*Component`, `*Widget`, `*Factory`, `*Provider`, `*Repository`, `*Model` used as type names.
- Method / function names — `identifier(...)` call syntax, `snake_case`/`camelCase` function refs.
- File paths and extensions — any `path/to/file`, any `.<ext>` filename reference.
- Field / wiring vocabulary — "serialized field", "reference", "wire", "bind to", "$ref",
  "inject", "hook up", hierarchy paths, namespace references.
- Architecture vocabulary — "module", "package", "table", "schema", "endpoint", "API route",
  "slice", "namespace", "config struct" used to describe the build.

ADAPTER BANNED TOKENS: the adapter may append project nouns (its tools, subsystems, file
roots) via `banned_tokens_source` in the manifest. Those are merged with this core list.

Describe everything in design language. Use the project's own domain words; avoid build words.

ADAPTER INJECTION: the adapter's `templates.spec_fragments` content is spliced at the
INJECT markers below — domain-specific sections (e.g. integration points, platform notes)
that this core skeleton intentionally does not name.

Remove these top comments and all `<!-- ... -->` guidance comments before commit.
-->

# Feature Spec: <feature-name>

## 1. Overview

<!-- 1 paragraph (1–5 sentences): what the feature is, who it's for, what problem it solves. Design language only. -->

## 2. Goals

<!-- 3–7 bullets. Design-level outcomes, not implementation tasks. -->

- 

## 3. Non-Goals

<!-- ≥1 bullet. Things explicitly out of scope for this feature. -->

- 

## 4. User Journey

<!-- 2–4 paragraphs. Narrative of how a user moves through the feature. No type or method names. -->

## 5. Surface Catalog

<!--
One row per behaviorally-distinct "surface" — a screen, view, page, output mode, or any
discrete thing the user perceives and that has its own states. "States" is the count of
distinct states for that surface. (Named "Surface" not "Screen" so it fits non-UI projects
too — a generated artifact, a CLI output mode, and a screen are all surfaces.)
-->

| Surface | Role | States |
|---|---|---|
|  |  |  |

## 6. State Specifications

<!--
One subsection per (surface × state) pair. Every surface in §5 must have at least one state
subsection here. Numbering is 6.1, 6.2, 6.3 ... in catalog order — all states of the first
surface, then all states of the second, etc.

The "Reference:" field is mandatory: the ground truth for this state (an image, a sample
artifact, an example output, a mock). Use comma-separated refs if more than one applies.

The "Acceptance Criteria (EARS)" field states this state's observable, testable behavior
using EARS templates (design language only — must still pass the banned-token check).
One or more bullets per state.

EARS templates (also used for §7 transitions):
- Ubiquitous   — "THE SYSTEM SHALL <behavior>." (always true)
- Event        — "WHEN <trigger> THE SYSTEM SHALL <behavior>." (response to an action/event)
- State-driven — "WHILE <state> THE SYSTEM SHALL <behavior>." (true throughout a state)
- Conditional  — "IF <condition> THEN THE SYSTEM SHALL <behavior>." (gated behavior)
-->

### 6.1 <Surface Name> — <State Name>

- **Reference:** `<path/to/reference>`
- **Entry condition:** 
- **Visible elements:** 
- **Hidden elements:** <!-- or "—" if none -->
- **Available actions:** 
- **Data displayed:** 
- **Acceptance Criteria (EARS):** <!-- one or more EARS-template bullets; design language only -->
  - 

## 7. Transitions

<!--
Every transition from the input plus any implicit ones.
From/To use "<Surface Name>.<State Name>" notation matching §5 and §6.
Trigger: user action or system event. Effect: what visibly changes.
Phrase each Trigger/Effect using the EARS templates documented under §6 (Event /
Conditional are the common fits for transitions); design language only.
-->

| From | To | Trigger | Effect |
|---|---|---|---|
|  |  |  |  |

## 8. Business Rules

<!-- All 5 sub-headings required. Use "N/A" if the topic doesn't apply to this feature. -->

### Progression
- 

### Economy
- 

### Timing
- 

### Competition
- 

### Gating
- 

## 9. Edge Cases & Defaults

<!--
All 4 sub-headings required. Each item: scenario → concrete default.
No TODO / TBD / ? entries — every case has a decided default.
-->

### Interruptions
- 

### State Recovery
- 

### Boundary Conditions
- 

### Error States
- 

<!-- INJECT: adapter spec_fragments here -->
<!--
The adapter's spec.fragments.md content is spliced in at this point. It adds domain-specific
spec sections the core does not name (e.g. for one project: platform/parity notes; for
another: artifact-format notes). Adapter sections are numbered §A1, §A2, … so they never
collide with the fixed core section numbers (1–11). They obey the same EARS + banned-token
discipline as the core sections.
-->

## 10. Open Questions

<!--
Empty at commit. During spec writing, list ambiguities here as bullets.
After AskUserQuestion rounds, append resolved Q&A below as a "Resolved Decisions" subsection.
-->

## 11. Glossary

<!-- Feature-specific terms used in this spec. -->

| Term | Definition |
|---|---|
|  |  |
