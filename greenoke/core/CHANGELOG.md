# Changelog

All notable changes to greenoke-core. Bump `.claude-plugin/plugin.json` `version` on every iteration and add an entry here.

## 0.2.0 — QA absorbs portable code-review protocols

The `qa` agent now gates on code quality as well as spec conformance, folding in the portable engineering-review protocols (generalized, project-agnostic — no external tooling assumptions).

- `agents/qa.md` — added a **six-dimension code-quality audit** (correctness/SOLID, architecture & conventions, performance, configurability, observability, naming) on the changed source, alongside the existing spec-conformance verification.
- Added **judge-grade finding validation**: the full-context rule (never review from the diff alone), fix-validation (a real defect paired with a broken fix is still bad guidance), evidence tags (`verified at file:line` vs `speculative`), and a don't-invent-issues / drop-false-positives posture.
- Real code defects now map to **BLOCKING**; quality suggestions to **MINOR** — same fail-closed verdict trichotomy (PASS / PASS_WITH_DEFERRALS / NEEDS_REVIEW) and `stale_system_guard`.
- Renamed qa's `## Rules` → `## Hard rules`.

## 0.1.0 — M0 scaffold

Initial project-agnostic core skeleton. Loadable, coherent, manifest-driven.

- `plugin.json` (`name: greenoke`, `0.1.0`), flag-only loading via `tooling/launch.sh`.
- `config.default.json` — knobs: `qa_iteration_cap`, `autonomy`, `writer_count`, `scout_count`, `require_clean_git_before_build`, `stale_system_guard`. Adapter override at `greenoke/adapter/greenoke.config.json`.
- `templates/adapter-manifest.schema.json` — JSON Schema for `greenoke.adapter.json` (project / inputs / capability_provider / templates / rules / knowledge_base / run_dir / banned_tokens_source / verification).
- `contracts/capability-provider.md` — the portable bridge contract: verbs `inspect / build / verify / screenshot / health`, the three carry-over guarantees (late-start self-healing discovery, high-level verbs not raw HTTP, provider-owned artifact safety), and the `health()` code-version stamp that powers `stale_system_guard`.
- `templates/feature-spec.core.md`, `templates/feature-plan.core.md` — domain-neutral strict templates with adapter injection points and a core banned-token list.
- `templates/{runbook,triage,decision,capability}.md` — knowledge-base doc templates.
- `templates/adapter-skeleton/` — scaffold copied by `/greenoke:init`.
- `skills/{spec,plan,build,init,install}/SKILL.md` — manifest-driven orchestration. Every skill resolves all paths through `greenoke.adapter.json` at Phase 0; no hardcoded input paths.
- `agents/*.md` — 11 project-agnostic agents (flat layout, ids `greenoke:<stem>`).
- `tooling/launch.sh`, `tooling/export-agent-logs.py`, `tooling/diff-dump.sh` — parameterized, no project assumptions.

M1 (manifest + init/install hardening), M2 (real capability provider for the first adapter) tracked in `greenoke-framework-plan.md` §12.
