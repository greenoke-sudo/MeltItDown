# Capability provider — MeltItDown (Unity)

This directory holds MeltItDown's greenoke **capability provider**: the portable
bridge greenoke agents drive to interact with the **live Unity Editor** without
ever touching the wire protocol. The core defines only the verb contract
(`greenoke/core/contracts/capability-provider.md`); this provider maps the five
verbs onto the running Editor via the **CoplayDev "MCP for Unity" bridge** already
installed in this project (`Library/PackageCache/com.coplaydev.unity-mcp@…`).

**STATUS: REAL bridge client, unit-tested in the sandbox; LIVE-PENDING against a
real Editor.** All socket plumbing lives inside one `UnityBridge` client class in
`server.py`. The verb→command mapping, port discovery, and FRAMING=1 protocol are
implemented and unit-tested against a fake in-memory bridge server (35 tests,
green in this Linux sandbox, no live Editor, no `mcp` SDK). The real handshake
against a running Editor can only be exercised on the user's Mac (see **Mac
live-run smoke** below) — the Linux sandbox cannot reach the Mac's localhost
bridge.

## Run modes

- **MCP stdio** — `python3 server.py mcp` (explicit) or a bare `python3 server.py`
  (serves MCP if the `mcp` SDK is installed, else prints `health` + an install
  hint). This is the live transport a greenoke launch drives. The launcher wires it
  in via `--mcp-config greenoke/adapter/provider/mcp.json` — opt-in, **not** a
  repo-wide `.mcp.json`, so the provider is live only when greenoke is launched.
- **CLI one-shots** — `python3 server.py <verb> ...` (tests + manual sanity checks).

```
python3 server.py mcp                  serve the 5 verbs over MCP stdio
python3 server.py health               one-shot: health() JSON
python3 server.py inspect <query>      one-shot: inspect()
python3 server.py verify [path]        one-shot: verify() (live EditMode tests)
python3 server.py screenshot [out]     one-shot: screenshot() (Game view -> file)
python3 server.py build <spec-json>    one-shot: build()
```

Heavy deps stay lazy: importing `server` pulls in only the stdlib (socket/json/…).
The `mcp` SDK and a live bridge are never required to import the module or run the
tests.

## The live system = the MCP-for-Unity bridge

The provider connects to the running Unity Editor over the bridge's loopback TCP
socket. Two facts, confirmed from the package source:

### Port discovery (`Helpers/PortManager.cs`)

1. Read `~/.unity-mcp/unity-mcp-port-<hash>.json`, field `unity_port`, where
   `<hash>` = first 8 hex chars of `SHA1(UTF-8 of the Unity project's dataPath)`
   and `dataPath` == `<repo_root>/Assets`.
2. Fallback to legacy `~/.unity-mcp/unity-mcp-port.json`.
3. Fallback to the default port **6400**.

The hash is computed at runtime from the *actual* `Assets` path, so the stamp is
correct on whatever machine the Editor runs on (the Mac), not the sandbox.

### Transport: WELCOME + FRAMING=1 (`Services/Transport/Transports/StdioBridgeHost.cs`)

1. TCP connect to `127.0.0.1:<port>`.
2. The server sends a newline-terminated handshake line
   `WELCOME UNITY-MCP 1 FRAMING=1\n` **before** framing begins — the client reads
   and discards it first.
3. Both directions then use length-prefixed frames: an **8-byte big-endian uint64**
   payload length, followed by that many bytes of UTF-8 JSON (max frame 64 MiB,
   zero-length frames rejected).
4. A bare `ping` frame is answered with a pong frame — used for reachability.
5. Command frames are `{"type": "<command>", "params": {…}}`; responses are
   `{"status": "success"|"error", "result": {…}}` (or an `error` field). Confirmed
   from `Models/Command.cs`, `Services/Transport/TransportCommandDispatcher.cs`, and
   `Tools/CommandRegistry.cs`.

All of this lives inside `UnityBridge` (`server.py`). Agents call verbs, never the
socket — contract guarantee #2.

## Verb → bridge-command mapping

Command `type` names below are CONFIRMED from the `[McpForUnityTool("…")]`
attributes in `Editor/Tools/*.cs` and `CommandRegistry.cs`.

| Verb | Bridge command(s) (`type`) | Params / notes |
|---|---|---|
| `health()` | `ping` (raw framed) | Socket connect + `ping`→`pong`. Plus the `code_version` git stamp. Never throws. |
| `inspect(query)` | `manage_editor` / `manage_scene` / `read_console` / `find_gameobjects` | `query` is interpreted: `"editor"`→`manage_editor {action:"get_state"}`; `"activeScene"`→`manage_scene {action:"get_active"}`; `"hierarchy"`→`manage_scene {action:"get_hierarchy"}`; `"console"`→`read_console {action:"get",types:["error","warning"]}`; any other string→`find_gameobjects {searchTerm:…}`. A JSON `{"type":…,"params":…}` selector passes straight through. All read-only. |
| `verify(artifact?)` | `run_tests` + `get_test_job` (poll) and `read_console` | Runs **EditMode** tests (`run_tests {mode:"EditMode"}` → poll `get_test_job {job_id,…}` until done) AND reads console errors (`read_console {action:"get",types:["error"]}`). Each is a named check. Optional `artifact` path is also well-formedness-checked via `validate_artifact`. Includes the `code_version` stamp. |
| `build(spec)` | `refresh_unity` / `execute_menu_item` / `manage_scene` (screenshot) | `{"kind":"refresh"}`→`refresh_unity {mode,scope,compile,wait_for_ready}`; `{"kind":"menu","menu_path":…}`→`execute_menu_item {menu_path:…}`; `{"kind":"capture","out":…}`→`manage_scene {action:"screenshot",captureSource:…}` then **temp→verify→atomic-swap** into `out`. |
| `screenshot(selector?)` | `manage_scene` (`action:"screenshot"`) | `captureSource` ∈ `"game_view"` (default) / `"scene_view"`; the bridge returns the file's absolute `fullPath`, returned to the agent as `image_ref`. |

### Why `manage_scene action=screenshot`

`Editor/Tools/ManageScene.cs` exposes a `screenshot` action that uses Unity's
capture pipeline (`ScreenshotUtility` / `ScreenCapture`) and returns the saved
file's absolute `fullPath` (and an Assets-relative `path`). This is the existing,
confirmed capture mechanism — preferred over injecting raw C# via `execute_code`.

## Artifact safety (contract guarantee #3)

When `build()` produces a **file** artifact (the `capture` kind), the provider:

1. Drives the bridge to capture the screenshot into a temp file (the Editor writes
   it under `Assets/` and reports its absolute path).
2. Runs `validate_artifact` on that temp file (a real, non-empty PNG).
3. Only on a clean verify does it **atomically swap** the file into the requested
   `out` (`os.replace`). A broken capture never replaces a prior good artifact — and
   if a prior artifact existed, it is proven byte-for-byte intact on the failure
   path. This guarantee lives in the provider, not the calling agent.

Non-file builds (`refresh`, `menu`) drive the live Editor and return its log; they
produce no file artifact to swap.

## `health()` + stale_system_guard

`health().code_version` is `git rev-parse --short HEAD` of this repo plus a coarse
`-dirty` suffix when the working tree has uncommitted edits. The Editor compiles and
runs out of THIS checkout, so the working-tree stamp is the version it executes
after its next domain reload. Before trusting any `verify()` as proof of a fix, the
agent compares this stamp to its own working-tree stamp; a mismatch means the Editor
is running stale code and the verdict cannot go green.

**Known limitation (documented, not a bug).** The `-dirty` flag is a **boolean, not
a content hash**. The stamp changes reliably on **commit** (the SHA moves), but two
**different** uncommitted edits at the **same HEAD** collide on the identical stamp
(`<sha>-dirty`). So `stale_system_guard` reliably catches an Editor running an *old
commit*, but **cannot** distinguish two distinct dirty trees at the same HEAD. This
is the contract's documented weaker form (commit-id + coarse dirty flag); the strong
form is a content hash. **Commit before trusting a same-HEAD cross-tree comparison.**

## Mac live-run smoke

The unit tests prove the protocol logic in the sandbox, but the real handshake
needs the Editor. On the user's **Mac**, with the **Unity Editor open** and the
**MCP-for-Unity bridge connected** (you should see "StdioBridgeHost started on port
…" in the Unity console), from the repo root:

```bash
python3 greenoke/adapter/provider/server.py health
#   expect: {"reachable": true, "code_version": "<sha>[-dirty]", "detail": "Unity MCP bridge reachable at 127.0.0.1:<port>"}

python3 greenoke/adapter/provider/server.py inspect activeScene
#   expect: {"ok": true, "state": { …active scene info… }, ...}

python3 greenoke/adapter/provider/server.py screenshot /tmp/meltitdown-shot.png
#   expect: {"ok": true, "image_ref": "/…/Assets/…png"}  (a readable PNG)
```

**Live validation requires the Editor running on the user's Mac.** The Linux sandbox
where these tests run cannot reach the Mac's `127.0.0.1` bridge, so `reachable:true`
and the test/console/screenshot round-trips are LIVE-PENDING until run there. The
provider fails closed and self-heals: if the Editor is closed, `health()` returns
`reachable:false` with a hint and the verbs return a clear "open the Unity Editor and
retry" error rather than crashing.

## Files

- `server.py` — the verb implementation + `UnityBridge` socket client + MCP stdio
  and CLI transports.
- `validate_artifact.py` — standalone well-formedness checker (NUnit results XML /
  PNG screenshot), exit 0/nonzero + JSON. Drives `verify()`'s artifact gate.
- `mcp.json` — launcher-loaded MCP config (`server.py mcp`), passed via `--mcp-config`.
- `tests/test_provider.py` — unit tests with a fake in-memory bridge server (port
  discovery, WELCOME+framing round-trip, big-endian framing, each verb's command
  construction + parsing, `code_version` format/dirty, graceful unreachable, and
  build() temp→verify→atomic-swap). Run: `python3 -m pytest greenoke/adapter/provider/tests/ -q`.
