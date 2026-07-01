#!/usr/bin/env python3
"""greenoke — shared adapter-manifest validator.

ONE place the validation logic lives, called by both /greenoke:init (sanity
check after writing the manifest) and /greenoke:install (the readiness gate).
Project-agnostic: it knows the schema, never a project's nouns.

Validates a `greenoke.adapter.json` against
`templates/adapter-manifest.schema.json` and optionally checks that the paths
the manifest references exist on disk.

Strategy:
  1. If the `jsonschema` package is importable, use it (full Draft-07 validation).
  2. Otherwise fall back to a stdlib structural validator that covers exactly the
     constraints this schema uses: required keys, type checks, enums, uniqueItems,
     minItems, minLength, and `additionalProperties: false`. The fallback is a
     subset of JSON Schema but a complete check of THIS schema's surface.

Stdlib only (json, argparse, os, sys, re); `jsonschema` is optional.

## Examples

    manifest_validate.py path/to/greenoke.adapter.json
        validate a manifest against the bundled schema; JSON report to stdout
    manifest_validate.py path/to/greenoke.adapter.json --check-paths --repo-root .
        also verify every referenced path exists, relative to repo-root
    manifest_validate.py manifest.json --schema custom.schema.json
        validate against an explicit schema file

## Environment

    (none)

## Exit codes

    0    manifest is valid (and, with --check-paths, all paths exist)
    1    manifest is invalid, unparseable, or a referenced path is missing
    2    usage error (bad arguments, schema not found)
"""

import argparse
import json
import os
import re
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
_DEFAULT_SCHEMA = os.path.normpath(
    os.path.join(_HERE, "..", "templates", "adapter-manifest.schema.json")
)


# ──────────────────────────────────────────────────────────────────────────────
# Fallback structural validator (used only when `jsonschema` is unavailable).
# Covers the keyword subset THIS schema uses. Not a general JSON Schema engine.
# ──────────────────────────────────────────────────────────────────────────────

_TYPE_MAP = {
    "object": dict,
    "array": list,
    "string": str,
    "boolean": bool,
    # JSON has no int/number distinction need here; not used by this schema.
}


def _type_ok(value, expected):
    py = _TYPE_MAP.get(expected)
    if py is None:
        return True  # unknown/unconstrained type → don't reject
    if expected == "boolean":
        return isinstance(value, bool)
    # bool is a subclass of int but not of the types we map; safe as-is.
    return isinstance(value, py)


def _structural_validate(instance, schema, path, errors):
    """Recursive subset validator. Appends `field: message` strings to errors."""
    where = path or "(root)"

    # type
    expected_type = schema.get("type")
    if expected_type and not _type_ok(instance, expected_type):
        errors.append(f"{where}: expected type '{expected_type}', got '{type(instance).__name__}'")
        return  # further checks assume the right shape

    if expected_type == "object" or isinstance(instance, dict):
        if isinstance(instance, dict):
            props = schema.get("properties", {})
            required = schema.get("required", [])
            for key in required:
                if key not in instance:
                    errors.append(f"{where}: missing required key '{key}'")
            if schema.get("additionalProperties", True) is False:
                allowed = set(props.keys())
                for key in instance:
                    if key not in allowed:
                        errors.append(f"{where}: unexpected key '{key}' (additionalProperties: false)")
            for key, subschema in props.items():
                if key in instance:
                    child = f"{path}.{key}" if path else key
                    _structural_validate(instance[key], subschema, child, errors)

    if expected_type == "array" or isinstance(instance, list):
        if isinstance(instance, list):
            if "minItems" in schema and len(instance) < schema["minItems"]:
                errors.append(f"{where}: needs at least {schema['minItems']} item(s)")
            if schema.get("uniqueItems") and len(instance) != len({json.dumps(i, sort_keys=True) for i in instance}):
                errors.append(f"{where}: items must be unique")
            item_schema = schema.get("items")
            if isinstance(item_schema, dict):
                for idx, item in enumerate(instance):
                    _structural_validate(item, item_schema, f"{path}[{idx}]", errors)

    if isinstance(instance, str):
        if "minLength" in schema and len(instance) < schema["minLength"]:
            errors.append(f"{where}: must be at least {schema['minLength']} char(s)")
        if "pattern" in schema and not re.search(schema["pattern"], instance):
            errors.append(f"{where}: '{instance}' does not match pattern {schema['pattern']}")

    if "enum" in schema and instance not in schema["enum"]:
        errors.append(f"{where}: '{instance}' not in allowed values {schema['enum']}")


def validate_manifest(manifest, schema):
    """Return (ok: bool, errors: list[str], validator_name: str)."""
    try:
        import jsonschema  # type: ignore

        validator = jsonschema.Draft7Validator(schema)
        errors = []
        for err in sorted(validator.iter_errors(manifest), key=lambda e: list(e.path)):
            loc = ".".join(str(p) for p in err.path) or "(root)"
            errors.append(f"{loc}: {err.message}")
        return (len(errors) == 0, errors, "jsonschema")
    except ImportError:
        errors = []
        _structural_validate(manifest, schema, "", errors)
        return (len(errors) == 0, errors, "structural-fallback")


# ──────────────────────────────────────────────────────────────────────────────
# Referenced-path existence check (optional; install uses it).
# ──────────────────────────────────────────────────────────────────────────────

def check_referenced_paths(manifest, repo_root):
    """Return (ok, results) where results is a list of (label, relpath, exists)."""
    results = []

    def rel(p):
        return os.path.normpath(os.path.join(repo_root, p))

    def add(label, p, required):
        if p is None:
            return
        results.append((label, p, os.path.exists(rel(p)), required))

    add("inputs.spec.dir", manifest.get("inputs", {}).get("spec", {}).get("dir"), True)
    add("rules.dir", manifest.get("rules", {}).get("dir"), True)
    add("knowledge_base.dir", manifest.get("knowledge_base", {}).get("dir"), True)
    tmpl = manifest.get("templates", {})
    add("templates.spec_fragments", tmpl.get("spec_fragments"), False)
    add("templates.plan_fragments", tmpl.get("plan_fragments"), False)
    add("banned_tokens_source", manifest.get("banned_tokens_source"), False)
    add("verification.artifact_validator",
        manifest.get("verification", {}).get("artifact_validator"), False)

    # A missing REQUIRED path is a hard failure; a missing optional one is a warning.
    ok = all(exists for (_l, _p, exists, required) in results if required)
    return ok, results


def main(argv=None):
    ap = argparse.ArgumentParser(
        prog="manifest_validate.py",
        description="Validate a greenoke.adapter.json against the adapter manifest schema.",
    )
    ap.add_argument("manifest", help="path to greenoke.adapter.json")
    ap.add_argument("--schema", default=_DEFAULT_SCHEMA, help="schema file (default: bundled)")
    ap.add_argument("--check-paths", action="store_true",
                    help="also verify referenced paths exist on disk")
    ap.add_argument("--repo-root", default=".",
                    help="root the manifest's relative paths resolve against (default: .)")
    ap.add_argument("--json", action="store_true", help="emit a JSON report (default: human text)")
    args = ap.parse_args(argv)

    if not os.path.isfile(args.schema):
        print(f"ERROR: schema not found at {args.schema}", file=sys.stderr)
        return 2

    try:
        with open(args.manifest, encoding="utf-8") as f:
            manifest = json.load(f)
    except FileNotFoundError:
        print(f"ERROR: manifest not found at {args.manifest}", file=sys.stderr)
        return 1
    except json.JSONDecodeError as e:
        print(f"ERROR: manifest is not valid JSON: {e}", file=sys.stderr)
        return 1

    with open(args.schema, encoding="utf-8") as f:
        schema = json.load(f)

    ok, errors, validator_name = validate_manifest(manifest, schema)

    path_ok = True
    path_results = []
    if args.check_paths:
        path_ok, path_results = check_referenced_paths(manifest, args.repo_root)

    report = {
        "manifest": args.manifest,
        "validator": validator_name,
        "schema_valid": ok,
        "schema_errors": errors,
        "paths_checked": args.check_paths,
        "paths_ok": path_ok,
        "paths": [
            {"label": l, "path": p, "exists": e, "required": r}
            for (l, p, e, r) in path_results
        ],
    }

    if args.json:
        print(json.dumps(report, indent=2))
    else:
        print(f"manifest : {args.manifest}")
        print(f"validator: {validator_name}")
        print(f"schema   : {'VALID' if ok else 'INVALID'}")
        for e in errors:
            print(f"  - {e}")
        if args.check_paths:
            print("paths    :")
            for (l, p, exists, required) in path_results:
                tag = "OK " if exists else ("MISSING(required)" if required else "missing(optional)")
                print(f"  [{tag}] {l} -> {p}")

    return 0 if (ok and path_ok) else 1


if __name__ == "__main__":
    sys.exit(main())
