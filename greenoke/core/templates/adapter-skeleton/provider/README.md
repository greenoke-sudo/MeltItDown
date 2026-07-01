# Capability provider — implementation stub

This directory holds **your project's** capability-provider implementation: the thing greenoke
agents drive to interact with your live system. The core defines only the verb contract; you
implement the verbs however suits your project (an MCP server, a CLI, a library entry point).

Read the contract first: `greenoke/core/contracts/capability-provider.md`.

## What to implement

Implement the verbs you declared in `greenoke.adapter.json → capability_provider.capabilities`:

| Verb | You must provide |
|---|---|
| `inspect(query)` | read live system state; side-effect-free; return `{ ok, state, error? }` |
| `build(spec)` | produce/refresh an artifact **to temp → verify → atomic swap**; return `{ ok, artifact_ref, log, error? }` |
| `verify(artifact)` | validate an output is well-formed and works; return `{ ok, checks[], code_version, error? }` |
| `screenshot()` | capture visual proof-of-life; return `{ ok, image_ref, error? }` |
| `health()` | return `{ reachable, code_version }` — `code_version` MUST change when the running code changes |

## The three guarantees you must honor

1. **Late-start, self-healing discovery** — attach lazily, re-attach if the backend restarts; never wedge.
2. **High-level verbs, not raw transport** — keep all HTTP/IPC/subprocess plumbing inside this provider.
3. **Artifact safety inside the provider** — `build()` does temp → `verify()` → atomic swap so a broken build never overwrites a good artifact. This must NOT be left to the calling agent.

## `health()` + stale_system_guard

`health().code_version` is what lets agents detect a stale live system (one still running old
code) instead of falsely reporting success. Make it a real stamp of the running code — a git
SHA + dirty flag, a content hash of loaded modules, a build id. The build/QA phases compare it
to the working tree before trusting any `verify()` as proof of a fix.

## Wiring

The launch command in `greenoke.adapter.json → capability_provider.launch` is how greenoke
starts/invokes this provider. `/greenoke:install` checks that `health()` is reachable through it.

Also provide `validate_artifact.<ext>` (referenced by `verification.artifact_validator`) — the
script your `verify()` drives to prove an artifact is well-formed.
