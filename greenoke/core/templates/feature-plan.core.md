<!--
STRICT CORE TEMPLATE for feature-plan.md (greenoke, domain-neutral).

The plan is the implementation "how" derived from the spec's "what". Sections, headings, and
order are mandatory. Do not rename, reorder, omit, or insert core sections. Use exact text
for headings. Unlike the spec, the plan DOES name modules, types, files, and integration
points — but in the project's own vocabulary (resolved from the adapter rules + research),
never hardcoded by the core.

The plan never restates spec content — it references the spec by section and translates it.

ADAPTER INJECTION: the adapter's `templates.plan_fragments` content is spliced at the INJECT
marker — domain integration points and any project-shaped sections the core skeleton does
not name. Adapter sections are numbered §A1, §A2, … (no collision with core 1–11).

Remove these top comments and all `<!-- ... -->` guidance comments before commit.
-->

# Feature Plan: <feature-name>

<!-- Reference: spec at <run_dir>/<run-id>/spec/reports/feature-spec.md -->

## 1. Overview

<!-- 1–3 sentences. Reference the spec by path. State the top-level structure and the
high-level implementation strategy. Do NOT restate the spec. -->

## 2. Structure & File Layout

<!-- Every source file, asset, and config this feature creates or edits, in the project's
own layout conventions (from the adapter rules). One line per file with its role. -->

## 3. Surface Decisions

<!-- Every spec §5 surface has a row. Decision is Reuse / Modify / New. Name the existing
component being reused or the new one being built, plus a one-line rationale grounded in the
codebase-researcher's reuse.md. -->

| Surface | Decision (Reuse / Modify / New) | Component | Rationale |
|---|---|---|---|
|  |  |  |  |

## 4. State Machine Implementation

<!-- Every spec §7 transition has a row with a concrete implementation trigger (function,
event, callback) and the implementation effect. Name the unit that owns the state. -->

| From | To | Trigger (impl) | Effect (impl) | Owner |
|---|---|---|---|---|
|  |  |  |  |  |

## 5. Data Structures

<!-- One subsection per data type the feature introduces or extends. Field-by-field. Reflects
the spec §8 business-rule needs. Name persistence location and shape. -->

## 6. Surface Specifications

<!-- One subsection per surface from §3 that is built or modified. For each: source reference,
destination, owning component/class, the state list (matching spec §6 state names), a
state-visibility table (which elements show per state), and the element mapping (each
referenced element → where it lives in the reference/source). Ground paths in research. -->

## 7. Cross-System Integration

<!--
The core requires these two integration sub-headings; the adapter's plan_fragments inject
the rest of the project's integration surface at the INJECT marker below. Use "N/A" where a
sub-heading doesn't apply.
-->

### Bootstrap & Registration
<!-- How the feature is initialized, registered, and discovered by the rest of the system. -->
- 

### Persistence
<!-- What state survives a restart, where it lives, and how it's keyed. -->
- 

<!-- INJECT: adapter plan_fragments here -->
<!--
The adapter's plan.fragments.md content is spliced in at this point, as additional §7
sub-headings or new adapter sections (§A1, §A2, …). These are the DOMAIN integration points
the core cannot name — e.g. for one project: the live-system hooks it must touch; for
another: its own subsystem wiring. The core ships only Bootstrap & Registration and
Persistence as universal; everything domain-shaped comes from the adapter.
-->

## 8. Pattern Conformance Map

<!-- One row per behavioral pattern the feature implies (persistence, timing, gating, rewards,
progression, etc.). Reference an existing feature/module that demonstrates it best (from the
researcher's patterns.md) and note any adaptations. The build phase treats a named reference
as binding — the implementation mirrors it. -->

| Pattern | Reference (existing) | Key unit to study | Adaptation |
|---|---|---|---|
|  |  |  |  |

## 9. Edge Case Implementation

<!-- Every spec §9 entry has a corresponding implementation note, grouped under the same four
sub-headings. No TODO / TBD / ? lines. -->

### Interruptions
- 

### State Recovery
- 

### Boundary Conditions
- 

### Error States
- 

## 10. Vertical Slice Plan

<!--
≥1 slice in build order. Each slice is independently buildable + verifiable. Every slice has
all 5 fields. This is the builder's slice list — the build skill does not re-derive it.
-->

### Slice 1: <name>
- **Deliverable:** 
- **Files touched:** 
- **Live-system surfaces involved:** <!-- which capability-provider verbs this slice exercises, or "—" -->
- **Dependencies:** 
- **Definition of done:** <!-- include the acceptance criteria this slice proves (from spec §6 EARS) -->

## 11. Open Questions

<!--
Empty at commit. During plan writing, list ambiguities here as bullets.
After AskUserQuestion rounds, append resolved Q&A below as a "Resolved Decisions" subsection.
-->
