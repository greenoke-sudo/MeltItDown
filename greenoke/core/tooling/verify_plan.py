#!/usr/bin/env python3
"""greenoke — runnable feature-plan verifier (project-agnostic, stdlib only).

Checks a produced `feature-plan.md` against the core plan-template contract
(`templates/feature-plan.core.md`) AND its coverage of the committed
`feature-spec.md`: every spec state maps to a plan decision, every spec transition
maps to a plan trigger, each vertical slice carries acceptance criteria +
dependencies + risks, and §11 Open Questions is empty-or-escalated.

It is the executable form of the plan skill's Phase-7 table — one place the checks
live, so the skill GATES on a real verdict.

Verdict semantics (subset of the framework verdict vocabulary):
  * PASS          — every check passed.
  * NEEDS_REVIEW  — at least one check failed (a spec state with no plan decision,
                    a transition with no plan trigger, a slice missing a required
                    field, an unresolved §11 question, …). Fail-closed: the plan
                    skill must NOT commit on NEEDS_REVIEW.

Project-agnostic: it knows the CORE plan/spec template section names, never a
project noun. The spec is the coverage oracle; it is passed in, not hardcoded.

Stdlib only (json, argparse, os, re, sys). Importable without side effects.

## Examples

    verify_plan.py path/to/feature-plan.md --spec path/to/feature-spec.md
        verify the plan's structure AND its coverage of the spec; JSON to stdout
    verify_plan.py path/to/feature-plan.md
        structure-only checks (no spec-coverage cross-check)
    verify_plan.py path/to/feature-plan.md --spec spec.md --human
        human-readable per-check report

## Environment

    (none)

## Exit codes

    0    verdict PASS
    1    verdict NEEDS_REVIEW (one or more checks failed)
    2    usage error (plan file not found, bad arguments)
"""

import argparse
import json
import os
import re
import sys

CORE_PLAN_SECTIONS = [
    "1. Overview",
    "2. Structure & File Layout",
    "3. Surface Decisions",
    "4. State Machine Implementation",
    "5. Data Structures",
    "6. Surface Specifications",
    "7. Cross-System Integration",
    "8. Pattern Conformance Map",
    "9. Edge Case Implementation",
    "10. Vertical Slice Plan",
    "11. Open Questions",
]

PLAN_INTEGRATION_CORE_SUBHEADINGS = ["Bootstrap & Registration", "Persistence"]
PLAN_EDGE_CASE_SUBHEADINGS = ["Interruptions", "State Recovery", "Boundary Conditions", "Error States"]

# A slice's five mandatory fields (template §10).
SLICE_FIELDS = ["Deliverable", "Files touched", "Dependencies", "Definition of done"]
# "Live-system surfaces involved" is required text but may be "—"; "Definition of done"
# carries the acceptance criteria. We additionally require an explicit risk note — the
# plan-template DoD bakes risk into the slice; we accept a "Risk" field OR a
# "Live-system surfaces involved" field as evidence of the 5-field shape, and flag a
# slice that has neither acceptance criteria nor dependencies.
TODO_MARKER = re.compile(r"(?<![A-Za-z])(TODO|TBD|FIXME|\?\?\?)\b|: *\?\s*$", re.MULTILINE)


# ── parsing helpers (shared shape with verify_spec) ────────────────────────────

def _strip_html_comments(text):
    return re.sub(r"<!--.*?-->", "", text, flags=re.DOTALL)


def _heading_sequence(text):
    out = []
    for line in text.splitlines():
        m = re.match(r"^(#{1,6})\s+(.*?)\s*#*\s*$", line)
        if m:
            out.append((len(m.group(1)), m.group(2).strip()))
    return out


def _section_body(text, h2_title):
    lines = text.splitlines()
    start = None
    for i, line in enumerate(lines):
        m = re.match(r"^##\s+(.*?)\s*$", line)
        if m and m.group(1).strip() == h2_title:
            start = i + 1
            break
    if start is None:
        return None
    body = []
    for line in lines[start:]:
        if re.match(r"^##\s+\S", line):
            break
        body.append(line)
    return "\n".join(body)


def _subheadings(body):
    if body is None:
        return set()
    return {m.group(1).strip() for m in re.finditer(r"^###\s+(.*?)\s*$", body, re.MULTILINE)}


def _table_rows(body):
    """Return data rows (list of cell-lists) from a markdown table body, dropping
    header + separator rows."""
    rows = []
    if not body:
        return rows
    for line in body.splitlines():
        if not line.strip().startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if not cells:
            continue
        if set("".join(cells)) <= set("-: "):  # separator
            continue
        rows.append(cells)
    # drop a header row if its first cell is a known header word
    if rows and rows[0] and rows[0][0].lower() in (
        "surface", "from", "pattern", "term", "field"
    ):
        rows = rows[1:]
    return rows


# ── spec-side parsing (the coverage oracle) ────────────────────────────────────

def _spec_surfaces(spec_text):
    body = _section_body(spec_text, "5. Surface Catalog")
    out = []
    for cells in _table_rows(body):
        if cells and cells[0]:
            out.append(cells[0])
    return out


def _spec_states(spec_text):
    """Return set of '<Surface> — <State>' names from spec §6 subsection titles."""
    body = _section_body(spec_text, "6. State Specifications")
    if not body:
        return set()
    return {m.group(1).strip()
            for m in re.finditer(r"^###\s+(.*?)\s*$", body, re.MULTILINE)}


def _spec_transitions(spec_text):
    """Return set of (from, to) endpoint pairs from spec §7 table."""
    body = _section_body(spec_text, "7. Transitions")
    pairs = set()
    for cells in _table_rows(body):
        if len(cells) >= 2 and cells[0] and cells[1]:
            pairs.add((cells[0], cells[1]))
    return pairs


# ── individual checks ──────────────────────────────────────────────────────────

def _check(name, ok, detail):
    return {"name": name, "status": "PASS" if ok else "FAIL", "detail": detail}


def _check_sections(text):
    headings = [t for (lvl, t) in _heading_sequence(text) if lvl == 2]
    missing = [s for s in CORE_PLAN_SECTIONS if s not in headings]
    if missing:
        return _check("sections_present", False,
                      f"missing core section(s): {', '.join(missing)}")
    positions = [headings.index(s) for s in CORE_PLAN_SECTIONS]
    if positions != sorted(positions):
        bad = [CORE_PLAN_SECTIONS[i] for i in range(1, len(positions))
               if positions[i] < positions[i - 1]]
        return _check("sections_ordered", False,
                      f"core sections out of order near: {', '.join(bad)}")
    return _check("sections_present_ordered", True,
                  "all 11 core plan sections present and in order")


def _check_integration_subheadings(text):
    body = _section_body(text, "7. Cross-System Integration")
    subs = _subheadings(body)
    missing = [s for s in PLAN_INTEGRATION_CORE_SUBHEADINGS if s not in subs]
    if missing:
        return _check("integration_subheadings", False,
                      f"§7 missing core sub-heading(s): {', '.join(missing)}")
    return _check("integration_subheadings", True,
                  "§7 has the two core integration sub-headings")


def _check_edge_cases(text):
    body = _section_body(text, "9. Edge Case Implementation")
    subs = _subheadings(body)
    missing = [s for s in PLAN_EDGE_CASE_SUBHEADINGS if s not in subs]
    if missing:
        return _check("edge_cases_subheadings", False,
                      f"§9 missing sub-heading(s): {', '.join(missing)}")
    if body and TODO_MARKER.search(body):
        return _check("edge_cases_no_todo", False, "§9 contains a TODO/TBD/? entry")
    return _check("edge_cases_subheadings", True,
                  "§9 has all 4 sub-headings, no TODO/TBD/?")


def _slices(text):
    """Return [(name, body)] for every §10 slice (### Slice N: ...)."""
    body = _section_body(text, "10. Vertical Slice Plan")
    out = []
    if not body:
        return out
    parts = re.split(r"^###\s+(Slice\s+.*?)\s*$", body, flags=re.MULTILINE)
    for i in range(1, len(parts), 2):
        out.append((parts[i].strip(), parts[i + 1] if i + 1 < len(parts) else ""))
    return out


def _check_slices(text):
    slices = _slices(text)
    if not slices:
        return _check("slices_complete", False, "§10 has no slices (need ≥1)")
    problems = []
    for name, body in slices:
        for field in SLICE_FIELDS:
            # field present as a bold label and carries a non-empty value
            m = re.search(r"\*\*" + re.escape(field) + r":?\*\*\s*(.*)", body)
            if not m or not m.group(1).strip():
                problems.append(f"{name}: missing/empty '{field}'")
        # Definition of done must carry acceptance criteria (the spec EARS it proves)
        dod = re.search(r"\*\*Definition of done:?\*\*\s*(.*)", body)
        if dod and not dod.group(1).strip():
            problems.append(f"{name}: empty Definition of done (no acceptance criteria)")
    if problems:
        return _check("slices_complete", False,
                      f"{len(problems)} slice field issue(s): " + "; ".join(problems[:6]))
    return _check("slices_complete", True,
                  f"§10 has {len(slices)} slice(s), each with all required fields "
                  "(deliverable, files, dependencies, definition-of-done/acceptance)")


def _check_open_questions(text):
    body = _section_body(text, "11. Open Questions")
    if body is None:
        return _check("open_questions_empty", False, "§11 Open Questions section missing")
    pre = re.split(r"^###\s+Resolved Decisions\s*$", body, maxsplit=1, flags=re.MULTILINE)[0]
    open_bullets = [l for l in pre.splitlines() if re.match(r"^\s*[-*]\s+\S", l)]
    if open_bullets:
        return _check("open_questions_empty", False,
                      f"§11 has {len(open_bullets)} unresolved open question bullet(s)")
    return _check("open_questions_empty", True, "§11 Open Questions empty-or-escalated")


# ── spec-coverage checks (require --spec) ──────────────────────────────────────

def _check_surface_decisions(text, spec_text):
    spec_surfaces = _spec_surfaces(spec_text)
    body = _section_body(text, "3. Surface Decisions")
    plan_rows = _table_rows(body)
    decided = {cells[0] for cells in plan_rows if cells and cells[0]}
    uncovered = [s for s in spec_surfaces if s not in decided]
    if uncovered:
        return _check("surface_decisions_cover_spec", False,
                      f"spec §5 surface(s) with no plan §3 decision: {', '.join(uncovered)}")
    return _check("surface_decisions_cover_spec", True,
                  f"all {len(spec_surfaces)} spec surface(s) have a plan §3 decision")


def _check_state_coverage(text, spec_text):
    spec_states = _spec_states(spec_text)
    plan_body = _section_body(text, "6. Surface Specifications")
    plan_text = plan_body or ""
    # A spec state '<Surface> — <State>' is covered if BOTH the surface and the state
    # name appear in plan §6 (the plan need not echo the em-dash title verbatim).
    uncovered = []
    for full in spec_states:
        # strip leading "N.M " numbering from the spec §6 title before splitting
        bare = re.sub(r"^\d+(\.\d+)*\s+", "", full).strip()
        parts = re.split(r"\s+[—\-]\s+", bare, maxsplit=1)
        surface = parts[0].strip()
        state = parts[1].strip() if len(parts) > 1 else ""
        if surface not in plan_text or (state and state not in plan_text):
            uncovered.append(full)
    if uncovered:
        return _check("states_cover_spec", False,
                      f"spec §6 state(s) absent from plan §6: {', '.join(sorted(uncovered)[:6])}")
    return _check("states_cover_spec", True,
                  f"all {len(spec_states)} spec state(s) appear in plan §6")


def _check_transition_coverage(text, spec_text):
    spec_pairs = _spec_transitions(spec_text)
    body = _section_body(text, "4. State Machine Implementation")
    plan_rows = _table_rows(body)
    plan_pairs = {(c[0], c[1]) for c in plan_rows if len(c) >= 2 and c[0] and c[1]}
    uncovered = [f"{a}→{b}" for (a, b) in spec_pairs if (a, b) not in plan_pairs]
    if uncovered:
        return _check("transitions_cover_spec", False,
                      f"spec §7 transition(s) with no plan §4 trigger: {', '.join(sorted(uncovered)[:6])}")
    return _check("transitions_cover_spec", True,
                  f"all {len(spec_pairs)} spec transition(s) mapped to a plan §4 trigger")


# ── public API ─────────────────────────────────────────────────────────────────

def verify_plan(plan_path, spec_path=None):
    """Verify a feature-plan.md (and, if spec_path given, its coverage of the spec).
    Return {verdict, checks, plan, spec}. Pure, side-effect-free."""
    with open(plan_path, encoding="utf-8") as f:
        text = _strip_html_comments(f.read())

    checks = [
        _check_sections(text),
        _check_integration_subheadings(text),
        _check_edge_cases(text),
        _check_slices(text),
        _check_open_questions(text),
    ]

    if spec_path:
        with open(spec_path, encoding="utf-8") as f:
            spec_text = _strip_html_comments(f.read())
        checks.extend([
            _check_surface_decisions(text, spec_text),
            _check_state_coverage(text, spec_text),
            _check_transition_coverage(text, spec_text),
        ])

    verdict = "PASS" if all(c["status"] == "PASS" for c in checks) else "NEEDS_REVIEW"
    return {"verdict": verdict, "checks": checks, "plan": plan_path, "spec": spec_path}


def main(argv=None):
    ap = argparse.ArgumentParser(
        prog="verify_plan.py",
        description="Verify a greenoke feature-plan.md against the core contract and the spec.",
    )
    ap.add_argument("plan", help="path to the produced feature-plan.md")
    ap.add_argument("--spec", default=None,
                    help="the committed feature-spec.md to cross-check coverage against")
    ap.add_argument("--human", action="store_true", help="human-readable report (default: JSON)")
    args = ap.parse_args(argv)

    if not os.path.isfile(args.plan):
        print(f"ERROR: plan not found at {args.plan}", file=sys.stderr)
        return 2
    if args.spec and not os.path.isfile(args.spec):
        print(f"ERROR: spec not found at {args.spec}", file=sys.stderr)
        return 2

    report = verify_plan(args.plan, args.spec)

    if args.human:
        print(f"plan    : {report['plan']}")
        print(f"spec    : {report['spec'] or '(structure-only)'}")
        print(f"verdict : {report['verdict']}")
        for c in report["checks"]:
            print(f"  [{c['status']:>4}] {c['name']}: {c['detail']}")
    else:
        print(json.dumps(report, indent=2))

    return 0 if report["verdict"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
