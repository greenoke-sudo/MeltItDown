#!/usr/bin/env python3
"""Tests for verify_plan.py — clean PASS (with spec coverage), and NEEDS_REVIEW
modes (missing section, slice missing a field, uncovered spec state, uncovered
transition, unresolved open question)."""

import os
import sys
import tempfile
import unittest

_HERE = os.path.dirname(os.path.abspath(__file__))
_TOOLING = os.path.normpath(os.path.join(_HERE, ".."))
for p in (_TOOLING, _HERE):
    if p not in sys.path:
        sys.path.insert(0, p)

import verify_plan  # noqa: E402
import _fixtures  # noqa: E402


def _write(text):
    fd, path = tempfile.mkstemp(suffix=".md")
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        f.write(text)
    return path


class TestVerifyPlan(unittest.TestCase):
    def setUp(self):
        self.spec_path = _write(_fixtures.CLEAN_SPEC)

    def tearDown(self):
        os.remove(self.spec_path)

    def test_clean_plan_passes_with_spec(self):
        path = _write(_fixtures.CLEAN_PLAN)
        try:
            r = verify_plan.verify_plan(path, self.spec_path)
            self.assertEqual(r["verdict"], "PASS",
                             msg=[c for c in r["checks"] if c["status"] == "FAIL"])
        finally:
            os.remove(path)

    def test_missing_section_needs_review(self):
        # drop §4 State Machine Implementation
        out, skip = [], False
        for line in _fixtures.CLEAN_PLAN.splitlines():
            if line.startswith("## 4. State Machine Implementation"):
                skip = True
                continue
            if skip and line.startswith("## 5. "):
                skip = False
            if not skip:
                out.append(line)
        path = _write("\n".join(out))
        try:
            r = verify_plan.verify_plan(path, self.spec_path)
            self.assertEqual(r["verdict"], "NEEDS_REVIEW")
            fails = {c["name"] for c in r["checks"] if c["status"] == "FAIL"}
            self.assertIn("sections_present", fails)
        finally:
            os.remove(path)

    def test_slice_missing_field_needs_review(self):
        # remove the Dependencies line from Slice 2
        bad = _fixtures.CLEAN_PLAN.replace(
            "- **Dependencies:** Slice 1\n", "", 1)
        path = _write(bad)
        try:
            r = verify_plan.verify_plan(path, self.spec_path)
            self.assertEqual(r["verdict"], "NEEDS_REVIEW")
            fails = {c["name"] for c in r["checks"] if c["status"] == "FAIL"}
            self.assertIn("slices_complete", fails)
        finally:
            os.remove(path)

    def test_uncovered_spec_transition_needs_review(self):
        # delete the dismiss transition row from plan §4 → transitions_cover_spec FAIL
        bad = _fixtures.CLEAN_PLAN.replace(
            "| Detail.Open | Gallery.Populated | on_dismiss callback | pop detail_view | detail_view |\n",
            "",
        )
        path = _write(bad)
        try:
            r = verify_plan.verify_plan(path, self.spec_path)
            self.assertEqual(r["verdict"], "NEEDS_REVIEW")
            fails = {c["name"] for c in r["checks"] if c["status"] == "FAIL"}
            self.assertIn("transitions_cover_spec", fails)
        finally:
            os.remove(path)

    def test_uncovered_spec_state_needs_review(self):
        # remove the Detail surface spec text from plan §6 → states_cover_spec FAIL
        bad = _fixtures.CLEAN_PLAN.replace(
            "### Detail\nOwning unit detail_view. State: Open. Shows the selected widget full size.\n",
            "",
        )
        path = _write(bad)
        try:
            r = verify_plan.verify_plan(path, self.spec_path)
            self.assertEqual(r["verdict"], "NEEDS_REVIEW")
            fails = {c["name"] for c in r["checks"] if c["status"] == "FAIL"}
            self.assertIn("states_cover_spec", fails)
        finally:
            os.remove(path)

    def test_unresolved_open_question_needs_review(self):
        bad = _fixtures.CLEAN_PLAN.replace(
            "## 11. Open Questions\n",
            "## 11. Open Questions\n\n- Which store namespace owns the widget list?\n",
        )
        path = _write(bad)
        try:
            r = verify_plan.verify_plan(path, self.spec_path)
            self.assertEqual(r["verdict"], "NEEDS_REVIEW")
            fails = {c["name"] for c in r["checks"] if c["status"] == "FAIL"}
            self.assertIn("open_questions_empty", fails)
        finally:
            os.remove(path)


if __name__ == "__main__":
    unittest.main()
