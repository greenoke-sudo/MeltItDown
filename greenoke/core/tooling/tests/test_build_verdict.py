#!/usr/bin/env python3
"""Tests for build_verdict.py — the fail-closed terminal-verdict logic.

Covers: clean PASS; user-approved deferral → PASS_WITH_DEFERRALS; builder-emitted
deferral → NEEDS_REVIEW (provenance split); cap-hit with unresolved → NEEDS_REVIEW;
stale-guard mismatch → NEEDS_REVIEW (halt); open BLOCKING → NEEDS_REVIEW; the
commit subject surfaces verdict + deferral counts."""

import os
import sys
import unittest

_HERE = os.path.dirname(os.path.abspath(__file__))
_TOOLING = os.path.normpath(os.path.join(_HERE, ".."))
if _TOOLING not in sys.path:
    sys.path.insert(0, _TOOLING)

import build_verdict as bv  # noqa: E402


class TestBuildVerdict(unittest.TestCase):
    def test_clean_pass(self):
        r = bv.compute_verdict(checks=[], deferrals=[], cap_hit=False, stale_guard=None)
        self.assertEqual(r["verdict"], "PASS")
        self.assertTrue(r["shippable"])
        self.assertEqual(r["counts"]["open_blocking"], 0)

    def test_resolved_blocking_is_not_open(self):
        r = bv.compute_verdict(checks=[
            {"name": "grid_renders", "severity": "BLOCKING", "status": "resolved"},
        ])
        self.assertEqual(r["verdict"], "PASS")

    def test_minor_findings_do_not_block(self):
        r = bv.compute_verdict(checks=[
            {"name": "spacing", "severity": "MINOR", "status": "open"},
        ])
        self.assertEqual(r["verdict"], "PASS")

    def test_open_blocking_needs_review(self):
        r = bv.compute_verdict(checks=[
            {"name": "dismiss_transition", "severity": "BLOCKING", "status": "open",
             "detail": "dismiss did not return to grid"},
        ])
        self.assertEqual(r["verdict"], "NEEDS_REVIEW")
        self.assertFalse(r["shippable"])
        self.assertTrue(any("dismiss_transition" in x for x in r["reasons"]))

    def test_user_approved_deferral_pass_with_deferrals(self):
        r = bv.compute_verdict(deferrals=[
            {"provenance": "accepted", "requirement": "pagination beyond 50",
             "detail": "user approved skipping in plan Q&A"},
        ])
        self.assertEqual(r["verdict"], "PASS_WITH_DEFERRALS")
        self.assertTrue(r["shippable"])
        self.assertEqual(r["counts"]["accepted_deferrals"], 1)

    def test_builder_emitted_deferral_needs_review(self):
        # PROVENANCE SPLIT: a builder "couldn't finish" deferral is NOT auto-honored.
        r = bv.compute_verdict(deferrals=[
            {"provenance": "builder_emitted", "requirement": "detail dismiss",
             "detail": "couldn't wire dismiss in-pipeline"},
        ])
        self.assertEqual(r["verdict"], "NEEDS_REVIEW")
        self.assertFalse(r["shippable"])
        self.assertEqual(r["counts"]["builder_emitted_deferrals"], 1)
        self.assertTrue(any("builder-emitted" in x for x in r["reasons"]))

    def test_mixed_deferrals_builder_wins_to_needs_review(self):
        r = bv.compute_verdict(deferrals=[
            {"provenance": "accepted", "requirement": "pagination"},
            {"provenance": "builder_emitted", "requirement": "dismiss"},
        ])
        self.assertEqual(r["verdict"], "NEEDS_REVIEW")
        # both provenance buckets counted, never conflated
        self.assertEqual(r["counts"]["accepted_deferrals"], 1)
        self.assertEqual(r["counts"]["builder_emitted_deferrals"], 1)

    def test_cap_hit_with_unresolved_blocking_needs_review(self):
        r = bv.compute_verdict(
            checks=[{"name": "empty_state", "severity": "BLOCKING", "status": "open"}],
            cap_hit=True,
        )
        self.assertEqual(r["verdict"], "NEEDS_REVIEW")
        self.assertTrue(any("does NOT auto-defer" in x or "BLOCKING" in x for x in r["reasons"]))

    def test_cap_hit_with_only_accepted_is_shippable(self):
        # cap reached but the only remaining work is user-approved → still shippable.
        r = bv.compute_verdict(
            deferrals=[{"provenance": "accepted", "requirement": "pagination"}],
            cap_hit=True,
        )
        self.assertEqual(r["verdict"], "PASS_WITH_DEFERRALS")
        self.assertTrue(r["shippable"])

    def test_stale_guard_halts_the_gate(self):
        # stale_system_guard mismatch → NEEDS_REVIEW unconditionally, even with no
        # other findings. Proves a code_version mismatch halts the gate.
        r = bv.compute_verdict(
            checks=[],
            deferrals=[],
            cap_hit=False,
            stale_guard={"triggered": True,
                         "detail": "health a1b2c3-dirty != tree d4e5f6"},
        )
        self.assertEqual(r["verdict"], "NEEDS_REVIEW")
        self.assertFalse(r["shippable"])
        self.assertTrue(any("stale" in x.lower() for x in r["reasons"]))

    def test_stale_guard_overrides_accepted_deferrals(self):
        r = bv.compute_verdict(
            deferrals=[{"provenance": "accepted", "requirement": "pagination"}],
            stale_guard={"triggered": True, "detail": "stamp mismatch"},
        )
        self.assertEqual(r["verdict"], "NEEDS_REVIEW")

    def test_commit_subject_carries_verdict_and_counts(self):
        r = bv.compute_verdict(deferrals=[
            {"provenance": "accepted", "requirement": "pagination"},
        ])
        subj = bv.commit_subject("widget-gallery", r)
        self.assertIn("PASS_WITH_DEFERRALS", subj)
        self.assertIn("1 accepted / 0 builder-emitted", subj)

    def test_needs_review_subject_for_builder_emitted(self):
        r = bv.compute_verdict(deferrals=[
            {"provenance": "builder_emitted", "requirement": "dismiss"},
        ])
        subj = bv.commit_subject("widget-gallery", r)
        self.assertIn("NEEDS_REVIEW", subj)
        self.assertIn("0 accepted / 1 builder-emitted", subj)


if __name__ == "__main__":
    unittest.main()
