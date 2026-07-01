# Capability Provider — the portable bridge contract (v0.1)

The capability provider is how greenoke agents drive a project's **live system** without the core ever knowing what that system is. The core defines a small, stable **verb set**; each adapter implements the verbs however it likes (an MCP server, a CLI, a library). This is what lets two completely different kinds of project share one core.

**The core only knows the verbs.** Agents (`builder`, `probe`, `qa`, `debugger`) call `inspect / build / verify / screenshot / health` — never raw HTTP, never a project-specific endpoint. The transport and the project specifics live inside the provider, one place to audit.

The adapter declares the provider in `greenoke.adapter.json → capability_provider`:

```json
"capability_provider": {
  "kind": "mcp",
  "launch": ["<argv to start/serve the provider>"],
  "capabilities": ["inspect", "build", "verify", "screenshot", "health"]
}
```

`capabilities` lists which verbs this provider implements. `health` is **required** for `stale_system_guard` (config) to function. A provider may declare `kind: "none"` for a project with no live system to drive — in that case `build()` is pure file work and the pipeline runs without `verify()/screenshot()` proof-of-life (the spec/plan phases still work fully).

---

## The verbs

Each verb below specifies its **purpose**, its **inputs/outputs shape**, and any **guarantees**. The shapes are logical contracts (JSON-ish), not a wire format — an MCP provider returns them as tool results, a CLI as stdout JSON, a library as return values. What matters is that the agent receives the named fields.

### `inspect(query) → state`

- **Purpose:** read live system state without changing it. The read half of the loop.
- **Inputs:** `query` — an adapter-interpreted selector for what to read (e.g. a hierarchy path, a record id, a settings key, a queue name). Free-form string or object; the core passes it through.
- **Outputs:** `state` — the requested state as structured data, plus `ok: bool` and `error?: string`. Read-only; never mutates.
- **Guarantee:** idempotent and side-effect-free.

### `build(spec) → build_result`

- **Purpose:** produce or refresh an artifact (compile, render, generate, write-and-bake).
- **Inputs:** `spec` — an adapter-interpreted description of what to build (a target id, a config, a variant request). The agent writes source via normal file tools; `build()` drives the *running system* to materialize the artifact.
- **Outputs:** `build_result` — `{ ok: bool, artifact_ref?: string, log?: string, error?: string }`. `artifact_ref` identifies the produced artifact for a subsequent `verify()`.
- **Guarantee (artifact safety — see below):** `build()` renders to a **temp location**, runs `verify()` on it, and only **atomically swaps** the temp artifact into the live location on a clean verify. A broken build never replaces a good artifact. This guarantee lives **inside the provider**, not in the calling agent — the agent cannot forget it.

### `verify(artifact) → verification`

- **Purpose:** validate that an output is well-formed and meets the proof-of-life bar (the artifact actually works, not just that the build process exited 0).
- **Inputs:** `artifact` — an `artifact_ref` from `build()`, or a path to an existing artifact.
- **Outputs:** `verification` — `{ ok: bool, checks: [{ name, ok, detail }], code_version?: string, error?: string }`. Each check names a concrete property the artifact must satisfy.
- **Guarantee:** `verify()` is the gate `build()` calls before its atomic swap, and the gate `probe`/`qa` call for proof-of-life. It returns the provider's `code_version` stamp alongside the result so the caller can enforce `stale_system_guard` (see below).

### `screenshot() → image_ref`

- **Purpose:** capture visual proof-of-life of the live system or the produced artifact.
- **Inputs:** optional selector for what to capture (a view, a frame index, a region). Default: the most relevant current view.
- **Outputs:** `{ ok: bool, image_ref: string, error?: string }` — `image_ref` is a path the agent can read with its file tools.
- **Guarantee:** captures even when the live system is unfocused / headless where the platform allows.

### `health() → health_report`

- **Purpose:** report whether the live system is reachable AND what code version it is running. Powers both self-healing discovery and `stale_system_guard`.
- **Inputs:** none.
- **Outputs:** `health_report` — `{ reachable: bool, code_version: string, detail?: string }`.
  - `reachable` — is the backend attached and responsive.
  - **`code_version` — a stamp identifying the code the running system is executing** (a git SHA, a content hash of the loaded modules, a build id — adapter's choice). It SHOULD change whenever the working-tree code the system runs changes. The strength of that guarantee is the adapter's choice and the agent must know which it gets:
    - A **content hash** (of the loaded modules / tracked working tree) is the strong form — every distinct code state yields a distinct stamp, so any divergence is detectable.
    - A **commit id plus a coarse dirty flag** (e.g. `git rev-parse --short HEAD` + a boolean `-dirty`) is the common weaker form — it changes reliably on *commit*, but two **different** uncommitted edits at the **same HEAD** collide on the same stamp (`<sha>-dirty`). With this form, `stale_system_guard` reliably catches a server running an *old commit*, but **cannot** distinguish two distinct dirty trees at the same HEAD — commit before trusting a same-HEAD cross-tree comparison. Adapters wanting the strong guarantee should content-hash; the dirty flag is documented future-hardening, not a content hash.

    Either way this is the field that lets an agent *detect a stale server* instead of declaring "it works" against code that was never reloaded.
- **Guarantee:** cheap and fast; safe to poll.

---

## The three carry-over guarantees (kept because they prevent real pain)

These three properties are lifted from a proven project-specific bridge and made framework-level. A provider that omits them is non-conformant.

### 1. Late-start, self-healing discovery

The provider attaches to its backend **lazily** and **re-attaches on its own** if the backend restarts mid-run. An agent must never wedge because the live system was opened after the session started, or was restarted on a new port/PID. If the backend is down, `health()` returns `{ reachable: false }` with an unblocking hint — the agent retries rather than dying. The agent treats a transient `reachable: false` as "retry after the backend comes up," not as a failure.

### 2. High-level verbs, not raw HTTP

Agents call `verify(artifact)`, not `GET /api/...`. **All HTTP / IPC / subprocess plumbing lives inside the provider** — one place to audit, one place to change transports. The core has zero knowledge of the provider's wire protocol. If you find an agent constructing a URL or a request body, that is a contract violation: the verb is missing or the agent is bypassing it.

### 3. Artifact safety belongs to the provider, not the agent

`build()` → temp → `verify()` → **atomic swap**. The provider guarantees a broken build never overwrites a good artifact. This is a **framework-level guarantee**, not a thing an agent remembers to do — it cannot be skipped under time pressure, cannot regress in a refactor of the calling skill, and is identical across every project. The calling agent simply calls `build()` and trusts that a failed verify left the prior good artifact in place.

---

## `health()` + `stale_system_guard` (framework law)

`stale_system_guard` (config, default `true`) makes one rule enforceable: **before any `verify()` that "proves" a fix, the provider's `health().code_version` must match the working tree the agent is operating on, or QA refuses to call the run green.**

Concretely, in the build/QA phases:

1. The agent makes a code change, then asks the provider to `build()` / reload.
2. Before trusting a `verify()` as proof the fix landed, the agent reads `health().code_version`.
3. The agent compares it to the working tree's current version stamp (the same kind of stamp the adapter chose — e.g. `git rev-parse HEAD` plus a dirty flag, or a content hash of the changed modules).
4. **If they don't match, the running system is stale** — it is executing old code. The agent does NOT declare success. QA records this and the verdict cannot be green until the stamps agree (the agent re-triggers a reload/restart through the provider and re-checks).

This single guard turns "it works, just restart" (asserted against a server still running old code) into a detectable, fail-closed condition. Adapters cannot opt out of honesty: the guard is on by default and the verdict gate consults it.

---

## Conformance checklist (what `/greenoke:install` verifies)

- [ ] `greenoke.adapter.json → capability_provider` declares `kind` and a non-empty `capabilities` list.
- [ ] `health()` is implemented and returns `{ reachable, code_version }` — `code_version` changes when the running code changes (strongest with a content hash; a commit-id + coarse dirty flag changes on commit but collides between two distinct dirty trees at the same HEAD — see `health()` above).
- [ ] The provider is reachable (or cleanly reports `reachable: false` with a hint when the backend is down — self-healing, not a hard error).
- [ ] If `build` is a declared capability, the provider implements temp → verify → atomic swap internally.
- [ ] Agents in this adapter's runs never construct raw transport calls — all live-system interaction routes through the verbs.

> **Illustrative only (NOT part of the contract).** To make the abstraction concrete: a game-editor adapter might map `inspect`→scene/hierarchy read, `build`→asset refresh, `verify`→play-mode + screenshot, `health`→editor-bridge attach status. A media-pipeline adapter might map `verify`→an output well-formedness check (codec/stream/duration probe), `health`→server reachable + a code-version stamp. A CLI-tool adapter might map `inspect`→read config/state, `verify`→exit-code + golden-output check, `health`→"code is on disk" + a code-version stamp. These mappings live in each adapter's `provider/`, never in the core.
