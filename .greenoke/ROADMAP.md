# Roadmap — MeltItDown

Initiatives and their status. Status flow: `research → spec → plan → implementing → shipped → closed`.

This is the durable, committed institutional memory for MeltItDown. The codebase-researcher
reads this (and `docs/decisions/`) during `/greenoke:plan` so plans honor prior decisions.

| Initiative | Status | Notes |
|---|---|---|
| greenoke onboarding | shipped | Vendored core at `greenoke/core/`, scaffolded the adapter (`greenoke/adapter/`) with manifest + minimal Unity-6-mobile rules + templates + build-smoke, wired the Unity MCP capability provider (reused from WordGame, same Unity 6 + MCP-for-Unity bridge), and seeded this `.greenoke/` KB. Ported from the WordGame project. |
| MELTFALL core gameplay loop | plan | Whole vertical slice (Proto 1–3). Spec PASSED `verify_spec.py` (8/8); plan PASSED `verify_plan.py` (8/8, full spec coverage). Durable [spec](work/meltfall-core-loop/feature-spec.md) + [plan](work/meltfall-core-loop/feature-plan.md); design at `.greenoke/research/2026-07-01-meltfall-design.md`. Build order = 5 slices. Next: `/greenoke:build`. |
| _(add initiatives here)_ | | |
