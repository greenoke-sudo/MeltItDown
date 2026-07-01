#!/usr/bin/env python3
"""greenoke — runnable feature-spec verifier (project-agnostic, stdlib only).

Checks a produced `feature-spec.md` against the core template contract
(`templates/feature-spec.core.md`) and the effective banned-token list (core bans
plus the adapter's `banned_tokens_source`). It is the executable form of the spec
skill's Phase-6 table — one place the checks live, so the skill GATES on a real
verdict instead of an agent's self-grading.

Verdict semantics (a strict subset of the framework verdict vocabulary — a spec
either holds the contract or a human must look):
  * PASS          — every check passed.
  * NEEDS_REVIEW  — at least one check failed (missing/misordered section, a banned
                    token leaked, a state with no EARS acceptance criteria, an
                    unresolved §10 Open Question, …). Fail-closed: the spec skill
                    must NOT commit on NEEDS_REVIEW.

Project-agnostic: it knows the CORE template's section names and the CORE banned
tokens; every project specific is injected (the adapter banned-token file path, the
spec file path). No project noun appears in this file.

Stdlib only (json, argparse, os, re, sys). Importable without side effects:
`from verify_spec import verify_spec` works; nothing runs until called.

## Examples

    verify_spec.py path/to/feature-spec.md
        verify against the core contract; JSON verdict to stdout
    verify_spec.py path/to/feature-spec.md --banned-tokens greenoke/adapter/templates/banned-tokens.txt
        also reject the adapter's project nouns (merged with the core list)
    verify_spec.py path/to/feature-spec.md --human
        human-readable per-check report instead of JSON

## Environment

    (none)

## Exit codes

    0    verdict PASS
    1    verdict NEEDS_REVIEW (one or more checks failed)
    2    usage error (spec file not found, bad arguments)
"""

import argparse
import json
import os
import re
import sys

# ── The CORE section contract (exact heading text, in mandatory order) ─────────
# Mirrors templates/feature-spec.core.md §1–§11. Adapter §A sections are allowed
# to appear between §9 and §10 (the INJECT point) and are NOT part of this fixed
# list — the order check tolerates anything between §9 and §10.
CORE_SECTIONS = [
    "1. Overview",
    "2. Goals",
    "3. Non-Goals",
    "4. User Journey",
    "5. Surface Catalog",
    "6. State Specifications",
    "7. Transitions",
    "8. Business Rules",
    "9. Edge Cases & Defaults",
    "10. Open Questions",
    "11. Glossary",
]

BUSINESS_RULE_SUBHEADINGS = ["Progression", "Economy", "Timing", "Competition", "Gating"]
EDGE_CASE_SUBHEADINGS = ["Interruptions", "State Recovery", "Boundary Conditions", "Error States"]

# ── CORE banned tokens (cross-section implementation leakage; project-agnostic) ─
# Regexes. The effective list = these + the adapter's banned_tokens_source lines.
# Kept conservative so design-language prose is not flagged: we target *call
# syntax*, *file paths/extensions*, *type-name suffixes used as a built thing*, and
# explicit wiring/architecture vocabulary — not every PascalCase word.
CORE_BANNED_PATTERNS = [
    # type-name suffixes used as a thing being built
    (r"\b[A-Z][A-Za-z0-9]*"
     r"(Controller|Manager|Service|Handler|Event|State|View|Component|Widget|"
     r"Factory|Provider|Repository|Model)\b", "type-name suffix"),
    # method / function call syntax: identifier( … )
    (r"\b[a-z_][A-Za-z0-9_]*\([^)]*\)", "function-call syntax"),
    # file extensions / paths
    (r"\b[\w./-]+\.(py|cs|js|ts|json|yaml|yml|md|sh|cpp|h|hpp|java|go|rs|shader|hlsl|cginc|prefab|asset|meta)\b",
     "file path/extension"),
    # explicit wiring / architecture vocabulary
    (r"\bserialized field\b", "wiring vocabulary"),
    (r"\bwire (up|to)\b", "wiring vocabulary"),
    (r"\bbind to\b", "wiring vocabulary"),
    (r"\$ref\b", "wiring vocabulary"),
    (r"\bhook (up|it up)\b", "wiring vocabulary"),
    (r"\bAPI route\b", "architecture vocabulary"),
    (r"\bendpoint\b", "architecture vocabulary"),
    (r"\bnamespace\b", "architecture vocabulary"),
    (r"\bconfig struct\b", "architecture vocabulary"),
]

EARS_KEYWORD = re.compile(r"\bSHALL\b")
EARS_TRIGGERS = re.compile(r"\b(WHEN|WHILE|IF)\b")
TODO_MARKER = re.compile(r"(?<![A-Za-z])(TODO|TBD|FIXME|\?\?\?)\b|: *\?\s*$", re.MULTILINE)


# ── parsing helpers ────────────────────────────────────────────────────────────

def _strip_html_comments(text):
    """Remove <!-- ... --> blocks (template guidance) so they never trip checks."""
    return re.sub(r"<!--.*?-->", "", text, flags=re.DOTALL)


def _heading_sequence(text):
    """Return [(level, title)] for every ATX heading, in document order."""
    out = []
    for line in text.splitlines():
        m = re.match(r"^(#{1,6})\s+(.*?)\s*#*\s*$", line)
        if m:
            out.append((len(m.group(1)), m.group(2).strip()))
    return out


def _section_body(text, h2_title):
    """Return the body text under a `## <h2_title>` up to the next `## ` heading."""
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
        if re.match(r"^##\s+\S", line):  # next h2 (not h3+)
            break
        body.append(line)
    return "\n".join(body)


def _subheadings(body):
    """Set of h3 (### ) titles within a section body."""
    if body is None:
        return set()
    return {m.group(1).strip() for m in re.finditer(r"^###\s+(.*?)\s*$", body, re.MULTILINE)}


def _load_banned_patterns(banned_tokens_path):
    """Effective banned list = core patterns + one literal regex per non-comment,
    non-blank line of the adapter banned-tokens file (if given)."""
    patterns = list(CORE_BANNED_PATTERNS)
    if banned_tokens_path and os.path.isfile(banned_tokens_path):
        with open(banned_tokens_path, encoding="utf-8") as f:
            for raw in f:
                line = raw.strip()
                if not line or line.startswith("#"):
                    continue
                # Treat each adapter token as a word-ish literal; the file already
                # holds "regex fragments" per its own docstring, so honor them.
                patterns.append((r"\b" + re.escape(line) + r"\b", f"adapter noun '{line}'"))
    return patterns


def _scan_banned(text, patterns):
    """Return a list of (token_class, matched_text) for every banned hit."""
    hits = []
    for pat, label in patterns:
        for m in re.finditer(pat, text):
            hits.append((label, m.group(0)))
    return hits


# ── individual checks ──────────────────────────────────────────────────────────

def _check(name, ok, detail):
    return {"name": name, "status": "PASS" if ok else "FAIL", "detail": detail}


def _check_sections_present_ordered(text):
    headings = [t for (lvl, t) in _heading_sequence(text) if lvl == 2]
    # Presence
    missing = [s for s in CORE_SECTIONS if s not in headings]
    if missing:
        return _check("sections_present", False,
                      f"missing core section(s): {', '.join(missing)}")
    # Order: the core sections must appear in the prescribed relative order. Adapter
    # §A sections may interleave anywhere (notably between §9 and §10).
    positions = [headings.index(s) for s in CORE_SECTIONS]
    if positions != sorted(positions):
        bad = [CORE_SECTIONS[i] for i in range(1, len(positions)) if positions[i] < positions[i - 1]]
        return _check("sections_ordered", False,
                      f"core sections out of order near: {', '.join(bad)}")
    return _check("sections_present_ordered", True,
                  "all 11 core sections present and in order")


def _check_business_rules(text):
    body = _section_body(text, "8. Business Rules")
    subs = _subheadings(body)
    missing = [s for s in BUSINESS_RULE_SUBHEADINGS if s not in subs]
    if missing:
        return _check("business_rules_subheadings", False,
                      f"§8 missing sub-heading(s): {', '.join(missing)}")
    return _check("business_rules_subheadings", True, "§8 has all 5 sub-headings")


def _check_edge_cases(text):
    body = _section_body(text, "9. Edge Cases & Defaults")
    subs = _subheadings(body)
    missing = [s for s in EDGE_CASE_SUBHEADINGS if s not in subs]
    if missing:
        return _check("edge_cases_subheadings", False,
                      f"§9 missing sub-heading(s): {', '.join(missing)}")
    if body and TODO_MARKER.search(body):
        return _check("edge_cases_no_todo", False, "§9 contains a TODO/TBD/? entry")
    return _check("edge_cases_subheadings", True, "§9 has all 4 sub-headings, no TODO/TBD/?")


def _surface_states(text):
    """Parse §5 catalog rows → [(surface, states_count_or_None)]."""
    body = _section_body(text, "5. Surface Catalog")
    rows = []
    if not body:
        return rows
    for line in body.splitlines():
        if not line.strip().startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 3:
            continue
        surface = cells[0]
        if surface.lower() in ("surface", "") or set(surface) <= set("-: "):
            continue  # header / separator / blank
        states = cells[2]
        try:
            n = int(re.sub(r"[^0-9]", "", states)) if re.search(r"\d", states) else None
        except ValueError:
            n = None
        rows.append((surface, n))
    return rows


def _check_catalog_states(text):
    rows = _surface_states(text)
    if not rows:
        return _check("catalog_states", False, "§5 Surface Catalog has no data rows")
    missing = [s for (s, n) in rows if not n or n < 1]
    if missing:
        return _check("catalog_states", False,
                      f"§5 surface(s) missing a States count: {', '.join(missing)}")
    return _check("catalog_states", True,
                  f"§5 has {len(rows)} surface(s), each with a States count")


def _state_subsections(text):
    """Return [(title, body)] for every §6 subsection (### within §6)."""
    body = _section_body(text, "6. State Specifications")
    out = []
    if not body:
        return out
    parts = re.split(r"^###\s+(.*?)\s*$", body, flags=re.MULTILINE)
    # parts = [pre, title1, body1, title2, body2, ...]
    for i in range(1, len(parts), 2):
        out.append((parts[i].strip(), parts[i + 1] if i + 1 < len(parts) else ""))
    return out


def _check_states_have_ears(text):
    """Every surface in §5 has ≥1 §6 state subsection, and EACH §6 subsection
    carries an `Acceptance Criteria (EARS)` field with EARS phrasing (SHALL)."""
    surfaces = [s for (s, _n) in _surface_states(text)]
    subs = _state_subsections(text)
    if not subs:
        return _check("states_have_ears", False, "§6 has no state subsections")

    # 1. Coverage: every §5 surface name appears as the surface part of some §6 title
    #    (titles are "<Surface> — <State>").
    covered = set()
    for title, _b in subs:
        # titles are "6.1 <Surface> — <State>" — strip the leading "N.M " numbering
        # before splitting on the em-dash/hyphen, so the surface part matches §5.
        bare = re.sub(r"^\d+(\.\d+)*\s+", "", title).strip()
        surface_part = re.split(r"\s+[—\-]\s+", bare, maxsplit=1)[0].strip()
        covered.add(surface_part)
    uncovered = [s for s in surfaces if s not in covered]
    if uncovered:
        return _check("states_have_ears", False,
                      f"§5 surface(s) with no §6 state subsection: {', '.join(uncovered)}")

    # 2. Each subsection has an EARS acceptance-criteria field with SHALL phrasing.
    no_ears = []
    for title, body in subs:
        if "Acceptance Criteria (EARS)" not in body:
            no_ears.append(f"{title} (no EARS field)")
            continue
        # the criteria bullets after the field must contain SHALL
        after = body.split("Acceptance Criteria (EARS)", 1)[1]
        if not EARS_KEYWORD.search(after):
            no_ears.append(f"{title} (EARS field has no SHALL)")
    if no_ears:
        return _check("states_have_ears", False,
                      "§6 state(s) without EARS acceptance criteria: " + "; ".join(no_ears))
    return _check("states_have_ears", True,
                  f"all {len(subs)} §6 state(s) carry EARS acceptance criteria; "
                  f"all {len(surfaces)} surface(s) covered")


def _check_transitions_ears(text):
    body = _section_body(text, "7. Transitions")
    if not body:
        return _check("transitions_ears", False, "§7 Transitions section is empty")
    rows = [l for l in body.splitlines() if l.strip().startswith("|")]
    data = []
    for line in rows:
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 4:
            continue
        if cells[0].lower() == "from" or set(cells[0]) <= set("-: "):
            continue
        data.append(cells)
    if not data:
        return _check("transitions_ears", False, "§7 has no transition data rows")
    weak = [c[0] + "→" + c[1] for c in data
            if not (EARS_KEYWORD.search(c[2] + c[3]) or EARS_TRIGGERS.search(c[2] + c[3]))]
    if weak:
        return _check("transitions_ears", False,
                      "§7 transition(s) without EARS phrasing: " + ", ".join(weak))
    return _check("transitions_ears", True,
                  f"§7 has {len(data)} transition(s), each EARS-phrased")


def _check_open_questions(text):
    """§10 must be empty-or-escalated: no open bullets remain. A `Resolved Decisions`
    subsection is allowed (and expected when Q&A occurred); open `- ` bullets that
    are NOT under Resolved Decisions are unresolved → FAIL."""
    body = _section_body(text, "10. Open Questions")
    if body is None:
        return _check("open_questions_empty", False, "§10 Open Questions section missing")
    # Split off the Resolved Decisions subsection — anything before it that is an
    # open bullet is an unresolved question.
    pre = re.split(r"^###\s+Resolved Decisions\s*$", body, maxsplit=1, flags=re.MULTILINE)[0]
    open_bullets = [l for l in pre.splitlines() if re.match(r"^\s*[-*]\s+\S", l)]
    if open_bullets:
        return _check("open_questions_empty", False,
                      f"§10 has {len(open_bullets)} unresolved open question bullet(s)")
    return _check("open_questions_empty", True, "§10 Open Questions empty-or-escalated")


def _check_banned_tokens(text, patterns):
    hits = _scan_banned(text, patterns)
    if hits:
        sample = "; ".join(f"{lbl}: '{tok}'" for lbl, tok in hits[:6])
        more = f" (+{len(hits) - 6} more)" if len(hits) > 6 else ""
        return _check("banned_tokens_absent", False,
                      f"{len(hits)} banned-token hit(s): {sample}{more}")
    return _check("banned_tokens_absent", True, "no banned tokens (core + adapter) present")


# ── public API ─────────────────────────────────────────────────────────────────

def verify_spec(spec_path, banned_tokens_path=None):
    """Verify a feature-spec.md. Return the verdict dict:
       {verdict: PASS|NEEDS_REVIEW, checks: [{name,status,detail}], spec: <path>}.
    Pure: reads the spec file, never writes. Side-effect-free import."""
    with open(spec_path, encoding="utf-8") as f:
        raw = f.read()
    text = _strip_html_comments(raw)
    patterns = _load_banned_patterns(banned_tokens_path)

    checks = [
        _check_sections_present_ordered(text),
        _check_catalog_states(text),
        _check_states_have_ears(text),
        _check_transitions_ears(text),
        _check_business_rules(text),
        _check_edge_cases(text),
        _check_open_questions(text),
        _check_banned_tokens(text, patterns),
    ]
    verdict = "PASS" if all(c["status"] == "PASS" for c in checks) else "NEEDS_REVIEW"
    return {"verdict": verdict, "checks": checks, "spec": spec_path}


def main(argv=None):
    ap = argparse.ArgumentParser(
        prog="verify_spec.py",
        description="Verify a greenoke feature-spec.md against the core template contract.",
    )
    ap.add_argument("spec", help="path to the produced feature-spec.md")
    ap.add_argument("--banned-tokens", default=None,
                    help="adapter banned_tokens_source file (merged with the core list)")
    ap.add_argument("--human", action="store_true", help="human-readable report (default: JSON)")
    args = ap.parse_args(argv)

    if not os.path.isfile(args.spec):
        print(f"ERROR: spec not found at {args.spec}", file=sys.stderr)
        return 2

    report = verify_spec(args.spec, args.banned_tokens)

    if args.human:
        print(f"spec    : {report['spec']}")
        print(f"verdict : {report['verdict']}")
        for c in report["checks"]:
            print(f"  [{c['status']:>4}] {c['name']}: {c['detail']}")
    else:
        print(json.dumps(report, indent=2))

    return 0 if report["verdict"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
