---
name: init
description: Onboard a new project to greenoke — interactive questionnaire → scaffold greenoke/adapter/ from the skeleton → write greenoke.adapter.json → seed the .greenoke/ knowledge base → patch CLAUDE.md. Idempotent (re-run updates, never clobbers). Project-agnostic; produces the adapter that makes /greenoke:spec|plan|build work.
user-invocable: true
effort: max
---

# Init — onboard a project to greenoke

You make a repo "greenoke-enabled" by scaffolding its **adapter** (`greenoke/adapter/`) and seeding its **knowledge base** (`.greenoke/`) from an interactive questionnaire. After init + `/greenoke:install`, the spec/plan/build skills work in this project with zero core edits. This skill names no specific project — it learns the project from the user's answers.

This is a **concrete, runnable flow**, not prose. Do every step. It is **idempotent**: a re-run detects an existing adapter and *updates* it (backing up before any overwrite) rather than destroying existing rules or KB content.

## Phase 0 — Preconditions

Run these and stop with the named unblocking action on any failure:

1. **Core present.** `test -f greenoke/core/.claude-plugin/plugin.json` → else: tell the user to `git submodule add <greenoke-core-url> greenoke/core` (or `git submodule update --init`).
2. **At repo root.** `git rev-parse --show-toplevel` and confirm it equals the cwd. Run init from the repo root.
3. **Re-run detection (idempotency gate).** `test -f greenoke/adapter/greenoke.adapter.json`:
   - **Absent** → fresh scaffold (Phase 2 copies the skeleton wholesale).
   - **Present** → this is an UPDATE. Load the existing manifest, ask via `AskUserQuestion` whether to **Update** (re-ask the questionnaire pre-filled with current values, rewrite only the manifest + re-seed any *missing* KB/skeleton pieces — never overwrite existing rules or KB docs) or **Abort**. Never clobber silently.

## Phase 1 — Questionnaire (interactive)

Ask via `AskUserQuestion`, in batches of ≤4, each with a recommended default. On an UPDATE, pre-fill defaults from the existing manifest. Cover (§10 of the blueprint):

1. **Project identity** — machine name (slug), display name, **primary language**, repo root (default `.`).
2. **Where feature briefs live** — `inputs.spec.dir` (default `greenoke/adapter/input/spec`) and `inputs.spec.kind` (default `markdown-brief`).
3. **Build-smoke command** — `verification.build_smoke` (compile/test/lint command the probe runs). Required.
4. **Live system the agents drive** — what is it, and does it already expose an MCP/CLI/library, or should init scaffold a **stub provider**? Capture `capability_provider.kind` (`mcp`/`cli`/`library`/`none`), its `launch` argv, and which `capabilities` it implements (`inspect/build/verify/screenshot/health` — recommend including `health` so `stale_system_guard` works).
5. **Top project rules** — the 3–5 conventions that should auto-load (seed `rules/project.md`).
6. **Artifact validator** (optional) — `verification.artifact_validator` path, if the project has a proof-of-life check.

Record every answer; they drive Phase 2–4.

## Phase 2 — Scaffold the adapter (idempotent copy)

Use a **non-clobbering copy**. The skeleton lives at `${CLAUDE_PLUGIN_ROOT}/templates/adapter-skeleton/`.

```bash
SKEL="${CLAUDE_PLUGIN_ROOT}/templates/adapter-skeleton"
DEST="greenoke/adapter"
mkdir -p "$DEST"
# copy ONLY files that don't already exist (-n = no-clobber); preserves any
# rules/ KB content a prior run or the user authored by hand.
cp -Rn "$SKEL"/. "$DEST"/
```

Then:

- **Write `greenoke/adapter/greenoke.adapter.json`** — this file *is* rewritten from the answers (it is the contract; on UPDATE, back it up first: `cp greenoke/adapter/greenoke.adapter.json greenoke/adapter/greenoke.adapter.json.bak`). Fill every placeholder from the questionnaire — `project.{name,display,language,repo_root}`, `inputs.spec.{dir,kind}`, `capability_provider.{kind,launch,capabilities}`, `verification.{build_smoke,artifact_validator}`. Leave `templates.*`, `rules.dir`, `knowledge_base.dir`, `run_dir`, `banned_tokens_source` at the skeleton defaults unless the user overrode them.
- **Validate the written manifest** before continuing — this catches a malformed answer immediately:
  ```bash
  python3 "${CLAUDE_PLUGIN_ROOT}/tooling/manifest_validate.py" \
      greenoke/adapter/greenoke.adapter.json --check-paths --repo-root .
  ```
  Schema errors → fix the manifest from the answers and re-run; do not proceed with an invalid manifest. (Missing optional paths like `artifact_validator` are warnings at this stage — they'll exist once the provider is built.)
- **Seed `greenoke/adapter/rules/project.md`** with the user's top rules (one bullet each). On UPDATE, if the file already has authored rules, **append** new ones under a dated heading rather than overwriting.
- **Provider:** if a provider already exists, set `launch` accordingly and leave `provider/README.md` as guidance. If the user asked for a **stub**, write a clearly-marked stub entry point (named per `kind`) implementing `health()` returning a real `code_version` (e.g. `git rev-parse HEAD` + dirty flag) and the other declared verbs as TODO stubs — so `install` can reach `health()`.
- **Template fragments:** leave `templates/{spec,plan}.fragments.md` as the commented skeletons unless the questionnaire surfaced obvious domain integration points (the live-system hooks) — then seed `plan.fragments.md` with a starter sub-heading per hook.

## Phase 3 — Seed the knowledge base (idempotent)

Create the `.greenoke/` schema at `knowledge_base.dir`. Use `mkdir -p` (idempotent) and **only write doc files that don't exist** (never overwrite committed KB content):

```bash
KB=".greenoke"
mkdir -p "$KB/work" \
         "$KB/docs/capabilities" "$KB/docs/decisions" "$KB/docs/learnings" "$KB/docs/measurements" \
         "$KB/research" "$KB/runbooks" "$KB/triage" "$KB/onboarding"
# seed index files only if absent
[ -f "$KB/ROADMAP.md" ]      || cp/seed an empty initiative table
[ -f "$KB/work/INDEX.md" ]   || cp/seed an empty work tracker (Type / Slug / Status / Active Task)
# drop the KB doc templates as authoring references (no-clobber)
cp -n "${CLAUDE_PLUGIN_ROOT}/templates/"{runbook,triage,decision,capability}.md "$KB/onboarding/"
```

Then ensure the **run dir is gitignored** (append, don't clobber an existing `.gitignore`):

```bash
grep -qxF '.greenoke-run/' .gitignore 2>/dev/null || echo '.greenoke-run/' >> .gitignore
```

The KB (`.greenoke/`) is committed; the run dir (`.greenoke-run/`) is ephemeral.

## Phase 4 — Patch CLAUDE.md (idempotent)

Add (or create) a `CLAUDE.md` greenoke section at the repo root pointing at both layers: the core (`greenoke/core/`) + launcher, the adapter manifest + rules dir, the `.greenoke/` KB, and the `/greenoke:spec|plan|build` workflow. **Guard against double-insertion**: if a `## Greenoke` (or equivalent) section already exists, update it in place rather than appending a duplicate. Keep it a thin index — link, don't duplicate.

## Phase 5 — Report

Summarize what was scaffolded (every path created vs. left untouched), the resolved manifest values, the validator output, and the next step: run `/greenoke:install` to validate the wiring. Do NOT commit automatically unless the user asked — list the new files and let them review.

## Rules

- **Interactive by nature** — `AskUserQuestion` is the mechanism here; it is not suppressed by any no-prompt directive.
- **Idempotent / never clobber** — re-running updates. The manifest is rewritten (backed up first); rules, KB docs, `.gitignore`, and `CLAUDE.md` are *merged/appended*, never overwritten.
- **No project assumptions** — every project-specific value comes from the answers, not from the core.
- **The manifest is the contract** — fill it completely and validate it with `tooling/manifest_validate.py` before finishing; `install` re-validates against the schema.
