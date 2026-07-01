# Roadmap — MeltItDown

Initiatives and their status. Status flow: `research → spec → plan → implementing → shipped → closed`.

This is the durable, committed institutional memory for MeltItDown. The codebase-researcher
reads this (and `docs/decisions/`) during `/greenoke:plan` so plans honor prior decisions.

| Initiative | Status | Notes |
|---|---|---|
| greenoke onboarding | shipped | Vendored core at `greenoke/core/`, scaffolded the adapter (`greenoke/adapter/`) with manifest + minimal Unity-6-mobile rules + templates + build-smoke, wired the Unity MCP capability provider (reused from WordGame, same Unity 6 + MCP-for-Unity bridge), and seeded this `.greenoke/` KB. Ported from the WordGame project. |
| MELTFALL core gameplay loop | spec | Whole vertical slice (Proto 1–3 as one feature). Spec PASSED `verify_spec.py` (8/8). Durable spec at `.greenoke/work/meltfall-core-loop/feature-spec.md`; full design at `.greenoke/research/2026-07-01-meltfall-design.md`. Next: `/greenoke:plan`. |
| _(add initiatives here)_ | | |
