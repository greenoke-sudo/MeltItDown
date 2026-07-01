---
name: spec
description: Build the feature spec — config + manifest discovery → question scouts → user Q&A drain → spec writers → judge → verify → gate → commit. Manifest-driven, project-agnostic. Produces a design-language spec with EARS acceptance criteria.
user-invocable: true
effort: max
---

# Spec (Tri Orchestrator)

You orchestrate the spec-writing phase. Question scouts surface design-level ambiguities; you drain them with the user; spec writers produce candidates; one judge synthesizes the final. Scout/writer counts are config-driven. **Everything project-specific is resolved through the adapter manifest** — this skill names no project, language, or tool.

This skill produces `<run_dir>/<run-id>/spec/reports/feature-spec.md`.

## Arguments / User Notes

The skill accepts **freeform user notes** at invocation — anything typed after `/greenoke:spec`. Treat as authoritative design context: they may describe the feature, point at where material lives, or override discovered files. When notes contradict a discovered file, notes win unless you explicitly ask. No notes → discovery alone drives the run.

## Phase 0 — Config + Manifest Discovery

**No path in this skill is hardcoded. Every path is resolved from the adapter manifest.**

### 0.0 Read config
`cat "${CLAUDE_PLUGIN_ROOT}/config.default.json"` and parse. Then, if `greenoke/adapter/greenoke.config.json` exists, overlay its keys (adapter override shadows individual keys; missing file/key → core default). Pull `scout_count` (default 2), `writer_count` (default 2), `autonomy` (default `"full"`). Carry these into later phases.

### 0.1 Resolve the manifest
Read `greenoke/adapter/greenoke.adapter.json`. If missing, hard-fail with: "no adapter manifest found — run `/greenoke:init` first." From it resolve:
- `inputs.spec.dir` and `inputs.spec.kind` — where briefs live and how to read them.
- `inputs.spec.required` — whether an empty input dir is a hard-fail.
- `templates.spec_fragments` — the adapter's spec sections to splice into the core template.
- `banned_tokens_source` — adapter project nouns to add to the core banned-token list.
- `knowledge_base.dir`, `run_dir`, `project.name`.

**Compute the run id** (e.g. `spec-<timestamp>` or `<project.name>-<timestamp>`) and the run root `<run_dir>/<run-id>/spec/`. Create `<run-root>/{scouts,candidates,reports,logs}`.

### 0.2 Capture user notes
Save invocation notes verbatim to `<run-root>/logs/user-notes.md` (`(no user notes)` if empty). This file is passed to every subagent.

### 0.3 Enumerate inputs (via the manifest)
List `inputs.spec.dir` and bucket its contents per `inputs.spec.kind` (the adapter defines the shape — a markdown brief, a flow export, structured files; the core only enforces *presence*, the adapter defines *what*). Write `<run-root>/logs/discovery.md` summarizing every discovered artifact + the user notes.

### 0.4 Validate & decide
- **Hard-fail (abort with an unblocking message)** if `inputs.spec.required` is true and `inputs.spec.dir` is missing or empty. The message must name the action ("drop a feature brief into `<inputs.spec.dir>`"). The *enforcement* is core; the *what* is manifest-defined.
- **Ask the user** via `AskUserQuestion` when inputs are present but ambiguous (multiple competing briefs, notes describing a different feature than the brief). 2–4 concrete options; apply the answer.
- **Proceed** when an input is present (or notes plus an input together describe the feature).

### 0.5 Assemble the template
Read the core skeleton `${CLAUDE_PLUGIN_ROOT}/templates/feature-spec.core.md`. If `templates.spec_fragments` exists, splice its content at the `<!-- INJECT: adapter spec_fragments here -->` marker. Write the assembled template to `<run-root>/logs/feature-spec.assembled.md`. Build the **effective banned-token list** = core list + (the `banned_tokens_source` file, if present). Hold both for writers and the verifier.

### 0.6 Feature name
Derive the feature name from the brief (or `inputs.spec.kind`-appropriate field). Lowercase kebab-case for the commit. If generic/missing, confirm via `AskUserQuestion`.

## Phase 1 — Question Scouting (parallel ×scout_count)

Spawn **`scout_count` `greenoke:spec-question-scout` agents in parallel** (single message, one `Agent` call per scout, `subagent_type: greenoke:spec-question-scout`). Each gets: the discovery summary path, the user-notes path, every discovered artifact path, and the instruction to return design-level questions only. Save each reply verbatim to `<run-root>/scouts/scout-<n>.md`.

## Phase 2 — Drain Questions with User

1. **Merge** scout outputs; dedupe; keep differing perspectives.
2. **Triage** — drop any implementation-level leakage (a scout shouldn't have produced it).
3. **Ask** via `AskUserQuestion`, batches of ≤4. Each: question + 1-line "why it matters", `header` ≤12 chars, 2–4 options with the best guess first labeled `(Recommended)`.
4. **Re-evaluate after each round** — answers may resolve or surface questions. Loop until none remain.
5. **Write Q&A** verbatim to `<run-root>/logs/qa.md` under `## Resolved Decisions`.

**This is the upfront ambiguity drain.** Do not self-write qa.md from best-guess defaults — surface every question, recommend, let the user pick. (In `autonomy=interactive` this is unchanged; the `AskUserQuestion` calls ARE the gate and are not suppressed by any "no-prompt" directive.)

## Phase 3 — Spec Writing (parallel ×writer_count)

Spawn **`writer_count` `greenoke:spec-writer` agents in parallel** (`subagent_type: greenoke:spec-writer`). Each gets: the **assembled** template path, output path `<run-root>/candidates/candidate-<n>.md`, discovery + user-notes paths, all input paths, the full resolved Q&A, and the effective banned-token list. Each §6 state must carry EARS acceptance criteria in design language.

## Phase 4 — Judge

Spawn **1 `greenoke:spec-judge`** (`subagent_type: greenoke:spec-judge`) with: all candidate paths, the Q&A path, user-notes path, discovery path, assembled template path, final output path `<run-root>/reports/feature-spec.md`, and source input paths. Save its report to `<run-root>/logs/judge-report.md`.

## Phase 5 — Escape Hatch (residue questions)

If the judge's report lists residue (final §10 non-empty): run another `AskUserQuestion` round (batches of 4), apply each answer via `Edit` (remove from §10, append under §10 "Resolved Decisions", edit affected sections), and append the pairs to qa.md. If §10 is empty, skip.

## Phase 6 — Verify (run the real verifier — DO NOT eyeball it)

Run the **runnable** spec verifier — it is the executable form of this contract, one place the checks live. Do NOT hand-grade the spec; call the tool and gate on its JSON verdict:

```
"${CLAUDE_PLUGIN_ROOT}/tooling/verify_spec.py" \
    "<run-root>/reports/feature-spec.md" \
    --banned-tokens "<banned_tokens_source resolved in 0.5>"
```

(Omit `--banned-tokens` only if the manifest declares no `banned_tokens_source`.) It emits `{"verdict": "PASS"|"NEEDS_REVIEW", "checks": [{name,status,detail}], "spec": "<path>"}` and **exits 0 on PASS, 1 on NEEDS_REVIEW**. Its checks: core sections 1–11 present + ordered (adapter §A allowed between §9 and §10); §5 every surface has a States count; **every §5 surface has ≥1 §6 state subsection AND every §6 state carries an `Acceptance Criteria (EARS)` field with `SHALL` phrasing**; §7 every transition row is EARS-phrased; §8 all 5 / §9 all 4 sub-headings (no TODO/TBD/?); §10 Open Questions empty-or-escalated; **banned-token scan** over the effective list (core + adapter `banned_tokens_source`).

For each FAILED check in the JSON, fix the spec via `Edit`, then **re-run the verifier** (never edit the verdict by hand). After 3 fix attempts on one still-failing check, log it `UNRESOLVED` and proceed. Save the final verifier JSON + your fix notes to `<run-root>/logs/verification-log.md`.

Source: `${CLAUDE_PLUGIN_ROOT}/tooling/verify_spec.py` (stdlib-only, fast, importable). It is unit-tested under `tooling/tests/test_verify_spec.py`.

## Phase 7 — Gate + Commit

### 7.0 Fail-closed gate
The gate consumes the **verifier's verdict** from Phase 6, not a self-assessment. If the final `verify_spec.py` run returned `verdict: NEEDS_REVIEW` (or any check is still `UNRESOLVED` after 3 fix attempts):
- **autonomy=full** → emit **NEEDS_REVIEW** and **HALT**. Do NOT commit. Output every failing/unresolved check + its `detail` line from the verifier JSON.
- **autonomy=interactive** → `AskUserQuestion` with the failing checks + their `detail`; per item accept-and-commit / fix-directive / halt. Apply the choice.

Only when the verifier returns `verdict: PASS` (exit 0) and zero `UNRESOLVED` remain, proceed. **Fail-closed: a NEEDS_REVIEW spec is never committed in `full`.**

### 7.1 Commit
Stage `<run-root>/reports/feature-spec.md` and `<run-root>/`. Commit (no signature, no co-author):
```
spec: <feature-name>
```

## Phase 8 — Export Session Log

Export this session to `<run-root>/logs/master.md` via `${CLAUDE_PLUGIN_ROOT}/tooling/export-agent-logs.py` (two-step stamp/export, include subagents) where the host supports it; otherwise note the transcript path.

## Rules

- **Manifest-driven, no hardcoded paths.** Every input/output path comes from the manifest or the computed run root.
- **Design language only** — the verifier enforces the effective banned-token list even if a writer slipped.
- **Workers are one-shot** — pass full context in each prompt; never resume a subagent.
- **Spawn agents by FQ id** — `subagent_type: greenoke:<agent>`.
- **Autonomy-gated prompting** — `AskUserQuestion` only at the defined drain/escape/gate points; the gate fails closed in `full`.
