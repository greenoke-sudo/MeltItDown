# Roadmap — MeltItDown

Initiatives and their status. Status flow: `research → spec → plan → implementing → shipped → closed`.

This is the durable, committed institutional memory for MeltItDown. The codebase-researcher
reads this (and `docs/decisions/`) during `/greenoke:plan` so plans honor prior decisions.

| Initiative | Status | Notes |
|---|---|---|
| greenoke onboarding | shipped | Vendored core at `greenoke/core/`, scaffolded the adapter (`greenoke/adapter/`) with manifest + minimal Unity-6-mobile rules + templates + build-smoke, wired the Unity MCP capability provider (reused from WordGame, same Unity 6 + MCP-for-Unity bridge), and seeded this `.greenoke/` KB. Ported from the WordGame project. |
| MELTFALL core gameplay loop | shipped | Whole vertical slice (Proto 1–3). spec ✅ → plan ✅ → build ✅ → QA gate **PASS_WITH_DEFERRALS** (0 BLOCKING, 5 accepted deferrals). 3 scenes, full liquid/material set, HUD, play-state machine, slow-mo beat, pause. [Decision](docs/decisions/meltfall-core-loop-build.md). Next: juice/audio, tests, level-flow, monetization. |
| _(add initiatives here)_ | | |
