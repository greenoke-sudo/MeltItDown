#!/usr/bin/env python3
"""greenoke artifact validator for the MeltItDown Unity provider.

A standalone, stdlib-only well-formedness checker the provider's `verify()` drives
(and the build() temp->verify->swap gate calls on a captured file). Two artifact
kinds are recognized by extension/content:

  * NUnit results XML (Unity Test Runner output, e.g. .greenoke-run/test-results.xml)
      -> parses the <test-run .../> root; usable iff it parses AND reports
         failed == 0 (and at least one test ran).
  * PNG screenshot (the screenshot/capture artifact)
      -> usable iff the file exists, is non-empty, and starts with the PNG magic
         signature.

Anything else: usable iff it exists and is non-empty (a weak fallback).

## Examples

    validate_artifact.py .greenoke-run/test-results.xml
    validate_artifact.py .greenoke-run/screenshot.png

## Exit codes

    0    artifact is well-formed / usable
    1    artifact is missing / empty / malformed / has test failures
    2    usage error (no path given)
"""

import json
import os
import sys
import xml.etree.ElementTree as ET

_PNG_MAGIC = b"\x89PNG\r\n\x1a\n"


def _basic_file_checks(path: str):
    """Return (ok, detail) for existence + non-emptiness, else (None, None) to
    continue to type-specific checks."""
    if not path or not os.path.isfile(path):
        return False, "artifact_missing"
    try:
        if os.path.getsize(path) <= 0:
            return False, "artifact_empty"
    except OSError:
        return False, "artifact_unreadable"
    return None, None


def validate_png(path: str) -> dict:
    """A usable PNG: exists, non-empty, and begins with the PNG magic signature."""
    ok, detail = _basic_file_checks(path)
    if ok is False:
        return {"ok": False, "kind": "png", "artifact": path, "detail": detail, "error": detail}
    try:
        with open(path, "rb") as f:
            head = f.read(len(_PNG_MAGIC))
    except OSError as e:
        return {"ok": False, "kind": "png", "artifact": path,
                "detail": f"unreadable: {e}", "error": "png_unreadable"}
    if head != _PNG_MAGIC:
        return {"ok": False, "kind": "png", "artifact": path,
                "detail": "not a PNG (bad magic signature)", "error": "png_bad_magic"}
    return {"ok": True, "kind": "png", "artifact": path,
            "detail": f"valid PNG ({os.path.getsize(path)} bytes)", "error": None}


def validate_nunit_xml(path: str) -> dict:
    """A usable NUnit results file: parses AND failed==0 AND total>0. Reads the
    standard NUnit3 <test-run total="" passed="" failed="" ...> attributes, falling
    back to counting <test-case result="..."> when the summary attrs are absent."""
    ok, detail = _basic_file_checks(path)
    if ok is False:
        return {"ok": False, "kind": "nunit", "artifact": path, "detail": detail, "error": detail}
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as e:
        return {"ok": False, "kind": "nunit", "artifact": path,
                "detail": f"XML parse error: {e}", "error": "xml_unparseable"}

    def _attr_int(node, name):
        try:
            return int(node.get(name)) if node is not None and node.get(name) is not None else None
        except (TypeError, ValueError):
            return None

    total = _attr_int(root, "total")
    failed = _attr_int(root, "failed")
    passed = _attr_int(root, "passed")

    if total is None or failed is None:
        # Fallback: count <test-case> elements and their result attribute.
        cases = root.findall(".//test-case")
        if cases:
            total = len(cases)
            failed = sum(1 for c in cases
                         if (c.get("result") or "").lower() in ("failed", "error"))
            passed = total - failed

    if total is None:
        return {"ok": False, "kind": "nunit", "artifact": path,
                "detail": "no test-run summary or test-case nodes found",
                "error": "no_results"}
    if total == 0:
        return {"ok": False, "kind": "nunit", "artifact": path,
                "detail": "zero tests ran", "error": "zero_tests"}
    ok = (failed == 0)
    return {"ok": ok, "kind": "nunit", "artifact": path,
            "detail": f"{passed if passed is not None else '?'} passed, {failed} failed, {total} total",
            "error": None if ok else "test_failures"}


def validate_artifact(path: str) -> dict:
    """Dispatch on extension/content. Returns a {ok, kind, artifact, detail, error}
    dict. Used by the provider's verify()/build() gate."""
    ok, detail = _basic_file_checks(path)
    if ok is False:
        return {"ok": False, "kind": "unknown", "artifact": path, "detail": detail, "error": detail}
    low = (path or "").lower()
    if low.endswith(".png"):
        return validate_png(path)
    if low.endswith(".xml"):
        return validate_nunit_xml(path)
    # Content sniff: PNG magic regardless of extension.
    try:
        with open(path, "rb") as f:
            if f.read(len(_PNG_MAGIC)) == _PNG_MAGIC:
                return validate_png(path)
    except OSError:
        pass
    return {"ok": True, "kind": "file", "artifact": path,
            "detail": f"exists, non-empty ({os.path.getsize(path)} bytes)", "error": None}


def main(argv):
    if not argv:
        print(json.dumps({"ok": False, "error": "usage: validate_artifact.py <path>"}),
              file=sys.stderr)
        return 2
    path = argv[0]
    report = validate_artifact(path)
    report["checks"] = [{"name": f"{report['kind']}_wellformed",
                         "ok": report["ok"], "detail": report["detail"]}]
    print(json.dumps(report, indent=2))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
