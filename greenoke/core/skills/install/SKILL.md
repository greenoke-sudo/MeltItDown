---
name: install
description: Validate a greenoke adapter is correctly wired — manifest validates against the schema (via tooling/manifest_validate.py), referenced paths exist, the capability provider's health() is reachable, the knowledge base + rules are present. Prints a green/red/amber readiness CHECKLIST with an unblocking action per FAIL. Read-only. Project-agnostic.
user-invocable: true
---

# Install — validate the adapter wiring

You verify a project's greenoke adapter is correctly wired and ready to run `/greenoke:spec|plan|build`. You change **nothing** — you check and report a readiness checklist. Run after `/greenoke:init` (or after editing the adapter by hand).

This is a **concrete validator**, not prose. Run the steps; emit the checklist. Exit nonzero **only on hard config errors** (invalid manifest, missing required path, missing KB root) — a not-yet-built provider (M2 pending) is PENDING/amber, never a hard fail.

## Phase 0 — Config + locate the manifest

```bash
cat "${CLAUDE_PLUGIN_ROOT}/config.default.json"
# overlay greenoke/adapter/greenoke.config.json if present so the checklist
# reflects the EFFECTIVE config (note stale_system_guard + require_clean_git_before_build).
test -f greenoke/adapter/greenoke.adapter.json || echo "MISSING — run /greenoke:init first"
```

Manifest missing → fail the whole checklist with VERDICT: NOT-READY and "run `/greenoke:init` first."

## Phase 1 — Manifest schema validation + referenced paths (shared helper)

Call the **shared validator** — the same one `init` uses, so the logic lives in one place:

```bash
python3 "${CLAUDE_PLUGIN_ROOT}/tooling/manifest_validate.py" \
    greenoke/adapter/greenoke.adapter.json \
    --check-paths --repo-root . --json
```

The helper:
1. **Parses** the manifest as JSON.
2. **Validates** it against `${CLAUDE_PLUGIN_ROOT}/templates/adapter-manifest.schema.json`. It uses the `jsonschema` package when importable (full Draft-07) and a **stdlib structural fallback** otherwise — the JSON report's `validator` field names which ran (`jsonschema` | `structural-fallback`). The fallback covers exactly this schema's surface: required keys, types, enums, `uniqueItems`, `minItems`, `minLength`, `pattern`, `additionalProperties:false`.
3. With `--check-paths`, confirms every referenced path exists: `inputs.spec.dir`, `rules.dir`, `knowledge_base.dir` (required) and `templates.spec_fragments`/`plan_fragments`, `banned_tokens_source`, `verification.artifact_validator` (optional → missing is a warning, not a fail).

Read the report's `schema_valid`, `schema_errors`, `paths_ok`, and `paths[]`. A `schema_valid:false` or a missing **required** path → red. A missing **optional** path (e.g. `artifact_validator` before the provider exists) → amber.

Then confirm **`greenoke_version`** is satisfiable by the installed core (`cat greenoke/core/.claude-plugin/plugin.json` → compare the version to the manifest's range).

## Phase 2 — Capability provider reachability

From `capability_provider` in the manifest:

- **`kind: none`** → mark provider checks **N/A (no live system)**; the pipeline runs without proof-of-life verbs. Not a fail.
- **Provider declared but not yet implemented** (the launch target is a stub / file absent / `health()` returns "not implemented") → **PENDING (amber)**, with the note "provider not yet built (M2)". **Do NOT hard-fail.** This is the expected state before the provider milestone.
- **Otherwise, call `health()`** through the declared transport (`launch` argv / MCP / library entry) and read `{ reachable, code_version }`:
  - `reachable:true` with a `code_version` → green.
  - `reachable:false` with a hint → **amber** (self-healing discovery: the backend may be down — report the hint, not a hard fail).
  - response present but `code_version` absent/garbled → **red** (non-conformant: `stale_system_guard` can't function without `code_version`).
- For each declared verb, note addressability: a `verify`/`build`/`screenshot`/`inspect` that errors "not implemented" is amber with a note; one missing entirely from a *claimed-complete* provider is red.

## Phase 3 — Knowledge base + rules presence

```bash
KB=$(jq -r '.knowledge_base.dir' greenoke/adapter/greenoke.adapter.json)   # or read it directly
test -d "$KB"                 && echo "KB root OK"   || echo "KB root MISSING (red)"
test -f "$KB/ROADMAP.md"      ; test -f "$KB/work/INDEX.md"
for d in docs/capabilities docs/decisions docs/learnings docs/measurements research runbooks triage; do
  test -d "$KB/$d" || echo "missing $KB/$d (amber)"
done
RULES=$(jq -r '.rules.dir' greenoke/adapter/greenoke.adapter.json)
test -d "$RULES" && ls "$RULES"/*.md >/dev/null 2>&1 && echo "rules OK" || echo "no rule files (amber)"
```

Missing KB root → red. Missing sub-dirs or no rule files → amber (the pipeline runs; the researcher just has less to read).

## Phase 4 — Report the checklist

Emit a green/amber/red readiness checklist. Every item is PASS / FAIL / PENDING, and **every FAIL carries the exact unblocking action**:

```
greenoke install readiness — <project.name>

[PASS]    manifest parses + conforms to schema           (validator: <jsonschema|structural-fallback>)
[PASS]    referenced required paths exist
[~AMBER]  artifact_validator path absent                 → exists once the provider is built (M2)
[PASS]    greenoke_version satisfied by core <x.y.z>
[PENDING] capability provider health() reachable         → provider not yet built (M2); launch=<argv>
[PASS]    knowledge base present at <knowledge_base.dir>
[PASS]    rules dir has auto-loaded rule files
[PASS]    effective config: autonomy=<…>, stale_system_guard=<…>, qa_iteration_cap=<…>

VERDICT: READY | READY-WITH-WARNINGS | NOT-READY
```

- **READY** — all PASS (amber/pending allowed only on optional/declared-but-stub items).
- **READY-WITH-WARNINGS** — only amber/PENDING items remain (provider not yet built, provider down but self-healing, partial KB). The user can run `/greenoke:spec` and `/greenoke:plan` now, and `/greenoke:build` proof-of-life once the provider lands.
- **NOT-READY** — any red (manifest invalid, missing required path, non-conformant provider returning no `code_version`, missing KB root). List the exact fix per red item.

**Exit code discipline:** nonzero **only** for NOT-READY (hard config errors). READY and READY-WITH-WARNINGS exit zero — a pending provider must not block the spec/plan phases.

## Rules

- **Read-only** — install validates; it never edits the adapter or the KB. If something's wrong, report the fix and let the user (or `/greenoke:init`) apply it.
- **Shared validator** — manifest + path checks go through `tooling/manifest_validate.py`, never re-implemented inline, so init and install agree.
- **No project assumptions** — every checked path/value comes from the manifest.
- **Amber vs. red discipline** — a not-yet-built provider, a transiently-down (self-healing) provider, a declared-but-stub verb, or a missing optional path is a warning; an invalid manifest, a missing required path, or a `health()` with no `code_version` (from a provider that claims to be live) is a failure.
