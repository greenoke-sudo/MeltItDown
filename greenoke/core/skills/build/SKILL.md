---
name: build
description: Implement the feature from feature-plan.md slice-by-slice with a fail-closed QA loop. Builder drives the live system via the capability-provider verbs; probe proves life; debugger investigates; qa gates. Honors stale_system_guard + deferral provenance. Manifest-driven, project-agnostic.
user-invocable: true
effort: max
---

# Build (Plan-Driven, Fail-Closed)

You implement the feature using the committed `feature-plan.md` (the "how") and `feature-spec.md` (the "what"). The plan ships its own slice breakdown (§10) and decisions — you do NOT re-derive them. Each slice: implement → build/refresh the live system → probe (smoke + proof-of-life) → debugger (conditional) → decide → record → commit. A terminal QA gate fails closed. **Everything project-specific is resolved through the adapter manifest** — you drive the live system only through the capability-provider verbs, never raw HTTP.

## Verdict states (core, identical across projects)

- **PASS** — clean; zero deferrals; zero BLOCKING (MINOR don't affect it).
- **PASS_WITH_DEFERRALS** — zero BLOCKING; ≥1 **user-approved (Accepted)** deferral remains, each named.
- **NEEDS_REVIEW** — unresolved BLOCKING remains, OR a builder-emitted (unreviewed) deferral maps to an unmet requirement, OR a stale-system finding stands. Not shippable; a human must look.

**Deferral provenance is split and never conflated:**
- `## Accepted Deferrals` — USER-approved (plan §11 / Q&A skips, or items accepted at an interactive ask-point). Honored as DEFERRED.
- `## Builder-Emitted Deferrals (unreviewed)` — builder "couldn't-complete" items. NOT auto-honored; QA re-evaluates the underlying requirement.

## Autonomy

Driven by `autonomy` (Phase 0). `full` (default) = hands-off; no prompts; auto-records builder-emitted deferrals and proceeds; **fails closed** at the terminal gate (NEEDS_REVIEW halt, never a green PASS). `interactive` = `AskUserQuestion` ONLY at the three ask-points: (1) QA iteration cap reached, (2) stuck slice, (3) external-input findings. `AskUserQuestion` is banned everywhere else.

## Phase 0 — Config + Read Plan + Manifest

### 0.0 Read config
`cat "${CLAUDE_PLUGIN_ROOT}/config.default.json"`, overlay `greenoke/adapter/greenoke.config.json` if present. Pull `qa_iteration_cap` (default 3), `autonomy` (default `"full"`), `require_clean_git_before_build` (default true), `stale_system_guard` (default true). Hold all for the build.

### 0.1 Resolve the manifest
Read `greenoke/adapter/greenoke.adapter.json`. Resolve `capability_provider` (kind/launch/capabilities), `verification.build_smoke`, `verification.artifact_validator`, `rules.dir`, `knowledge_base.dir`, `run_dir`, `project.name`. Compute the run id + run root `<run_dir>/<run-id>/build/`; create `<run-root>/{slices,reports,qa,logs}`.

### 0.2 Required inputs
- `feature-plan.md` (most recent plan run) — implementation source of truth. Missing → stop, report path.
- `feature-spec.md` (its spec run) — design source of truth. Missing → stop.
- Plan research `reuse.md` / `patterns.md` — reuse these, do not re-research patterns.
- Plan Q&A / spec Q&A — read if present (carries the deferral buckets).

### 0.3 Clean-git guard
If `require_clean_git_before_build` is true, check `git status --porcelain`. If the tree is dirty, **stop** with an unblocking message ("commit or stash before building — the build needs a clean baseline for an honest diff"). This is the clean baseline a per-slice diff and the final audit depend on.

### 0.4 Provider health + version baseline
Start/attach the capability provider per `capability_provider.launch`. Call `health()`. If `reachable: false`, retry (self-healing discovery); if still down and the provider is required for this build, stop with the provider's hint. **Record `health().code_version` and the working-tree version stamp** — `stale_system_guard` compares them throughout.

### 0.5 Build supplement
Read plan + spec end-to-end (internalize §10 slice list — do not re-derive it). Write `<run-root>/logs/log.md` with the slice list copied verbatim from plan §10 (each slice `status: pending`). Re-run the `greenoke:codebase-researcher` here only if the build needs fresh context the plan research doesn't cover.

## Phase 1..N — Slice Execution

**HARD RULE: do not start the next slice until the current one is verified by your judgment and committed.** Each slice's cycle:

### 1. Implement (via the builder agent)
Spawn `greenoke:builder` (`subagent_type: greenoke:builder`) for the slice, passing: the plan (§2 layout, §3–§9, the specific §10 slice + its definition of done), the spec, the research, the `rules.dir`, and the capability-provider info. The builder writes code with file tools and drives the live system through the verbs. It logs generously.

### 2. Build / refresh the live system
When the slice produces an artifact or needs a reload, the builder calls the provider's `build()` verb — temp → verify → atomic swap is the **provider's** guarantee (a broken build never replaces a good artifact). The builder never hardcodes transport.

### 3. Probe (smoke + proof-of-life)
Spawn `greenoke:probe` (`subagent_type: greenoke:probe`) to: run `verification.build_smoke`, then exercise each acceptance criterion in the slice's definition of done through the verbs (`verify()` checks, `screenshot()`, `inspect()`, log lines). The probe **records `health().code_version`** and reports observations only — never PASS/FAIL. Save its report under `<run-root>/reports/slice-<NN>.md`.

### 4. Decide + stale guard
The builder reads the probe evidence and decides PASS/FAIL per criterion. **`stale_system_guard`:** before trusting any `verify()` as proof a fix landed, confirm the probe's `code_version` matches the working tree. If they disagree, the live system is stale — re-trigger a reload through the provider and re-probe; do NOT mark PASS against stale code.

### 5. Debugger (conditional)
On any FAIL, spawn `greenoke:debugger` (`subagent_type: greenoke:debugger`) — first-line, not last resort. Read its root-cause hypothesis + recommended fix; the builder applies it; re-probe. The debugger never applies fixes. The only surprise the builder fixes directly is a build/compile error with a clear message.

### 6. Stuck-slice handling (ask-point 2)
If a criterion can't close in-pipeline (cause out of reach — missing external input, ambiguous spec, upstream gap):
- **interactive** → `AskUserQuestion` with the unresolved item + evidence; accept-as-deferral / fix-directive / halt.
- **full** → record a **builder-emitted deferral** under `<run-root>/qa/qa.md` → `## Builder-Emitted Deferrals (unreviewed)` (with the evidence line), mark the criterion DEFERRED, proceed. NOT auto-honored later.

### 7. Record + commit
Write `<run-root>/slices/<NN>-<slug>.md` (plan ref, files, per-criterion PASS/DEFERRED + evidence, open items). Mark the slice `done` in log.md. Commit:
```
build: <feature-name> <slice-description>
```

## Phase Final — Audit → QA Gate → Session Log

Terminates with **exactly one verdict**. Cannot exit until the QA loop resolves or the cap is reached.

### Final.1 — Audit
Full diff audit across all slices (apply the auto-loaded project rules). Apply safe findings. Commit `chore: <feature-name> audit fixes`.

### Final.2 — QA Verification Loop (the gate)
`mkdir -p <run-root>/qa/reports`. Set `iteration = 1`, max = `qa_iteration_cap`.

**Final.2.a — Spawn QA.** One `greenoke:qa` agent (`subagent_type: greenoke:qa`). Pass ONLY its cold sources: the spec, spec Q&A, plan Q&A (honor `## Accepted Deferrals` as DEFERRED; do NOT auto-honor `## Builder-Emitted Deferrals (unreviewed)` — re-evaluate each underlying requirement), plan §9–§10, the spec references, the capability-provider info, and the report output path `<run-root>/qa/reports/iteration-<iteration>.md`. **Do NOT pass the build log, slice files, or any "I think it works" claim** — QA reads cold. (On iteration > 1, also pass the prior BLOCKING list to re-verify adversarially.)

**Final.2.b — Consume the verdict.** Read the report.
- **PASS** → exit loop. Terminal verdict `PASS` (zero deferrals) or `PASS_WITH_DEFERRALS` (≥1 Accepted deferral). Proceed to Final.3.
- **NEEDS_REVIEW** → triage each BLOCKING finding:
  - *In-pipeline fixable* → apply (builder edits + provider verbs). Commit `fix: <feature-name> qa iteration <iteration>`.
  - *External-input required* (ask-point 3) → **interactive:** `AskUserQuestion` (accept-as-deferral → `## Accepted Deferrals` / fix-directive / halt). **full:** record a builder-emitted deferral with the concrete `<what's missing>` named.
  - Increment `iteration`. If `> qa_iteration_cap` → Final.2.c. Else loop to Final.2.a.

**stale_system_guard at the gate (concrete):** when `stale_system_guard` is true (default), before trusting ANY `verify()` as proof, QA calls the provider's `health()` verb and compares the returned `code_version` to the working tree's stamp (the same stamp kind the adapter's provider emits — e.g. `git rev-parse --short HEAD` + a dirty flag, or a content hash). The comparison is computed as:

```
WORKTREE_STAMP="$(git rev-parse --short HEAD)$([ -n "$(git status --porcelain)" ] && echo -dirty)"
# HEALTH_VERSION = code_version field from the provider's health() result
```

**If `HEALTH_VERSION` != `WORKTREE_STAMP`, the live system is STALE** — it is executing old code. QA emits a clear `stale system — restart required` finding, sets `stale_guard.triggered = true` in `findings.json`, and **halts the gate**: `build_verdict.py` then returns `NEEDS_REVIEW` unconditionally (a stale system can never go green). QA does NOT call any result green until the stamps agree (re-trigger a reload/restart through the provider and re-`health()`). This is the same check `greenoke:qa` and `greenoke:probe` run; the skill records the comparison in the QA gate state.

**Final.2.c — Cap reached (ask-point 1).** Do NOT auto-accept BLOCKING as deferrals; do NOT treat as PASS (fail-closed). Compute the terminal verdict with the **single, testable helper** — never hand-roll the verdict logic:

```
"${CLAUDE_PLUGIN_ROOT}/tooling/build_verdict.py" --findings "<run-root>/qa/findings.json" --human
```

where `<run-root>/qa/findings.json` is assembled from the QA report + the deferral buckets:
```json
{
  "feature": "<feature-name>",
  "checks":    [ {"name": "...", "severity": "BLOCKING|MINOR", "status": "open|resolved", "detail": "..."} ],
  "deferrals": [ {"provenance": "accepted|builder_emitted", "requirement": "...", "detail": "..."} ],
  "cap_hit":   true,
  "stale_guard": { "triggered": <bool>, "detail": "<health code_version vs tree>" }
}
```

The helper encodes framework law in ONE place (it **exits 0 = shippable, 1 = NEEDS_REVIEW**):
- **`accepted`** deferrals (user-approved — from plan §11 / Q&A) are honored as DEFERRED.
- **`builder_emitted`** deferrals are NEVER auto-honored → they force `NEEDS_REVIEW`. Provenance is never conflated.
- **Any open BLOCKING** → `NEEDS_REVIEW`. **`stale_guard.triggered`** → `NEEDS_REVIEW`, unconditionally.
- Zero open BLOCKING + zero builder-emitted + not stale, with ≥1 accepted deferral → `PASS_WITH_DEFERRALS`; with zero deferrals → `PASS`.

Honor the verdict the helper returns:
- **`NEEDS_REVIEW`** → **full:** emit NEEDS_REVIEW + HALT; do NOT run Final.3; output the helper's `reasons` list (every unresolved item + evidence). **interactive:** `AskUserQuestion` (accept-as-deferral → move the item to `## Accepted Deferrals` and re-compute / fix-directive → re-run QA / halt).
- **`PASS` / `PASS_WITH_DEFERRALS`** → Final.3.

Source: `${CLAUDE_PLUGIN_ROOT}/tooling/build_verdict.py` (stdlib-only, importable, unit-tested under `tooling/tests/test_build_verdict.py` — clean PASS, provenance split, cap-hit, stale-guard halt all covered).

### Final.3 — Final artifacts (shippable verdict only)
Reached only on `PASS` / `PASS_WITH_DEFERRALS`. The deferral counts split by provenance and the verdict come from the **same `build_verdict.py` result** computed at Final.2.c — its `commit_subject` field IS the honest subject line (verdict + `<N> accepted / <M> builder-emitted` surface in the subject). Update log.md with the verdict line + the helper's `reasons`. Export the session to `<run-root>/logs/build.md` via the log exporter where supported. Commit remaining artifacts with that subject:
```
chore: <feature-name> session log + qa artifacts [<VERDICT>: <N> accepted / <M> builder-emitted]
```
(`<VERDICT>` and counts are NOT re-derived by hand — they are the helper's `verdict` + `deferral_summary`.)

## Rules

- **Manifest-driven, no hardcoded paths or tools.** Live-system interaction is ALWAYS through the capability-provider verbs — never raw HTTP/IPC. If you're constructing a request, a verb is missing.
- **Plan-driven** — architecture, slices, patterns, edge cases live in the plan; don't re-derive.
- **QA agent verdict is the gate** — Phase Final MUST spawn `greenoke:qa` cold; the build cannot self-grade. Fail-closed: at the cap, unresolved BLOCKING ⇒ NEEDS_REVIEW (never a green PASS). Only user-approved Accepted deferrals are honored.
- **Deferral provenance is split** — builder-emitted deferrals are NOT auto-honored; an unmet requirement stays BLOCKING.
- **stale_system_guard is honored** — no verify() is trusted as proof while the live system's `code_version` ≠ the tree.
- **Verdicts are the builder's job** during slices; probe observes, debugger hypothesizes, the QA agent gates.
- **Autonomy-gated prompting** — `full` is hands-off + fail-closed; `interactive` prompts only at the three ask-points.
- **Spawn agents by FQ id** — `subagent_type: greenoke:<agent>`. Workers are one-shot.
- **No signed commits** — no co-author / signoff lines.
