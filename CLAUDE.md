# MeltItDown

Unity 6 (6000.0.62f1) mobile game project.

## Greenoke

This Unity repo is **greenoke-enabled** — onboarded to the greenoke agentic-engineering framework (spec → plan → build pipeline + institutional-memory knowledge base). This section is a thin index; follow the links rather than duplicating them.

- **Core** (vendored, project-agnostic): `greenoke/core/` — launch a pipeline session with `greenoke/core/tooling/launch.sh`.
- **Adapter** (this project's wiring): manifest `greenoke/adapter/greenoke.adapter.json`; auto-loaded conventions in `greenoke/adapter/rules/`; domain template fragments in `greenoke/adapter/templates/`; the capability provider in `greenoke/adapter/provider/` (wired to the Unity MCP-for-Unity bridge).
- **Knowledge base** (durable, committed): `.greenoke/` — `ROADMAP.md`, `work/INDEX.md`, and `docs/` (decisions, capabilities, learnings, measurements), plus `research/`, `runbooks/`, `triage/`. The ephemeral per-run dir `.greenoke-run/` is gitignored.
- **Workflow:** `/greenoke:init` (onboard) · `/greenoke:install` (validate the wiring) · `/greenoke:spec` → `/greenoke:plan` → `/greenoke:build` (the feature pipeline).

To drive the pipeline, from the repo root run `greenoke/core/tooling/launch.sh`, then inside that session `/greenoke:install` to confirm readiness, then `/greenoke:spec`.
