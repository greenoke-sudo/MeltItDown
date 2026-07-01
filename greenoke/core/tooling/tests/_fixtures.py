"""Shared in-memory fixtures for the greenoke tooling tests.

A CLEAN spec and a CLEAN plan that pass their respective verifiers, built as
strings so the tests need no on-disk template. Helpers mutate copies to produce the
NEEDS_REVIEW variants (missing section, missing EARS, etc.). Project-agnostic — the
fixtures use a neutral toy feature ("widget gallery") with no project nouns.
"""

CLEAN_SPEC = """# Feature Spec: widget-gallery

## 1. Overview

A gallery surface that lets a person browse saved widgets and open one for a closer
look. It solves the problem of widgets being scattered with no single place to see
them all.

## 2. Goals

- Let a person see every saved widget at a glance.
- Let a person open a single widget for detail.
- Keep the empty experience inviting rather than blank.

## 3. Non-Goals

- Editing a widget's contents.

## 4. User Journey

A person opens the gallery and sees their saved widgets in a grid. When there are
none, an inviting empty message appears instead. Selecting any widget opens a detail
surface; dismissing it returns to the grid.

## 5. Surface Catalog

| Surface | Role | States |
|---|---|---|
| Gallery | Browse saved widgets | 2 |
| Detail | View one widget closely | 1 |

## 6. State Specifications

### 6.1 Gallery — Populated

- **Reference:** mock-gallery-populated
- **Entry condition:** at least one widget is saved
- **Visible elements:** the widget grid, a title
- **Hidden elements:** the empty message
- **Available actions:** select a widget
- **Data displayed:** each saved widget's preview
- **Acceptance Criteria (EARS):**
  - WHILE at least one widget is saved THE SYSTEM SHALL display the widget grid.
  - WHEN a person selects a widget THE SYSTEM SHALL open the detail surface.

### 6.2 Gallery — Empty

- **Reference:** mock-gallery-empty
- **Entry condition:** no widgets are saved
- **Visible elements:** an inviting empty message
- **Hidden elements:** the widget grid
- **Available actions:** none
- **Data displayed:** an encouragement to save a widget
- **Acceptance Criteria (EARS):**
  - IF no widgets are saved THEN THE SYSTEM SHALL display the inviting empty message.

### 6.3 Detail — Open

- **Reference:** mock-detail-open
- **Entry condition:** a person selected a widget
- **Visible elements:** the widget at full size, a dismiss affordance
- **Hidden elements:** —
- **Available actions:** dismiss
- **Data displayed:** the selected widget in full
- **Acceptance Criteria (EARS):**
  - WHEN a person dismisses the detail THE SYSTEM SHALL return to the gallery grid.

## 7. Transitions

| From | To | Trigger | Effect |
|---|---|---|---|
| Gallery.Populated | Detail.Open | WHEN a person selects a widget | THE SYSTEM SHALL open the detail surface |
| Detail.Open | Gallery.Populated | WHEN a person dismisses the detail | THE SYSTEM SHALL return to the grid |

## 8. Business Rules

### Progression
- N/A

### Economy
- N/A

### Timing
- N/A

### Competition
- N/A

### Gating
- A person sees the gallery only after at least one widget exists in their account.

## 9. Edge Cases & Defaults

### Interruptions
- A person leaving mid-browse returns to the grid on the next visit.

### State Recovery
- The grid rebuilds from saved widgets each visit; nothing transient is kept.

### Boundary Conditions
- With exactly one widget the grid shows a single item, not the empty message.

### Error States
- A widget that fails to load shows a placeholder rather than a broken slot.

## 10. Open Questions

### Resolved Decisions
- Q: Should the empty state allow creating a widget? A: No — out of scope.

## 11. Glossary

| Term | Definition |
|---|---|
| Widget | A saved item a person can browse in the gallery. |
"""


CLEAN_PLAN = """# Feature Plan: widget-gallery

## 1. Overview

Implements the gallery described in the spec by reusing the existing grid list and
adding one detail surface.

## 2. Structure & File Layout

- gallery_view — the grid surface (modify existing)
- detail_view — the new detail surface

## 3. Surface Decisions

| Surface | Decision (Reuse / Modify / New) | Component | Rationale |
|---|---|---|---|
| Gallery | Modify | gallery_view | extend the existing grid list |
| Detail | New | detail_view | no full-size viewer exists yet |

## 4. State Machine Implementation

| From | To | Trigger (impl) | Effect (impl) | Owner |
|---|---|---|---|---|
| Gallery.Populated | Detail.Open | on_widget_selected callback | push detail_view | gallery_view |
| Detail.Open | Gallery.Populated | on_dismiss callback | pop detail_view | detail_view |

## 5. Data Structures

### GalleryModel
- widgets: list of saved widget refs; persisted in the account store.

## 6. Surface Specifications

### Gallery
Owning unit gallery_view. States: Populated, Empty. Element mapping grounded in the
grid list. The Gallery surface renders both Populated and Empty states.

### Detail
Owning unit detail_view. State: Open. Shows the selected widget full size.

## 7. Cross-System Integration

### Bootstrap & Registration
- gallery_view registers in the surface registry at startup.

### Persistence
- The widget list survives restart in the account store, keyed by account id.

## 8. Pattern Conformance Map

| Pattern | Reference (existing) | Key unit to study | Adaptation |
|---|---|---|---|
| List rendering | settings_list | settings_list grid | swap row template for widget preview |

## 9. Edge Case Implementation

### Interruptions
- On re-entry the grid rebuilds from the saved widget list.

### State Recovery
- No transient state to recover; the grid is derived each visit.

### Boundary Conditions
- A single-widget account renders the grid, not the empty message.

### Error States
- A failed widget load substitutes a placeholder preview.

## 10. Vertical Slice Plan

### Slice 1: gallery grid + empty state
- **Deliverable:** the Gallery surface rendering Populated and Empty states.
- **Files touched:** gallery_view
- **Live-system surfaces involved:** inspect, screenshot
- **Dependencies:** the account widget store
- **Definition of done:** proves spec §6.1 and §6.2 — grid shows when widgets exist, inviting empty message when none.

### Slice 2: detail surface
- **Deliverable:** the Detail surface with dismiss.
- **Files touched:** detail_view
- **Live-system surfaces involved:** screenshot
- **Dependencies:** Slice 1
- **Definition of done:** proves spec §6.3 and both §7 transitions — selecting opens detail, dismissing returns.

## 11. Open Questions

### Resolved Decisions
- Q: Reuse the settings grid? A: Yes — extend it.
"""


def spec_missing_section():
    """Drop §7 Transitions entirely → sections_present FAIL."""
    out = []
    skip = False
    for line in CLEAN_SPEC.splitlines():
        if line.startswith("## 7. Transitions"):
            skip = True
            continue
        if skip and line.startswith("## 8. "):
            skip = False
        if not skip:
            out.append(line)
    return "\n".join(out)


def spec_missing_ears():
    """Strip the EARS acceptance criteria from §6.3 → states_have_ears FAIL."""
    out = []
    in_63 = False
    for line in CLEAN_SPEC.splitlines():
        if line.startswith("### 6.3 "):
            in_63 = True
        elif line.startswith("## 7."):
            in_63 = False
        if in_63 and ("Acceptance Criteria (EARS)" in line
                      or line.strip().startswith("- WHEN a person dismisses")):
            continue
        out.append(line)
    return "\n".join(out)
