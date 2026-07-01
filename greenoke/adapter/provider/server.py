#!/usr/bin/env python3
"""greenoke capability provider for the MeltItDown Unity project — REAL implementation.

Entry point declared in
`greenoke/adapter/greenoke.adapter.json -> capability_provider.launch`:

    python3 greenoke/adapter/provider/server.py mcp

It maps the greenoke verb contract (inspect / build / verify / screenshot /
health — see greenoke/core/contracts/capability-provider.md) onto the LIVE Unity
Editor, driven through the CoplayDev "MCP for Unity" bridge already installed in
this project (Library/PackageCache/com.coplaydev.unity-mcp@...).

The bridge is a TCP server the running Unity Editor exposes on localhost. ALL
socket plumbing lives inside ONE `UnityBridge` client class in this file —
greenoke agents only ever call the five verbs, never the wire protocol. This is
the contract's guarantee #2 (high-level verbs, not raw transport).

Design constraints honored here:
  * The `mcp` SDK and a live bridge are LAZY: importing this module pulls in only
    the stdlib (socket/json/subprocess/...). Tests import the module and exercise
    the bridge client against a fake in-memory loopback server WITHOUT the SDK.
  * Self-healing / late-start (guarantee #1): the bridge attaches lazily and
    reconnects on its own. If Unity is down, `health()` returns reachable:false
    with a hint instead of throwing — the agent retries, never wedges.
  * Artifact safety (guarantee #3): when `build()` produces a FILE artifact it
    renders to a TEMP path, runs `verify()` on it, and only ATOMICALLY swaps into
    the live location on a clean verify. A broken build never replaces a good
    artifact. This belongs to the provider, not the calling agent.

Run modes (mirror the youtube_shorts_bot provider):
  python3 server.py mcp              # serve the 5 verbs over MCP stdio (needs `mcp`)
  python3 server.py                  # (bare) serve MCP if installed, else print health
  python3 server.py health           # one-shot: health() JSON
  python3 server.py inspect <query>  # one-shot inspect (query JSON or plain string)
  python3 server.py verify [path]    # one-shot verify (live EditMode tests + console)
  python3 server.py screenshot [out] # one-shot screenshot (Game view -> file)
  python3 server.py build <spec-json># one-shot build (refresh / recompile / capture)
"""

import hashlib
import json
import os
import socket
import struct
import subprocess
import sys
import tempfile
import time

_HERE = os.path.dirname(os.path.abspath(__file__))
# repo_root = <repo>/ ; this file is at <repo>/greenoke/adapter/provider/server.py
_REPO_ROOT = os.path.normpath(os.path.join(_HERE, "..", "..", ".."))
# The Unity project's dataPath (== <repo_root>/Assets) is what the bridge hashes
# to name its per-project port file. Computed at runtime so the stamp is correct on
# whatever machine the Editor actually runs on (the Mac), not the sandbox.
_DATA_PATH = os.path.join(_REPO_ROOT, "Assets")

# Make the sibling validate_artifact importable without side effects.
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

# Bridge defaults extracted/confirmed from the package source:
#   PortManager.cs  -> DefaultPort 6400, registry dir ~/.unity-mcp,
#                      file unity-mcp-port-<sha1(dataPath)[:8]>.json, legacy unity-mcp-port.json
#   StdioBridgeHost.cs -> WELCOME line, FRAMING=1 (8-byte big-endian length prefix), ping->pong
_DEFAULT_PORT = 6400
_REGISTRY_DIR = os.path.join(os.path.expanduser("~"), ".unity-mcp")
_WELCOME_PREFIX = "WELCOME UNITY-MCP"
_MAX_FRAME_BYTES = 64 * 1024 * 1024
_CONNECT_TIMEOUT = 2.0
_IO_TIMEOUT = 30.0
# Where build() writes captured artifacts inside the repo (gitignore-friendly).
_RUN_DIR = os.path.join(_REPO_ROOT, ".greenoke-run")


# ── code_version stamp (powers stale_system_guard) ────────────────────────────

def code_version() -> str:
    """A stamp of the code the Editor is running: git short-SHA + dirty flag.

    Changes when the working-tree code changes — this is what lets an agent DETECT
    a stale Editor (stale_system_guard) instead of declaring false success. The
    Editor compiles and runs out of THIS checkout, so the working-tree stamp is the
    version it executes after its next domain reload. `-dirty` means the tree has
    uncommitted edits the still-running Editor has not necessarily recompiled yet.

    KNOWN LIMITATION — `-dirty` is a BOOLEAN, not a content hash. The stamp changes
    reliably on COMMIT (the SHA moves), but two DIFFERENT dirty trees at the SAME
    HEAD produce the IDENTICAL stamp (`<sha>-dirty`), so stale_system_guard can pass
    falsely between two distinct uncommitted edits of the same HEAD. This is the
    documented WEAKER form (commit-id + coarse dirty flag) from the contract; the
    strong form is a content hash. Accepted as a coarse signal: the guard catches
    the common stale case (Editor on an OLD commit), not same-HEAD dirty-vs-dirty.
    Commit before trusting a same-HEAD cross-tree comparison.
    """
    try:
        sha = subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"],
            cwd=_REPO_ROOT, capture_output=True, text=True, timeout=10,
        ).stdout.strip() or "unknown"
        dirty = subprocess.run(
            ["git", "status", "--porcelain"],
            cwd=_REPO_ROOT, capture_output=True, text=True, timeout=10,
        ).stdout.strip()
        return f"{sha}{'-dirty' if dirty else ''}"
    except Exception:
        return "unknown"


# ── port discovery (PortManager.cs parity) ────────────────────────────────────

def _project_hash(data_path: str = None) -> str:
    """First 8 hex of SHA1(UTF-8 of the Unity project's dataPath). Mirrors
    PortManager.ComputeProjectHash / StdioBridgeHost.ComputeProjectHash."""
    raw = (data_path if data_path is not None else _DATA_PATH) or ""
    try:
        return hashlib.sha1(raw.encode("utf-8")).hexdigest()[:8]
    except Exception:
        return "default"


def discover_port(data_path: str = None, registry_dir: str = None) -> int:
    """Resolve the bridge TCP port the running Editor is listening on.

    Order (matches the package): project-scoped file
    `~/.unity-mcp/unity-mcp-port-<hash>.json` (field `unity_port`), then legacy
    `~/.unity-mcp/unity-mcp-port.json`, then the default 6400. Never throws — a
    missing/garbled file falls through to the next candidate."""
    reg = registry_dir or _REGISTRY_DIR
    scoped = os.path.join(reg, f"unity-mcp-port-{_project_hash(data_path)}.json")
    legacy = os.path.join(reg, "unity-mcp-port.json")
    for path in (scoped, legacy):
        port = _read_port_file(path)
        if port:
            return port
    return _DEFAULT_PORT


def _read_port_file(path: str):
    try:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        port = int(data.get("unity_port") or 0)
        return port if port > 0 else None
    except (IOError, ValueError, TypeError):
        return None


# ── UnityBridge: the single socket client (all IPC lives here) ────────────────

class UnityBridge:
    """Loopback TCP client for the MCP-for-Unity bridge. Owns the WELCOME
    handshake and the FRAMING=1 wire format (8-byte big-endian length prefix +
    UTF-8 JSON). Self-healing: connects lazily, reconnects on a dead socket. The
    ONLY place in the provider that touches a socket — agents call verbs, not this.
    """

    def __init__(self, host: str = "127.0.0.1", port: int = None,
                 data_path: str = None, registry_dir: str = None,
                 connect_timeout: float = _CONNECT_TIMEOUT, io_timeout: float = _IO_TIMEOUT):
        self.host = host
        self._explicit_port = port
        self._data_path = data_path
        self._registry_dir = registry_dir
        self.connect_timeout = connect_timeout
        self.io_timeout = io_timeout
        self._sock = None
        self.port = None

    # -- connection lifecycle -------------------------------------------------

    def _resolve_port(self) -> int:
        if self._explicit_port:
            return self._explicit_port
        return discover_port(self._data_path, self._registry_dir)

    def connect(self):
        """(Re)establish the socket and consume the WELCOME line. Idempotent: a
        live socket is reused. Raises OSError if nothing is listening."""
        if self._sock is not None:
            return self._sock
        self.port = self._resolve_port()
        sock = socket.create_connection((self.host, self.port), timeout=self.connect_timeout)
        sock.settimeout(self.io_timeout)
        try:
            self._read_welcome(sock)
        except Exception:
            try:
                sock.close()
            finally:
                pass
            raise
        self._sock = sock
        return sock

    def close(self):
        if self._sock is not None:
            try:
                self._sock.close()
            except OSError:
                pass
            self._sock = None

    def _read_welcome(self, sock):
        """Read and discard the newline-terminated `WELCOME UNITY-MCP 1 FRAMING=1`
        line the server sends on connect, BEFORE framing begins."""
        buf = bytearray()
        deadline = time.monotonic() + self.connect_timeout
        while b"\n" not in buf:
            if time.monotonic() > deadline:
                raise OSError("timed out waiting for WELCOME handshake")
            chunk = sock.recv(256)
            if not chunk:
                raise OSError("connection closed before WELCOME handshake")
            buf.extend(chunk)
            if len(buf) > 1024:  # a sane cap; the welcome line is short
                break
        line = bytes(buf).split(b"\n", 1)[0].decode("ascii", "replace")
        if _WELCOME_PREFIX not in line:
            raise OSError(f"unexpected handshake (no WELCOME): {line!r}")
        return line

    # -- framing --------------------------------------------------------------

    @staticmethod
    def _recv_exact(sock, count: int) -> bytes:
        """Read exactly `count` bytes or raise. Mirrors ReadExactAsync."""
        buf = bytearray()
        while len(buf) < count:
            chunk = sock.recv(count - len(buf))
            if not chunk:
                raise OSError("connection closed before reading expected bytes")
            buf.extend(chunk)
        return bytes(buf)

    def _write_frame(self, sock, payload: bytes):
        """8-byte big-endian uint64 length prefix, then the payload (FRAMING=1)."""
        if len(payload) > _MAX_FRAME_BYTES:
            raise OSError(f"frame too large: {len(payload)}")
        header = struct.pack(">Q", len(payload))
        sock.sendall(header + payload)

    def _read_frame(self, sock) -> bytes:
        header = self._recv_exact(sock, 8)
        (length,) = struct.unpack(">Q", header)
        if length == 0:
            raise OSError("zero-length frames are not allowed")
        if length > _MAX_FRAME_BYTES:
            raise OSError(f"invalid framed length: {length}")
        return self._recv_exact(sock, length)

    # -- request/response -----------------------------------------------------

    def _send_raw(self, text: str) -> str:
        """One framed round-trip with a single self-healing reconnect on a dead
        socket. Returns the decoded UTF-8 response frame."""
        for attempt in (0, 1):
            try:
                sock = self.connect()
                self._write_frame(sock, text.encode("utf-8"))
                return self._read_frame(sock).decode("utf-8")
            except OSError:
                self.close()
                if attempt == 1:
                    raise
        raise OSError("unreachable")

    def ping(self) -> bool:
        """`ping` -> pong reachability probe. Never raises — False if unreachable."""
        try:
            resp = self._send_raw("ping")
            return "pong" in resp
        except OSError:
            return False

    def command(self, type_: str, params: dict = None) -> dict:
        """Send `{"type": type_, "params": {...}}` and return the parsed bridge
        response dict `{"status": "...", "result"|"error": ...}`. Raises OSError if
        the bridge is unreachable; raises ValueError on an unparseable response."""
        payload = json.dumps({"type": type_, "params": params or {}})
        raw = self._send_raw(payload)
        try:
            return json.loads(raw)
        except ValueError as e:
            raise ValueError(f"unparseable bridge response: {raw[:200]!r}") from e

    def reachable(self) -> bool:
        """Cheap connectability check: can we connect + ping? Never raises."""
        try:
            self.connect()
        except OSError:
            return False
        return self.ping()


def _bridge(**kwargs) -> "UnityBridge":
    """Factory so the module-level verbs share one construction point (and tests
    can monkeypatch it)."""
    return UnityBridge(**kwargs)


def _unwrap(resp: dict):
    """Normalize a bridge response into (ok, result, error)."""
    status = (resp or {}).get("status")
    if status == "success":
        return True, resp.get("result"), None
    err = (resp or {}).get("error") or (resp or {}).get("result") or "bridge error"
    return False, resp.get("result"), err


# ── health(): bridge reachability + code_version ──────────────────────────────

def health() -> dict:
    """Is the live Unity Editor (its MCP bridge) reachable, and what code version
    is it running? Cheap, safe to poll, NEVER throws.

    * `reachable` — the bridge socket connects AND answers a ping. A down/late-start
      Editor yields reachable:false with a hint (self-healing): the agent retries.
    * `code_version` — git short-SHA + dirty flag of THIS working tree. Powers
      stale_system_guard: the agent compares it to its own working-tree stamp; a
      mismatch means the Editor is running stale code and a verify() must not be
      trusted as proof of a fix.
    """
    cv = code_version()
    try:
        br = _bridge()
        reachable = br.reachable()
        port = br.port if br.port else discover_port()
        br.close()
    except Exception as e:  # never throw out of health()
        return {
            "reachable": False,
            "code_version": cv,
            "detail": f"bridge probe error: {e}; is the Unity Editor open with the "
                      "MCP-for-Unity bridge connected?",
        }
    detail = (
        f"Unity MCP bridge reachable at 127.0.0.1:{port}"
        if reachable else
        f"Unity MCP bridge not reachable at 127.0.0.1:{port} — open the Unity Editor "
        "with the MCP-for-Unity bridge connected and retry"
    )
    return {"reachable": reachable, "code_version": cv, "detail": detail}


# ── inspect(): read-only live state via the bridge ────────────────────────────

# query keyword -> (bridge command type, params). All read-only / side-effect-free.
def _inspect_plan(q):
    """Map a free-form query (string or dict) to a (type, params) bridge call.

    Strings:
      "editor" / "state"      -> manage_editor get_state    (play/pause/compile state)
      "scene" / "activeScene" -> manage_scene  get_active   (active scene info)
      "hierarchy"             -> manage_scene  get_hierarchy (scene hierarchy, paged)
      "console"               -> read_console  get          (recent errors+warnings)
      "<other text>"          -> find_gameobjects (by name)  — a GO query
    Dicts:
      {"type": "...", "params": {...}}  -> passed straight through (advanced)
      {"kind": "...", ...}              -> kind dispatched like the strings above
    Returns (type, params) or None if unmappable.
    """
    if isinstance(q, dict):
        if q.get("type"):
            return q["type"], q.get("params") or {}
        kind = (q.get("kind") or "").lower()
        if kind:
            plan = _inspect_string(kind, q)
            return plan
        return None
    if q is None:
        q = "editor"
    if isinstance(q, str):
        s = q.strip()
        if s.startswith("{"):
            try:
                return _inspect_plan(json.loads(s))
            except ValueError:
                pass  # treat as a plain GO-name query below
        return _inspect_string(s, {})
    return None


def _inspect_string(s: str, extra: dict):
    low = s.lower()
    if low in ("editor", "state", "editor_state", "editorstate"):
        return "manage_editor", {"action": "get_state"}
    if low in ("scene", "activescene", "active_scene", "active"):
        return "manage_scene", {"action": "get_active"}
    if low in ("hierarchy", "get_hierarchy", "tree"):
        params = {"action": "get_hierarchy"}
        if "pageSize" in extra:
            params["pageSize"] = extra["pageSize"]
        if "maxDepth" in extra:
            params["maxDepth"] = extra["maxDepth"]
        return "manage_scene", params
    if low in ("console", "logs", "log"):
        return "read_console", {"action": "get",
                                "types": extra.get("types", ["error", "warning"]),
                                "count": extra.get("count", 50),
                                "format": "plain"}
    # default: treat the string as a GameObject name to find
    name = extra.get("name", s)
    return "find_gameobjects", {"searchTerm": name}


def inspect(query=None) -> dict:
    """Read live Unity state without changing it. Idempotent, side-effect-free.

    Interprets `query` (free-form string or JSON) and issues the matching READ-ONLY
    bridge command (editor state / active scene / hierarchy / console / find
    gameobject). Returns {ok, state, error}. Never mutates the Editor.
    """
    plan = _inspect_plan(query)
    if plan is None:
        return {"ok": False, "state": None, "error": f"unmappable inspect query: {query!r}"}
    type_, params = plan
    try:
        resp = _bridge().command(type_, params)
    except OSError as e:
        return {"ok": False, "state": None,
                "error": f"bridge unreachable: {e} — open the Unity Editor and retry"}
    except ValueError as e:
        return {"ok": False, "state": None, "error": str(e)}
    ok, result, err = _unwrap(resp)
    return {"ok": ok, "state": result if ok else None,
            "error": None if ok else err, "command": type_}


# ── verify(): proof-of-life — EditMode tests + console compile errors ─────────

def verify(artifact=None, mode: str = "EditMode", timeout: float = 240.0,
           poll_interval: float = 2.0) -> dict:
    """Proof-of-life: run Unity EditMode tests through the bridge AND read the
    console for compile errors, surfacing each as a named check. Returns
    {ok, checks:[{name,ok,detail}], code_version, error?}.

    `artifact` is accepted for contract symmetry (an artifact_ref / path). When it
    is a path to a NUnit results XML or a screenshot PNG, verify() ALSO validates
    that file's well-formedness via the sibling validate_artifact — so build()'s
    temp->verify->swap can gate a produced file. The live-Editor checks (tests +
    console) always run; they need the bridge, and degrade to a failed check with a
    hint when it is down (never throws).

    code_version is included so the caller can enforce stale_system_guard.
    """
    cv = code_version()
    checks = []

    # 0. Optional artifact-file well-formedness (used by build()'s gate).
    if artifact and os.path.isfile(artifact):
        fok, fdetail = _validate_artifact_file(artifact)
        checks.append({"name": "artifact_wellformed", "ok": fok, "detail": fdetail})

    # 1. Console compile errors (read-only) — a fast, independent signal.
    console_ok, console_detail = _verify_console()
    checks.append({"name": "no_compile_errors", "ok": console_ok, "detail": console_detail})

    # 2. EditMode tests via the run-tests job (poll until done).
    tests_ok, tests_detail = _verify_tests(mode, timeout, poll_interval)
    checks.append({"name": f"{mode.lower()}_tests_pass", "ok": tests_ok, "detail": tests_detail})

    ok = all(c["ok"] for c in checks)
    return {"ok": ok, "checks": checks, "code_version": cv,
            "error": None if ok else "; ".join(c["detail"] for c in checks if not c["ok"])}


def _validate_artifact_file(path: str):
    """Delegate to the sibling validate_artifact for file well-formedness. Lazy
    import so a missing sibling degrades to a clear message, not an import crash."""
    try:
        from validate_artifact import validate_artifact as _va  # sibling, stdlib-only
    except Exception as e:
        return False, f"validate_artifact unavailable: {e}"
    res = _va(path)
    return bool(res.get("ok")), res.get("detail") or res.get("error") or "ok"


def _verify_console():
    """Read errors from the console; OK iff there are no error-level entries.
    Returns (ok, detail). A down bridge -> (False, hint)."""
    try:
        resp = _bridge().command("read_console",
                                 {"action": "get", "types": ["error"],
                                  "count": 50, "format": "plain",
                                  "includeStacktrace": False})
    except OSError as e:
        return False, f"bridge unreachable: {e} — open the Unity Editor and retry"
    except ValueError as e:
        return False, str(e)
    ok, result, err = _unwrap(resp)
    if not ok:
        return False, f"read_console failed: {err}"
    errors = _extract_console_errors(result)
    if errors:
        sample = errors[0] if errors else ""
        return False, f"{len(errors)} console error(s); first: {str(sample)[:160]}"
    return True, "no console errors"


def _extract_console_errors(result):
    """Best-effort pull of error entries from read_console's varied result shapes
    (list, {lines:[...]}, {entries:[...]}, {messages:[...]}, or a plain string)."""
    if result is None:
        return []
    if isinstance(result, str):
        lines = [ln for ln in result.splitlines() if ln.strip()]
        return lines
    if isinstance(result, list):
        return result
    if isinstance(result, dict):
        for key in ("lines", "entries", "messages", "logs", "data"):
            val = result.get(key)
            if isinstance(val, list):
                return val
            if isinstance(val, str) and val.strip():
                return [ln for ln in val.splitlines() if ln.strip()]
        # A count-only shape.
        cnt = result.get("count")
        if isinstance(cnt, int):
            return list(range(cnt))
    return []


def _verify_tests(mode: str, timeout: float, poll_interval: float):
    """Start a run_tests job and poll get_test_job until it finishes. Returns
    (ok, detail). A down bridge -> (False, hint)."""
    try:
        br = _bridge()
        start = br.command("run_tests", {"mode": mode})
    except OSError as e:
        return False, f"bridge unreachable: {e} — open the Unity Editor and retry"
    except ValueError as e:
        return False, str(e)
    ok, result, err = _unwrap(start)
    if not ok:
        return False, f"run_tests failed: {err}"
    job_id = (result or {}).get("job_id") or (result or {}).get("jobId")
    if not job_id:
        # Some bridge builds may run synchronously and return results inline.
        return _summarize_test_result(result)

    deadline = time.monotonic() + timeout
    last = result
    while time.monotonic() < deadline:
        time.sleep(poll_interval)
        try:
            poll = br.command("get_test_job", {"job_id": job_id,
                                               "includeDetails": True,
                                               "includeFailedTests": True})
        except OSError as e:
            return False, f"bridge dropped mid-test: {e}"
        except ValueError as e:
            return False, str(e)
        pok, presult, perr = _unwrap(poll)
        if not pok:
            return False, f"get_test_job failed: {perr}"
        last = presult or {}
        status = str(last.get("status", "")).lower()
        if status in ("completed", "finished", "done", "success", "failed", "error", "passed"):
            return _summarize_test_result(last)
    return False, f"tests did not finish within {timeout:.0f}s (job {job_id})"


def _summarize_test_result(result):
    """Reduce a test-job result to (ok, human detail). Tolerant of field naming."""
    r = result or {}
    failed = _first_int(r, ("failed", "failedCount", "failures", "fail_count"))
    passed = _first_int(r, ("passed", "passedCount", "passCount"))
    total = _first_int(r, ("total", "testCount", "count"))
    status = str(r.get("status", "")).lower()
    if failed is None and status in ("passed", "success", "completed", "done", "finished"):
        failed = 0
    ok = (failed == 0) if failed is not None else (status in ("passed", "success"))
    parts = []
    if passed is not None:
        parts.append(f"{passed} passed")
    if failed is not None:
        parts.append(f"{failed} failed")
    if total is not None:
        parts.append(f"{total} total")
    detail = (", ".join(parts) or f"status={status or 'unknown'}")
    return bool(ok), detail


def _first_int(d: dict, keys):
    for k in keys:
        v = d.get(k)
        if isinstance(v, bool):
            continue
        if isinstance(v, int):
            return v
        if isinstance(v, str) and v.isdigit():
            return int(v)
    return None


# ── build(): drive the live system; temp -> verify -> atomic swap ─────────────

def build(spec=None) -> dict:
    """Interpret a build `spec` and drive the LIVE Editor via the bridge. Returns
    {ok, artifact_ref?, log?, error?, code_version}.

    spec (dict, or JSON string):
      {"kind": "refresh", "compile": "request"?, "scope": "all"|"scripts"?}
          Refresh the asset DB and (optionally) request a recompile. No file
          artifact — returns the refresh log. Use after editing scripts so the
          next verify() runs the NEW code.

      {"kind": "capture", "out": "<path>"?, "source": "game_view"|"scene_view"?}
          Capture a screenshot as a FILE artifact, made artifact-safe BY THE
          PROVIDER: capture to a TEMP path -> verify() the file -> only on a clean
          verify atomically swap into `out`. A broken capture never replaces a good
          prior artifact. Defaults `out` to .greenoke-run/screenshot.png.

      {"kind": "menu", "menu_path": "<Unity menu path>"}
          Execute a Unity menu item (e.g. a custom build/bake tool). No file
          artifact; returns the bridge log.

    Heavy work stays in the Editor; the provider never renders bytes itself.
    """
    if isinstance(spec, str):
        try:
            spec = json.loads(spec)
        except ValueError:
            return _build_result(False, None, None, f"unparseable build spec: {spec!r}")
    if not isinstance(spec, dict):
        return _build_result(False, None, None, "build() requires a spec dict")

    kind = (spec.get("kind") or "refresh").lower()
    if kind == "refresh":
        return _build_refresh(spec)
    if kind == "capture":
        return _build_capture(spec)
    if kind == "menu":
        return _build_menu(spec)
    return _build_result(False, None, None, f"unknown build kind: {kind!r}")


def _build_refresh(spec) -> dict:
    params = {
        "mode": spec.get("mode", "force"),
        "scope": spec.get("scope", "all"),
        "compile": spec.get("compile", "request"),
        "wait_for_ready": spec.get("wait_for_ready", True),
    }
    try:
        resp = _bridge().command("refresh_unity", params)
    except OSError as e:
        return _build_result(False, None, None,
                             f"bridge unreachable: {e} — open the Unity Editor and retry")
    except ValueError as e:
        return _build_result(False, None, None, str(e))
    ok, result, err = _unwrap(resp)
    return _build_result(ok, None, json.dumps(result) if result is not None else None,
                         None if ok else err)


def _build_menu(spec) -> dict:
    menu_path = spec.get("menu_path") or spec.get("menuPath")
    if not menu_path:
        return _build_result(False, None, None, "menu build needs a 'menu_path'")
    try:
        resp = _bridge().command("execute_menu_item", {"menu_path": menu_path})
    except OSError as e:
        return _build_result(False, None, None,
                             f"bridge unreachable: {e} — open the Unity Editor and retry")
    except ValueError as e:
        return _build_result(False, None, None, str(e))
    ok, result, err = _unwrap(resp)
    return _build_result(ok, None, json.dumps(result) if result is not None else None,
                         None if ok else err)


def _build_capture(spec) -> dict:
    """Provider-owned artifact safety: capture -> verify the file -> atomic swap.

    The live screenshot is taken by the bridge (manage_scene action=screenshot),
    which writes a PNG into the project's Assets folder and returns its absolute
    `fullPath`. We treat THAT as the temp render, verify the file, and only on a
    clean file-verify atomically move it into the requested `out`. A failed capture
    leaves any prior `out` untouched.
    """
    out = spec.get("out") or os.path.join(_RUN_DIR, "screenshot.png")
    out = os.path.abspath(out)
    source = spec.get("source", "game_view")
    prior_hash = _hash_file(out)  # None if no prior artifact

    cap = _capture_to_temp(source, spec)
    if not cap["ok"]:
        return _build_result(False, out, None, cap["error"])
    temp_png = cap["path"]

    # VERIFY the captured file's well-formedness (a real PNG, non-empty).
    fok, fdetail = _validate_artifact_file(temp_png)
    if not fok:
        # Do NOT place. Prior artifact (if any) is untouched.
        if prior_hash is not None and _hash_file(out) != prior_hash:
            return _build_result(False, out, None,
                                 "INVARIANT VIOLATION: prior artifact changed on a failed capture")
        return _build_result(False, out, None,
                             f"captured screenshot failed verify(): {fdetail}; prior artifact intact")

    # ATOMIC swap into place (same filesystem rename).
    try:
        os.makedirs(os.path.dirname(out), exist_ok=True)
        staged = out + ".greenoke-tmp"
        _move_file(temp_png, staged)
        os.replace(staged, out)
    except OSError as e:
        return _build_result(False, out, None, f"atomic swap failed: {e}")
    return _build_result(True, out, f"captured via temp->verify->swap ({source})", None)


def _capture_to_temp(source: str, spec) -> dict:
    """Ask the bridge to capture a Game/Scene-view screenshot. Returns
    {ok, path, error}. The bridge writes into Assets and returns fullPath; that file
    is our temp render for the verify->swap gate."""
    params = {"action": "screenshot", "captureSource": source}
    if spec.get("camera"):
        params["camera"] = spec["camera"]
    if spec.get("maxResolution"):
        params["maxResolution"] = spec["maxResolution"]
    try:
        resp = _bridge().command("manage_scene", params)
    except OSError as e:
        return {"ok": False, "path": None,
                "error": f"bridge unreachable: {e} — open the Unity Editor and retry"}
    except ValueError as e:
        return {"ok": False, "path": None, "error": str(e)}
    ok, result, err = _unwrap(resp)
    if not ok:
        return {"ok": False, "path": None, "error": f"screenshot command failed: {err}"}
    full = (result or {}).get("fullPath") or (result or {}).get("path")
    if not full or not os.path.isfile(full):
        return {"ok": False, "path": None,
                "error": f"bridge reported no readable screenshot file (got {full!r})"}
    return {"ok": True, "path": full, "error": None}


def _build_result(ok: bool, artifact_ref, log, error) -> dict:
    return {"ok": ok, "artifact_ref": artifact_ref, "log": log,
            "error": error, "code_version": code_version()}


def _hash_file(path):
    """sha256 of a file, or None if it doesn't exist. Proves a prior artifact was
    untouched across a failed build."""
    if not path or not os.path.isfile(path):
        return None
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def _move_file(src, dst):
    """Move src->dst, preferring an atomic rename and falling back to copy+unlink
    across filesystems."""
    try:
        os.replace(src, dst)
    except OSError:
        import shutil
        shutil.copyfile(src, dst)
        try:
            os.remove(src)
        except OSError:
            pass


# ── screenshot(): capture the Game/Scene view to a readable file ──────────────

def screenshot(selector=None) -> dict:
    """Capture the Game view (default) or Scene view as proof-of-life. Returns
    {ok, image_ref, error?} — `image_ref` is an absolute filesystem path the agent
    can read.

    `selector` (optional): "scene_view" | "game_view", or a dict
    {"source": "...", "camera": "...", "out": "<path>"}. Realized via the bridge's
    manage_scene screenshot action (confirmed in Tools/ManageScene.cs), which uses
    Unity's capture pipeline and returns the file's absolute path.
    """
    spec = {"kind": "capture"}
    if isinstance(selector, str) and selector.strip():
        s = selector.strip()
        if s.startswith("{"):
            try:
                d = json.loads(s)
                spec.update(d)
            except ValueError:
                spec["source"] = s
        else:
            spec["source"] = s
    elif isinstance(selector, dict):
        spec.update(selector)

    res = _build_capture(spec)
    if res["ok"]:
        return {"ok": True, "image_ref": res["artifact_ref"], "error": None}
    return {"ok": False, "image_ref": None, "error": res["error"]}


# ── transport: MCP stdio server + CLI one-shot ────────────────────────────────
#
# The verb FUNCTIONS above are the single implementation. Two transports wrap them:
#   * CLI one-shots — `python3 server.py <verb> ...` (tests + manual sanity checks).
#   * MCP stdio     — `python3 server.py mcp` (the live transport a greenoke launch
#                     drives once `mcp` is installed). Thin typed wrappers delegate
#                     straight to the verbs; nothing heavy is imported here.

_VERBS = {
    "health": lambda args: health(),
    "inspect": lambda args: inspect(args[0] if args else None),
    "verify": lambda args: verify(args[0] if args else None),
    "screenshot": lambda args: screenshot(args[0] if args else None),
    "build": lambda args: build(args[0] if args else None),
}

# Canonical, greppable schema map (also the documentation surface).
TOOL_SCHEMAS = {
    "health": {
        "args": {},
        "returns": "{reachable: bool, code_version: str, detail: str}",
        "summary": "Is the live Unity Editor (its MCP bridge) reachable, and what "
                   "code version is it running? Cheap, never throws — a closed Editor "
                   "yields reachable:false + a hint. code_version powers stale_system_guard.",
    },
    "inspect": {
        "args": {"query": "str — 'editor' | 'activeScene' | 'hierarchy' | 'console' | "
                          "'<gameobject-name>' | a JSON selector {\"type\":\"...\",\"params\":{...}}"},
        "returns": "{ok: bool, state: object|null, error: str|null}",
        "summary": "Read live Unity state without changing it (idempotent): editor "
                   "play/compile state, active scene, hierarchy, console, or a "
                   "gameobject query — routed to the matching read-only bridge command.",
    },
    "verify": {
        "args": {"artifact": "str? — optional path to a NUnit results XML / PNG to also "
                            "well-formedness-check (build() gate); live checks always run"},
        "returns": "{ok: bool, checks: [{name, ok, detail}], code_version: str, error: str|null}",
        "summary": "Proof-of-life: run EditMode tests through the bridge AND read the "
                   "console for compile errors, each a named check. Returns the "
                   "code_version stamp for stale_system_guard.",
    },
    "screenshot": {
        "args": {"selector": "str? — 'game_view' (default) | 'scene_view', or a JSON "
                            "{\"source\":\"...\",\"camera\":\"...\",\"out\":\"<path>\"}"},
        "returns": "{ok: bool, image_ref: str|null, error: str|null}",
        "summary": "Capture the Game/Scene view via the bridge as a readable image "
                   "file (proof-of-life). image_ref is an absolute filesystem path.",
    },
    "build": {
        "args": {"spec": "str — JSON build spec: {\"kind\":\"refresh\",...} | "
                        "{\"kind\":\"capture\",\"out\":\"<path>\"} | {\"kind\":\"menu\",\"menu_path\":\"...\"}"},
        "returns": "{ok: bool, artifact_ref: str|null, log: str|null, error: str|null, code_version: str}",
        "summary": "Drive the live Editor (refresh/recompile, run a menu tool, or "
                   "capture an artifact). Provider-owned artifact safety: file "
                   "artifacts go temp -> verify -> atomic swap; a broken build never "
                   "replaces a good artifact.",
    },
}


def _mcp_health() -> dict:
    """Is the live Unity Editor (its MCP bridge) reachable, and what code version is
    it running? Returns {reachable, code_version, detail}. Cheap, never throws — a
    closed Editor yields reachable:false + a hint. code_version powers
    stale_system_guard."""
    return health()


def _mcp_inspect(query: str = "editor") -> dict:
    """Read live Unity state without changing it (idempotent, side-effect-free).

    query: 'editor' | 'activeScene' | 'hierarchy' | 'console' | '<gameobject-name>'
           | a JSON selector {"type":"...","params":{...}}.
    Returns {ok, state, error}."""
    return inspect(query)


def _mcp_verify(artifact: str = "") -> dict:
    """Proof-of-life: run Unity EditMode tests through the bridge AND read the
    console for compile errors, each surfaced as a named check. Optional `artifact`
    is a results XML / PNG to also well-formedness-check. Returns
    {ok, checks, code_version, error} — code_version drives stale_system_guard."""
    return verify(artifact or None)


def _mcp_screenshot(selector: str = "game_view") -> dict:
    """Capture the Game view (default) or Scene view via the bridge as a readable
    image file (proof-of-life). selector: 'game_view' | 'scene_view' | a JSON
    {"source":"...","camera":"...","out":"<path>"}. Returns {ok, image_ref, error}."""
    return screenshot(selector)


def _mcp_build(spec: str) -> dict:
    """Drive the live Editor to produce/refresh an artifact (provider-owned temp ->
    verify -> atomic swap for file artifacts). spec is a JSON build spec, e.g.
    {"kind":"refresh","compile":"request"} | {"kind":"capture","out":"<path>"} |
    {"kind":"menu","menu_path":"<Unity menu>"}. Returns
    {ok, artifact_ref, log, error, code_version}."""
    return build(spec)


_MCP_TOOLS = (_mcp_health, _mcp_inspect, _mcp_verify, _mcp_screenshot, _mcp_build)
_MCP_TOOL_NAMES = ("health", "inspect", "verify", "screenshot", "build")


def register_tools(server):
    """Register the 5 verbs as MCP tools on a FastMCP `server`. Returns the list of
    registered tool names. Isolated from `_serve_mcp` so it is unit-testable with a
    mocked FastMCP (the sandbox has no `mcp` SDK)."""
    for fn, name in zip(_MCP_TOOLS, _MCP_TOOL_NAMES):
        server.tool(name=name)(fn)
    return list(_MCP_TOOL_NAMES)


def _serve_mcp():
    """Serve the 5 verbs over MCP stdio. If `mcp` is not installed, fall back to
    printing health() so a bare invocation still proves addressability."""
    try:
        from mcp.server.fastmcp import FastMCP  # type: ignore
    except ImportError:
        print(json.dumps(health(), indent=2))
        print("\n[greenoke provider] `mcp` not installed — CLI fallback (printed "
              "health). Use `python3 server.py <verb>` for one-shots, or install the "
              "`mcp` SDK (`pip install mcp`) to serve over stdio.", file=sys.stderr)
        return
    server = FastMCP("meltitdown-greenoke-provider")
    register_tools(server)
    server.run()


_USAGE = (
    "greenoke capability provider — MeltItDown (Unity)\n\n"
    "  python3 server.py mcp                  serve the 5 verbs over MCP stdio\n"
    "  python3 server.py health               one-shot: health() JSON\n"
    "  python3 server.py inspect <query>      one-shot: inspect()\n"
    "  python3 server.py verify [path]        one-shot: verify() (live EditMode tests)\n"
    "  python3 server.py screenshot [out]     one-shot: screenshot() (Game view -> file)\n"
    "  python3 server.py build <spec-json>    one-shot: build()\n"
    "  python3 server.py                      (bare) serve MCP if installed, else print health\n"
)


def main(argv):
    if argv and argv[0] == "mcp":
        _serve_mcp()
        return 0
    if argv and argv[0] in ("-h", "--help", "help"):
        print(_USAGE)
        return 0
    if argv and argv[0] in _VERBS:
        print(json.dumps(_VERBS[argv[0]](argv[1:]), indent=2))
        return 0
    if argv and argv[0] not in _VERBS:
        print(json.dumps({"error": f"unknown verb '{argv[0]}'",
                          "verbs": list(_VERBS), "usage": _USAGE.strip()},
                         indent=2), file=sys.stderr)
        return 2
    _serve_mcp()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
