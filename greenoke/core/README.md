# greenoke-core

**A portable agentic engineering core: a project-agnostic spec → plan → build pipeline + institutional-memory schema + capability-bridge contract, driven entirely by a per-project adapter.**

The core is versioned once and reused everywhere. A new project becomes "greenoke-enabled" by adding one thin adapter (`greenoke/adapter/`) and answering an init questionnaire — no core edits. The litmus test for every file here: pointed at one project's adapter or a completely different project's adapter, it must mean the same thing. If a core file names a specific project, language, or tool, that is a bug — route it through the manifest.

See `greenoke-framework-plan.md` (one level up, in the project that vendors this core) for the full blueprint, milestones, and decision records.

## The core / adapter contract

```
┌─────────────────────────────────────────────────────────────┐
│ GREENOKE CORE  (this repo — project-agnostic, versioned once)│
│  • spec → plan → build orchestration skills                 │
│  • generic agent definitions (scouts, writers, judges, QA…) │
│  • domain-neutral spec / plan TEMPLATES + injection points  │
│  • the .greenoke/ knowledge-base SCHEMA + init/install       │
│  • the capability-provider CONTRACT (the portable bridge)   │
│  • config schema, verdict semantics, deferral provenance    │
│  • tooling: launcher, log export, diff-dump (parameterized) │
└─────────────────────────────────────────────────────────────┘
                              ▲  references by stable contract only
┌─────────────────────────────────────────────────────────────┐
│ PROJECT ADAPTER  (greenoke/adapter/, in each project repo)  │
│  • greenoke.adapter.json — the manifest (names, paths,      │
│    language, capability provider, template fragments)       │
│  • rules/*.md — project conventions, auto-loaded by glob    │
│  • provider/ — the capability-provider IMPLEMENTATION        │
│  • templates/{spec,plan}.fragments.md — domain sections     │
│  • input/spec/ — where feature briefs are dropped           │
└─────────────────────────────────────────────────────────────┘
```

**How a core skill stays project-agnostic:** it never hardcodes a path or a noun. Each skill's **Phase 0** reads `config.default.json` (with the adapter override) and resolves `greenoke.adapter.json` for every path and behavior — input location, capability provider, knowledge-base root, run directory, verification commands. Where a project-specific pipeline would hardcode its own input directory, greenoke's spec skill reads `inputs.spec.dir` from the manifest. The same core, pointed at any manifest, resolves to that project's world.

### Where the adapter lives (distribution)

The core is pulled into each project as a git submodule at `greenoke/core/`. The adapter lives at `greenoke/adapter/` (committed in the project repo). Onboarding:

```
git submodule add <greenoke-core-url> greenoke/core
./greenoke/core/tooling/launch.sh        # then inside the session:
/greenoke:init                            # interactive — scaffolds greenoke/adapter/
/greenoke:install                         # validates the wiring, green/red checklist
```

Core upgrades are `git submodule update --remote`. Adapters are insulated because they only touch the documented contract.

### Durable vs. ephemeral

- **`.greenoke/`** — durable, **committed** institutional memory (the knowledge base, §8 below). `knowledge_base.dir` in the manifest.
- **`.greenoke-run/`** — ephemeral, **gitignored** per-run scaffolding (logs, candidates, qa.md, reports, verdicts). `run_dir` in the manifest. Runs write under `<run_dir>/<run-id>/{spec,plan,build}/…`.

## Plugin conventions (load-bearing — carried from the proven source plugin this core generalizes)

These are real Claude Code plugin mechanics, confirmed by smoke test in the source project. Violating them silently breaks loading.

- **Agents spawn by fully-qualified id.** Bare agent names do NOT resolve inside a plugin. Skills must spawn `greenoke:<agent>`, and `subagent_type` values use the same form (e.g. `subagent_type: greenoke:spec-question-scout`).
- **Flat agent layout.** `agents/<stem>.md` → id `greenoke:<stem>`. A doubled `agents/<x>/<x>.md` yields the ugly id `greenoke:<x>:<x>`, so agents are flat files.
- **Skill name is de-prefixed.** Frontmatter `name: spec` (NOT `name: greenoke:spec`) — the plugin name supplies the prefix. Invocation is `/greenoke:spec`. Skills are `user-invocable: true`.
- **Plugin-relative reads.** `${CLAUDE_PLUGIN_ROOT}/templates/...`, `${CLAUDE_PLUGIN_ROOT}/config.default.json`, `${CLAUDE_PLUGIN_ROOT}/contracts/...` resolve from inside skills and agents. The adapter manifest and all project paths come from the working tree, resolved through `greenoke.adapter.json`.

## Flag-only loading

The plugin loads per-session via `--plugin-dir greenoke/core` — no persistent `enabledPlugins` registration, so non-pipeline sessions stay lean (Decision D2). Use the launcher:

```
./greenoke/core/tooling/launch.sh        # = claude --plugin-dir <core> "$@", errors if plugin/CLI missing
/reload-plugins                           # inside the session, after editing core files
/greenoke:spec                            # then drive it (or :plan / :build / :init / :install)
```

**Loading gotcha.** A session NOT started with `--plugin-dir` cannot load this plugin — `/reload-plugins` only refreshes plugins registered at launch; it does not discover an unregistered local dir. Relaunch via the launcher to test.

## Configuration

Knobs live in `${CLAUDE_PLUGIN_ROOT}/config.default.json`, overridable per project by `greenoke/adapter/greenoke.config.json` (the adapter file shadows individual keys; missing file or key → the documented default). JSON has no comments, so this table is the schema doc. All five skills read it in **Phase 0**.

| Knob | Default | Meaning |
|------|---------|---------|
| `qa_iteration_cap` | `3` | Max build → QA → fix iterations before the terminal gate. |
| `autonomy` | `"full"` | Run mode. Extensible string — more modes may be added. |
| `writer_count` | `2` | Number of parallel spec/plan writer agents. |
| `scout_count` | `2` | Number of parallel question-scout (and codebase-researcher) agents. |
| `require_clean_git_before_build` | `true` | Build refuses to start on a dirty working tree (clean baseline for an honest diff). |
| `stale_system_guard` | `true` | Before any `verify()` that proves a fix, the provider's `health()` code-version stamp must match the working tree, or QA refuses to call the run green. |

**`autonomy` values:**

- `full` — hands-off; the pipeline runs end-to-end without prompting. Fail-closed verdicts replace prompts.
- `interactive` — prompts the user at defined ask-points via `AskUserQuestion`.

Treat `autonomy` as an extensible mode string: only `"interactive"` enables prompts; any other value (including `"full"`) is hands-off.

## Verdict semantics (core, identical across projects)

Every phase ends in exactly one verdict. This is the mechanism that makes "it works" impossible to fake — a run is green only when the gate says so, and the commit message carries the truth.

| Verdict | Meaning | Ships? |
|---|---|---|
| `PASS` | All checks passed, zero deferrals (MINOR findings don't affect the verdict). | Yes |
| `PASS_WITH_DEFERRALS` | Passed; only **user-approved** deferrals remain, each named. | Yes, with an honest commit subject |
| `NEEDS_REVIEW` | Unresolved BLOCKING issue, or cap hit with builder-emitted gaps. | **No — human required** |

**Deferral provenance is split** and the two buckets are never conflated:

- **User-approved** (from plan open-questions / Q&A, or an interactive ask-point) → honored as `DEFERRED`. Recorded under `## Accepted Deferrals`.
- **Builder-emitted** ("couldn't finish") → recorded under `## Builder-Emitted Deferrals (unreviewed)`, **NOT auto-honored**. QA re-evaluates the underlying requirement; if still unmet it is BLOCKING → `NEEDS_REVIEW`.

The build commit subject carries the provenance-split counts: `[<VERDICT>: <N> accepted / <M> builder-emitted]`.

## The capability provider (the portable bridge)

`contracts/capability-provider.md` defines the small, stable verb set agents call to drive a project's live system: `inspect / build / verify / screenshot / health`. The adapter implements the verbs however it likes (MCP, CLI, library); the core only knows the verbs. This is what lets two completely different kinds of project share one core. Three carry-over guarantees (late-start self-healing discovery; high-level verbs not raw HTTP; provider-owned artifact safety = build-to-temp → verify → atomic swap) and the `health()` code-version stamp are documented in that contract. `builder`, `probe`, and `qa` drive the live system through these verbs — never raw HTTP.

## Institutional memory — `.greenoke/` schema

A fixed, domain-neutral knowledge-base schema, so memory looks the same in every project and agents always know where to read/write. Resolved via `knowledge_base.dir`.

| Path | Purpose | Template |
|---|---|---|
| `.greenoke/ROADMAP.md` | initiatives + status (`research→spec→plan→implementing→shipped→closed`) | core |
| `.greenoke/work/INDEX.md` | live work tracker (Type, Slug, Status, Active Task) | core |
| `.greenoke/docs/decisions/<slug>.md` | decision records | `decision.md` |
| `.greenoke/docs/capabilities/<slug>.md` | what a subsystem does + gotchas + integration points | `capability.md` |
| `.greenoke/docs/learnings/<slug>.md` | post-mortems, hard-won insights | core |
| `.greenoke/docs/measurements/<slug>.md` | perf / telemetry / A-B results | core |
| `.greenoke/research/<date>-<slug>.md` | long-form investigations | core |
| `.greenoke/runbooks/<slug>.md` | operational guides for recurring tasks | `runbook.md` |
| `.greenoke/triage/<date>--<slug>.md` | incident records | `triage.md` |

The `codebase-researcher` reads this base during PHASE B so the plan honors prior decisions; `qa` reads `runbooks/` for domain test cases.

## How an adapter plugs in

1. `git submodule add <greenoke-core-url> greenoke/core`.
2. `/greenoke:init` — interactive questionnaire (language? where do briefs live? build-smoke command? what live system do agents drive — existing MCP/CLI or scaffold a stub provider? top project rules?), then scaffolds `greenoke/adapter/` from `templates/adapter-skeleton/`, writes `greenoke.adapter.json`, seeds `.greenoke/` KB, patches `CLAUDE.md`.
3. `/greenoke:install` — validates the manifest against `templates/adapter-manifest.schema.json`, checks the provider `health()` is reachable, confirms the KB is present; reports a green/red readiness checklist.
4. `/greenoke:spec | plan | build` then work in that project with zero core edits.

## Layout

```
greenoke-core/
├── .claude-plugin/plugin.json
├── README.md  CHANGELOG.md  config.default.json
├── skills/{init,install,spec,plan,build}/SKILL.md   # /greenoke:<name>
├── agents/<stem>.md                                 # 11 agents, flat → ids greenoke:<stem>
├── templates/
│   ├── feature-spec.core.md  feature-plan.core.md   # domain-neutral + injection points
│   ├── adapter-manifest.schema.json
│   ├── runbook.md  triage.md  decision.md  capability.md
│   └── adapter-skeleton/                            # what /greenoke:init copies
├── contracts/capability-provider.md
└── tooling/{launch.sh,export-agent-logs.py,diff-dump.sh}
```

The 11 agents: `spec-question-scout`, `spec-writer`, `spec-judge`, `plan-question-scout`, `plan-writer`, `plan-judge`, `codebase-researcher`, `builder`, `probe`, `debugger`, `qa`.
