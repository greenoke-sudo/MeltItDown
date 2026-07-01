#!/usr/bin/env python3
"""greenoke — terminal BUILD verdict computer (project-agnostic, stdlib only).

ONE place the fail-closed build-gate logic lives, so the build skill + qa agent
reason about a single, testable function instead of re-deriving the rules in prose.
Computes the terminal verdict from four inputs:

  1. checks            — QA findings, each {name, severity, status, detail}.
                         severity ∈ {BLOCKING, MINOR}; status ∈ {open, resolved}.
  2. deferrals         — each {provenance, requirement, detail}.
                         provenance ∈ {accepted, builder_emitted}.
  3. cap_hit           — bool: the QA loop reached qa_iteration_cap with work left.
  4. stale_guard       — {triggered: bool, detail}: stale_system_guard fired
                         (running code_version ≠ working tree) → cannot go green.

Verdict semantics (framework law, §5.4 of the blueprint):
  * PASS                  — zero open BLOCKING, zero deferrals of ANY provenance,
                            cap not hit, stale guard not triggered.
  * PASS_WITH_DEFERRALS   — zero open BLOCKING; ≥1 deferral and EVERY remaining
                            deferral is USER-APPROVED (provenance=accepted); cap not
                            blocking; stale guard not triggered.
  * NEEDS_REVIEW          — ANY open BLOCKING finding, OR ANY builder-emitted
                            (unreviewed) deferral, OR cap hit with unresolved work,
                            OR stale guard triggered.

Hard rules encoded here (so they cannot regress in a skill refactor):
  - Deferral PROVENANCE is never conflated. `accepted` (user-approved, from plan
    Open-Questions / Q&A) is honored as DEFERRED. `builder_emitted` ("couldn't
    finish") is NEVER auto-honored → it forces NEEDS_REVIEW.
  - At the cap with unresolved BLOCKING (or builder-emitted deferrals) → NEEDS_REVIEW.
    The gate NEVER auto-defers to green.
  - stale_system_guard triggered → NEEDS_REVIEW, unconditionally. A live system
    running stale code cannot be called green.

Stdlib only (json, argparse, sys). Importable without side effects.

## Examples

    build_verdict.py --findings findings.json
        compute the terminal verdict from a JSON gate-state file; JSON to stdout
    build_verdict.py --findings findings.json --human
        human-readable verdict + reasons

## Environment

    (none)

## Exit codes

    0    verdict PASS or PASS_WITH_DEFERRALS (shippable)
    1    verdict NEEDS_REVIEW (human required)
    2    usage error (bad/missing input file, malformed JSON)
"""

import argparse
import json
import sys

PASS = "PASS"
PASS_WITH_DEFERRALS = "PASS_WITH_DEFERRALS"
NEEDS_REVIEW = "NEEDS_REVIEW"


def _is_open_blocking(check):
    return (str(check.get("severity", "")).upper() == "BLOCKING"
            and str(check.get("status", "open")).lower() != "resolved")


def compute_verdict(checks=None, deferrals=None, cap_hit=False, stale_guard=None):
    """Compute the terminal build verdict + a structured reason list.

    Returns:
      {
        "verdict": PASS|PASS_WITH_DEFERRALS|NEEDS_REVIEW,
        "shippable": bool,
        "reasons": [str, ...],                # why this verdict (never empty)
        "counts": {
          "open_blocking": int,
          "accepted_deferrals": int,
          "builder_emitted_deferrals": int,
        },
        "deferral_summary": "<N> accepted / <M> builder-emitted",
      }

    Pure: no I/O, no globals mutated. Same inputs → same output.
    """
    checks = checks or []
    deferrals = deferrals or []
    stale_guard = stale_guard or {}

    open_blocking = [c for c in checks if _is_open_blocking(c)]
    accepted = [d for d in deferrals
                if str(d.get("provenance", "")).lower() == "accepted"]
    builder_emitted = [d for d in deferrals
                       if str(d.get("provenance", "")).lower() == "builder_emitted"]
    stale = bool(stale_guard.get("triggered"))

    reasons = []

    # ── NEEDS_REVIEW conditions (any one forces it; fail-closed) ───────────────
    if stale:
        reasons.append(
            "stale_system_guard triggered — live system code_version != working tree"
            + (f": {stale_guard.get('detail')}" if stale_guard.get("detail") else "")
            + " — STALE SYSTEM, restart required before the result can be trusted"
        )
    if open_blocking:
        for c in open_blocking:
            reasons.append(f"BLOCKING unresolved: {c.get('name', '?')} — {c.get('detail', '')}")
    if builder_emitted:
        for d in builder_emitted:
            reasons.append(
                "builder-emitted deferral (NOT auto-honored): "
                f"{d.get('requirement', '?')} — {d.get('detail', '')}"
            )
    if cap_hit and (open_blocking or builder_emitted):
        reasons.append(
            "qa_iteration_cap reached with unresolved work — gate does NOT auto-defer to green"
        )

    counts = {
        "open_blocking": len(open_blocking),
        "accepted_deferrals": len(accepted),
        "builder_emitted_deferrals": len(builder_emitted),
    }
    deferral_summary = f"{len(accepted)} accepted / {len(builder_emitted)} builder-emitted"

    if stale or open_blocking or builder_emitted:
        verdict = NEEDS_REVIEW
    elif cap_hit and (open_blocking or builder_emitted):
        # (unreachable given the branch above, but kept explicit for clarity)
        verdict = NEEDS_REVIEW
    elif accepted:
        verdict = PASS_WITH_DEFERRALS
        for d in accepted:
            reasons.append(f"accepted (user-approved) deferral honored as DEFERRED: "
                           f"{d.get('requirement', '?')}")
    else:
        verdict = PASS
        reasons.append("all checks passed; zero deferrals; cap not hit; system not stale")

    return {
        "verdict": verdict,
        "shippable": verdict in (PASS, PASS_WITH_DEFERRALS),
        "reasons": reasons,
        "counts": counts,
        "deferral_summary": deferral_summary,
    }


def commit_subject(feature_name, result):
    """The honest commit subject the build skill writes — verdict + deferral counts
    surface in the subject line (blueprint §5.3 C4)."""
    return (f"chore: {feature_name} session log + qa artifacts "
            f"[{result['verdict']}: {result['deferral_summary']}]")


def main(argv=None):
    ap = argparse.ArgumentParser(
        prog="build_verdict.py",
        description="Compute the greenoke terminal build verdict (fail-closed).",
    )
    ap.add_argument("--findings", required=True,
                    help="JSON file: {checks, deferrals, cap_hit, stale_guard, feature?}")
    ap.add_argument("--human", action="store_true", help="human-readable report (default: JSON)")
    args = ap.parse_args(argv)

    try:
        with open(args.findings, encoding="utf-8") as f:
            data = json.load(f)
    except FileNotFoundError:
        print(f"ERROR: findings file not found at {args.findings}", file=sys.stderr)
        return 2
    except json.JSONDecodeError as e:
        print(f"ERROR: findings is not valid JSON: {e}", file=sys.stderr)
        return 2

    result = compute_verdict(
        checks=data.get("checks"),
        deferrals=data.get("deferrals"),
        cap_hit=bool(data.get("cap_hit")),
        stale_guard=data.get("stale_guard"),
    )
    if data.get("feature"):
        result["commit_subject"] = commit_subject(data["feature"], result)

    if args.human:
        print(f"verdict   : {result['verdict']} ({'shippable' if result['shippable'] else 'NOT shippable'})")
        print(f"deferrals : {result['deferral_summary']}")
        print(f"counts    : {json.dumps(result['counts'])}")
        print("reasons   :")
        for r in result["reasons"]:
            print(f"  - {r}")
        if result.get("commit_subject"):
            print(f"commit    : {result['commit_subject']}")
    else:
        print(json.dumps(result, indent=2))

    return 0 if result["shippable"] else 1


if __name__ == "__main__":
    sys.exit(main())
