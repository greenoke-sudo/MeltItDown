#!/usr/bin/env python3
"""Tests for verify_spec.py — clean PASS, and the NEEDS_REVIEW failure modes
(missing section, missing EARS, banned token, unresolved open question)."""

import os
import sys
import tempfile
import unittest

_HERE = os.path.dirname(os.path.abspath(__file__))
_TOOLING = os.path.normpath(os.path.join(_HERE, ".."))
for p in (_TOOLING, _HERE):
    if p not in sys.path:
        sys.path.insert(0, p)

import verify_spec  # noqa: E402
import _fixtures  # noqa: E402


def _write(text, suffix=".md"):
    fd, path = tempfile.mkstemp(suffix=suffix)
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        f.write(text)
    return path


class TestVerifySpec(unittest.TestCase):
    def test_clean_spec_passes(self):
        path = _write(_fixtures.CLEAN_SPEC)
        try:
            r = verify_spec.verify_spec(path)
            self.assertEqual(r["verdict"], "PASS",
                             msg=[c for c in r["checks"] if c["status"] == "FAIL"])
        finally:
            os.remove(path)

    def test_missing_section_needs_review(self):
        path = _write(_fixtures.spec_missing_section())
        try:
            r = verify_spec.verify_spec(path)
            self.assertEqual(r["verdict"], "NEEDS_REVIEW")
            names = {c["name"] for c in r["checks"] if c["status"] == "FAIL"}
            self.assertIn("sections_present", names)
        finally:
            os.remove(path)

    def test_missing_ears_needs_review(self):
        path = _write(_fixtures.spec_missing_ears())
        try:
            r = verify_spec.verify_spec(path)
            self.assertEqual(r["verdict"], "NEEDS_REVIEW")
            fails = {c["name"] for c in r["checks"] if c["status"] == "FAIL"}
            self.assertIn("states_have_ears", fails)
        finally:
            os.remove(path)

    def test_unresolved_open_question_needs_review(self):
        # add an OPEN bullet to §10 (before Resolved Decisions) → FAIL.
        bad = _fixtures.CLEAN_SPEC.replace(
            "## 10. Open Questions\n",
            "## 10. Open Questions\n\n- Should the grid paginate beyond 50 widgets?\n",
        )
        path = _write(bad)
        try:
            r = verify_spec.verify_spec(path)
            self.assertEqual(r["verdict"], "NEEDS_REVIEW")
            fails = {c["name"] for c in r["checks"] if c["status"] == "FAIL"}
            self.assertIn("open_questions_empty", fails)
        finally:
            os.remove(path)

    def test_core_banned_token_needs_review(self):
        # leak a class-name suffix into the overview → banned_tokens_absent FAIL.
        bad = _fixtures.CLEAN_SPEC.replace(
            "A gallery surface that lets",
            "The GalleryController surface that lets",
        )
        path = _write(bad)
        try:
            r = verify_spec.verify_spec(path)
            self.assertEqual(r["verdict"], "NEEDS_REVIEW")
            fails = {c["name"] for c in r["checks"] if c["status"] == "FAIL"}
            self.assertIn("banned_tokens_absent", fails)
        finally:
            os.remove(path)

    def test_adapter_banned_token_merged(self):
        # An ADAPTER-SUPPLIED project noun must trip the scan ONLY when the adapter
        # banned-tokens file is passed — proving the core merges core+adapter lists
        # without itself knowing any project noun. We synthesize a throwaway adapter
        # file with a neutral token ("frobnicator") so the core test stays noun-free.
        adapter_file = _write("# adapter nouns\nfrobnicator\n", suffix=".txt")
        bad = _fixtures.CLEAN_SPEC.replace(
            "browse saved widgets",
            "browse saved widgets via the frobnicator",
        )
        path = _write(bad)
        try:
            clean = verify_spec.verify_spec(path)  # no adapter list → not flagged
            bt_clean = [c for c in clean["checks"] if c["name"] == "banned_tokens_absent"][0]
            self.assertEqual(bt_clean["status"], "PASS")
            withadapter = verify_spec.verify_spec(path, adapter_file)
            self.assertEqual(withadapter["verdict"], "NEEDS_REVIEW")
            bt = [c for c in withadapter["checks"] if c["name"] == "banned_tokens_absent"][0]
            self.assertEqual(bt["status"], "FAIL")
            self.assertIn("frobnicator", bt["detail"])
        finally:
            os.remove(path)
            os.remove(adapter_file)


if __name__ == "__main__":
    unittest.main()
