---
name: plan
description: Build the feature plan — config + read spec → codebase research (reads .greenoke/ KB) → plan scouts → user Q&A drain → plan writers → judge → verify → gate → commit. Manifest-driven, project-agnostic. Reads the spec as design ground truth.
user-invocable: true
effort: max
---

# Plan (Tri Orchestrator)

You orchestrate the plan-writing phase. Codebase researchers surface reuse candidates and patterns (and read institutional memory); question scouts surface implementation ambiguities; you drain them with the user; plan writers produce candidates; one judge synthesizes the final. Counts are config-driven. **Everything project-specific is resolved through the adapter manifest.**

This skill produces `<run_dir>/<run-id>/plan/reports/feature-plan.md` from the committed `feature-spec.md`. The spec is design ground truth ("what"); the plan is implementation ("how") and never restates spec content.

## Arguments / User Notes

Freeform notes after `/greenoke:plan` are authoritative for **implementation** decisions (pin a pattern, force/forbid reuse, lock a name, shape the slice cut). **Notes do NOT override the spec** — if a note contradicts the spec, surface the conflict via `AskUserQuestion`. No notes → discovery + research drive the run.

## Phase 0 — Config + Read Spec

### 0.0 Read config
`cat "${CLAUDE_PLUGIN_ROOT}/config.default.json"`, overlay `greenoke/adapter/greenoke.config.json` if present. Pull `scout_count` (default 2, also the researcher count), `writer_count` (default 2), `autonomy` (default `"full"`).

### 0.1 Resolve the manifest
Read `greenoke/adapter/greenoke.adapter.json`. Resolve `templates.plan_fragments`, `rules.dir`, `knowledge_base.dir`, `run_dir`, `capability_provider`, `project.name`. Compute the run id + run root `<run_dir>/<run-id>/plan/`; create `<run-root>/{research,scouts,candidates,reports,logs}`.

### 0.2 Required input — the spec
Locate the committed spec at `<run_dir>/<spec-run-id>/spec/reports/feature-spec.md` (most recent spec run) — resolve it from the run dir; if none exists, hard-fail: "no feature-spec.md — run `/greenoke:spec` first." Optional: the spec-phase `qa.md` if present.

### 0.3 Capture user notes
Save verbatim to `<run-root>/logs/user-notes.md` (`(no user notes)` if empty). Passed to every subagent.

### 0.4 Assemble the plan template
Read `${CLAUDE_PLUGIN_ROOT}/templates/feature-plan.core.md`. Splice `templates.plan_fragments` (if present) at the `<!-- INJECT: adapter plan_fragments here -->` marker. Write `<run-root>/logs/feature-plan.assembled.md`. Hold for writers/judge/verifier.

### 0.5 Feature name
From the spec title (`# Feature Spec: <name>`), lowercase kebab-case.

## Phase 1 — Codebase Research (parallel ×scout_count)

Spawn **`scout_count` `greenoke:codebase-researcher` agents in parallel** (`subagent_type: greenoke:codebase-researcher`). The two canonical scopes (extra researchers go to the more ambiguous one):

- **Researcher A — Reuse Candidates.** Prompt: read user-notes first; read the spec (§5/§6); enumerate existing units that could serve each surface; classify Reuse / Modify / New with confidence; **read `<knowledge_base.dir>/docs/decisions/` and `/capabilities/`** for prior decisions that bear on reuse. Save to `<run-root>/research/reuse.md`. Reuse is for code/units, never for cloning another feature's authored deliverable.
- **Researcher B — Similar Feature Patterns.** Prompt: read user-notes first; read the spec (§8/§9); enumerate existing features/patterns (persistence, timing, gating, progression, integration); **read `<knowledge_base.dir>/runbooks/` and `/triage/`** so the plan honors prior incidents; for each behavioral pattern the spec implies, name the best existing reference + adaptations. Save to `<run-root>/research/patterns.md`.

**This is where institutional memory enters the loop.** Wait for both before proceeding.

## Phase 2 — Plan-Question Scouting (parallel ×scout_count)

Spawn **`scout_count` `greenoke:plan-question-scout` agents in parallel** (`subagent_type: greenoke:plan-question-scout`). Each gets: spec path, user-notes path (authoritative — don't re-ask settled notes), both research paths, the capability-provider info, and the `rules.dir`. Save each reply to `<run-root>/scouts/scout-<n>.md`.

## Phase 3 — Drain Questions with User

**NON-SKIPPABLE.** Global "no-prompt"/"auto-mode" directives do NOT suppress this — the `AskUserQuestion` calls ARE the gate.

1. **Merge** scout outputs; dedupe.
2. **Triage — narrow only.** Drop a question ONLY if explicitly settled in the spec or spec-qa. "I have a strong recommendation" is NOT triage — surface it as `(Recommended)`.
3. **Ask** via `AskUserQuestion`, batches ≤4 (question + why, `header` ≤12 chars, 2–4 options, best guess `(Recommended)`).
4. **Re-evaluate** after each round; loop until none remain.
5. **Write Q&A** to `<run-root>/logs/qa.md` under `## Resolved Decisions`.

## Phase 4 — Plan Writing (parallel ×writer_count)

Spawn **`writer_count` `greenoke:plan-writer` agents in parallel** (`subagent_type: greenoke:plan-writer`). Each gets: the assembled template path, output `<run-root>/candidates/candidate-<n>.md`, spec path, user-notes path, both research paths, the `rules.dir`, and the full resolved Q&A.

## Phase 5 — Judge

Spawn **1 `greenoke:plan-judge`** (`subagent_type: greenoke:plan-judge`) with: all candidate paths, Q&A path, assembled template path, spec path, user-notes path, research paths, final output `<run-root>/reports/feature-plan.md`. Save report to `<run-root>/logs/judge-report.md`.

## Phase 6 — Escape Hatch (residue questions)

Same non-skippable contract as Phase 3. If the judge lists residue (final §11 non-empty): `AskUserQuestion` round, apply each answer via `Edit` (remove from §11, append under §11 "Resolved Decisions", edit affected sections), append pairs to qa.md. If §11 empty, skip.

## Phase 7 — Verify (run the real verifier — DO NOT eyeball it)

Run the **runnable** plan verifier — the executable form of this contract, and pass the committed spec so it cross-checks coverage. Do NOT hand-grade; gate on its JSON verdict:

```
"${CLAUDE_PLUGIN_ROOT}/tooling/verify_plan.py" \
    "<run-root>/reports/feature-plan.md" \
    --spec "<the committed feature-spec.md from 0.2>"
```

It emits `{"verdict": "PASS"|"NEEDS_REVIEW", "checks": [...], "plan": "<path>", "spec": "<path>"}` and **exits 0 on PASS, 1 on NEEDS_REVIEW**. Its checks: core sections 1–11 present + ordered; §7 has the two core integration sub-headings; §9 all 4 sub-headings (no TODO/TBD/?); **§10 ≥1 slice, each with all required fields (deliverable, files, dependencies, definition-of-done/acceptance)**; §11 Open Questions empty-or-escalated; and the **spec-coverage cross-checks** — every spec §5 surface has a plan §3 decision, every spec §6 state appears in plan §6, every spec §7 transition maps to a plan §4 trigger.

For each FAILED check in the JSON, fix the plan via `Edit`, then **re-run the verifier**. After 3 attempts on one still-failing check, log it `UNRESOLVED`, proceed. Save the final verifier JSON + fix notes to `<run-root>/logs/verification-log.md`.

Source: `${CLAUDE_PLUGIN_ROOT}/tooling/verify_plan.py` (stdlib-only, importable, unit-tested under `tooling/tests/test_verify_plan.py`).

## Phase 8 — Gate + Commit

### 8.0 Fail-closed gate
The gate consumes the **verifier's verdict** from Phase 7. If `verify_plan.py` returned `verdict: NEEDS_REVIEW` (or any check is still `UNRESOLVED` after 3 fix attempts):
- **autonomy=full** → **NEEDS_REVIEW** + **HALT**, no commit, output every failing/unresolved check + its `detail` from the verifier JSON.
- **autonomy=interactive** → `AskUserQuestion` (accept-as-deferral / fix-directive / halt); apply.

Only when the verifier returns `verdict: PASS` (exit 0) and zero `UNRESOLVED` remain → commit. **Fail-closed: a NEEDS_REVIEW plan is never committed in `full`.**

### 8.1 Commit
Stage `<run-root>/reports/feature-plan.md` and `<run-root>/`. Commit:
```
plan: <feature-name>
```

## Phase 9 — Export Session Log

Export to `<run-root>/logs/master.md` via `${CLAUDE_PLUGIN_ROOT}/tooling/export-agent-logs.py` where supported; else note the transcript path.

## Rules

- **Manifest-driven, no hardcoded paths.**
- **Spec is immutable** — never edit `feature-spec.md` from this skill.
- **Institutional memory in the loop** — researchers read `<knowledge_base.dir>` so the plan honors prior decisions and incidents.
- **Every ambiguity drained** — Phase 3/6 surface every scout question; self-writing qa.md from best-guess is forbidden.
- **Spawn agents by FQ id** — `subagent_type: greenoke:<agent>`.
- **Workers are one-shot.**
