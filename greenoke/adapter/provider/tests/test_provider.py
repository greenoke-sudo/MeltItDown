#!/usr/bin/env python3
"""Unit tests for the MeltItDown greenoke capability provider.

These run GREEN in the Linux sandbox WITHOUT a live Unity Editor: a FAKE in-memory
bridge server (a real loopback socket in a thread that performs the WELCOME
handshake + framed request/response, exactly like StdioBridgeHost) stands in for
Unity. Run from the repo root:

    python3 -m pytest greenoke/adapter/provider/tests/ -q

Importing the provider must NOT require the `mcp` SDK or a live bridge — these
tests prove that too.
"""

import json
import os
import socket
import struct
import sys
import threading

import pytest

_HERE = os.path.dirname(os.path.abspath(__file__))
_PROVIDER_DIR = os.path.normpath(os.path.join(_HERE, ".."))
if _PROVIDER_DIR not in sys.path:
    sys.path.insert(0, _PROVIDER_DIR)

import server  # noqa: E402  — the module under test (no mcp / no live bridge needed)
import validate_artifact  # noqa: E402


# ── Fake in-memory bridge server (mirrors StdioBridgeHost framing) ────────────

class FakeBridge:
    """A loopback TCP server that speaks the MCP-for-Unity wire protocol:
    sends the WELCOME line on connect, then length-prefixed (8-byte big-endian)
    UTF-8 JSON frames. `responder(type, params) -> dict` returns the bridge result
    object for each command; a bare 'ping' frame is answered with pong. Used to drive
    the provider's UnityBridge against canned responses."""

    def __init__(self, responder=None, welcome="WELCOME UNITY-MCP 1 FRAMING=1\n"):
        self.responder = responder or (lambda t, p: {"status": "success", "result": {"echo": t}})
        self.welcome = welcome
        self.requests = []  # (type, params) seen
        self._srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._srv.bind(("127.0.0.1", 0))
        self._srv.listen(5)
        self.port = self._srv.getsockname()[1]
        self._stop = False
        self._thread = threading.Thread(target=self._serve, daemon=True)
        self._thread.start()

    def _serve(self):
        while not self._stop:
            try:
                conn, _ = self._srv.accept()
            except OSError:
                return
            threading.Thread(target=self._handle, args=(conn,), daemon=True).start()

    def _handle(self, conn):
        try:
            conn.sendall(self.welcome.encode("ascii"))
            while not self._stop:
                header = self._recv_exact(conn, 8)
                if header is None:
                    return
                (length,) = struct.unpack(">Q", header)
                payload = self._recv_exact(conn, length)
                if payload is None:
                    return
                text = payload.decode("utf-8")
                if text.strip() == "ping":
                    self._send(conn, {"status": "success", "result": {"message": "pong"}})
                    continue
                try:
                    cmd = json.loads(text)
                except ValueError:
                    self._send(conn, {"status": "error", "error": "Invalid JSON format"})
                    continue
                self.requests.append((cmd.get("type"), cmd.get("params")))
                self._send(conn, self.responder(cmd.get("type"), cmd.get("params") or {}))
        except OSError:
            return
        finally:
            try:
                conn.close()
            except OSError:
                pass

    @staticmethod
    def _recv_exact(conn, count):
        buf = bytearray()
        while len(buf) < count:
            try:
                chunk = conn.recv(count - len(buf))
            except OSError:
                return None
            if not chunk:
                return None
            buf.extend(chunk)
        return bytes(buf)

    def _send(self, conn, obj):
        data = json.dumps(obj).encode("utf-8")
        conn.sendall(struct.pack(">Q", len(data)) + data)

    def close(self):
        self._stop = True
        try:
            self._srv.close()
        except OSError:
            pass


@pytest.fixture
def bridge_factory(monkeypatch):
    """Yields a function start(responder) -> FakeBridge that ALSO patches the
    provider's _bridge() factory to point UnityBridge at the fake's port."""
    servers = []

    def start(responder=None):
        fake = FakeBridge(responder=responder)
        servers.append(fake)

        def _patched(**kwargs):
            kwargs.setdefault("port", fake.port)
            return server.UnityBridge(**kwargs)

        monkeypatch.setattr(server, "_bridge", _patched)
        return fake

    yield start
    for s in servers:
        s.close()


# ── port discovery + fallback ─────────────────────────────────────────────────

def test_project_hash_is_sha1_first8():
    import hashlib
    data_path = "/some/project/Assets"
    expected = hashlib.sha1(data_path.encode("utf-8")).hexdigest()[:8]
    assert server._project_hash(data_path) == expected


def test_discover_port_scoped_file(tmp_path):
    data_path = "/proj/Assets"
    h = server._project_hash(data_path)
    (tmp_path / f"unity-mcp-port-{h}.json").write_text(json.dumps({"unity_port": 6543}))
    assert server.discover_port(data_path, str(tmp_path)) == 6543


def test_discover_port_legacy_fallback(tmp_path):
    data_path = "/proj/Assets"
    # No scoped file; legacy present.
    (tmp_path / "unity-mcp-port.json").write_text(json.dumps({"unity_port": 7001}))
    assert server.discover_port(data_path, str(tmp_path)) == 7001


def test_discover_port_default_when_nothing(tmp_path):
    assert server.discover_port("/proj/Assets", str(tmp_path)) == 6400


def test_discover_port_ignores_garbage(tmp_path):
    data_path = "/proj/Assets"
    h = server._project_hash(data_path)
    (tmp_path / f"unity-mcp-port-{h}.json").write_text("not json{{{")
    assert server.discover_port(data_path, str(tmp_path)) == 6400


# ── WELCOME-then-framed round trip + big-endian framing ───────────────────────

def test_welcome_then_framed_roundtrip(bridge_factory):
    fake = bridge_factory(lambda t, p: {"status": "success", "result": {"got": t, "p": p}})
    br = server.UnityBridge(port=fake.port)
    resp = br.command("manage_editor", {"action": "get_state"})
    assert resp["status"] == "success"
    assert resp["result"]["got"] == "manage_editor"
    assert resp["result"]["p"] == {"action": "get_state"}
    assert fake.requests[-1] == ("manage_editor", {"action": "get_state"})
    br.close()


def test_ping_pong(bridge_factory):
    fake = bridge_factory()
    br = server.UnityBridge(port=fake.port)
    assert br.ping() is True
    assert br.reachable() is True
    br.close()


def test_big_endian_length_framing_small_and_large(bridge_factory):
    # Echo back a payload whose size we control to exercise the >Q length prefix on
    # both a tiny (>0) and a large-ish (>64KiB, multi-recv) frame.
    big = "x" * 200_000

    def responder(t, p):
        return {"status": "success", "result": {"size": p.get("n"), "blob": big if p.get("big") else "ok"}}

    fake = bridge_factory(responder)
    br = server.UnityBridge(port=fake.port)

    small = br.command("echo", {"n": 1, "big": False})
    assert small["result"]["blob"] == "ok"

    large = br.command("echo", {"n": len(big), "big": True})
    assert large["result"]["blob"] == big
    assert len(large["result"]["blob"]) == 200_000
    br.close()


def test_write_frame_header_is_8byte_big_endian():
    # Direct unit check of the framing helper independent of the socket.
    sent = bytearray()

    class _FakeSock:
        def sendall(self, b):
            sent.extend(b)

    br = server.UnityBridge(port=1)
    br._write_frame(_FakeSock(), b"hello")
    assert sent[:8] == struct.pack(">Q", 5)
    assert bytes(sent[8:]) == b"hello"


def test_zero_length_frame_rejected_on_read():
    class _ZeroSock:
        def __init__(self):
            self._buf = struct.pack(">Q", 0)
            self._i = 0

        def recv(self, n):
            chunk = self._buf[self._i:self._i + n]
            self._i += len(chunk)
            return chunk

    br = server.UnityBridge(port=1)
    with pytest.raises(OSError):
        br._read_frame(_ZeroSock())


# ── health() ──────────────────────────────────────────────────────────────────

def test_health_reachable_with_fake_bridge(bridge_factory):
    bridge_factory()
    h = server.health()
    assert h["reachable"] is True
    assert "code_version" in h and isinstance(h["code_version"], str)


def test_health_unreachable_is_graceful(monkeypatch):
    # Point the bridge at a closed port — nothing listening. Must NOT throw.
    def _patched(**kwargs):
        kwargs.setdefault("port", _free_port())
        return server.UnityBridge(**kwargs)

    monkeypatch.setattr(server, "_bridge", _patched)
    h = server.health()
    assert h["reachable"] is False
    assert h["code_version"]  # still stamped
    assert "not reachable" in h["detail"].lower() or "error" in h["detail"].lower()


def test_code_version_format_and_dirty(monkeypatch):
    # Force a known git output to assert the <sha> and <sha>-dirty formats.
    calls = {"n": 0}

    class _Res:
        def __init__(self, out):
            self.stdout = out

    def fake_run(args, **kwargs):
        if args[:2] == ["git", "rev-parse"]:
            return _Res("abc1234\n")
        if args[:2] == ["git", "status"]:
            # First call: clean tree; second call: dirty.
            calls["n"] += 1
            return _Res("" if calls["n"] == 1 else " M file.cs\n")
        return _Res("")

    monkeypatch.setattr(server.subprocess, "run", fake_run)
    assert server.code_version() == "abc1234"          # clean
    assert server.code_version() == "abc1234-dirty"    # dirty


def _free_port():
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    p = s.getsockname()[1]
    s.close()
    return p


# ── inspect(): command construction + response parsing ────────────────────────

def test_inspect_editor_maps_to_manage_editor(bridge_factory):
    fake = bridge_factory(lambda t, p: {"status": "success", "result": {"isPlaying": False}})
    out = server.inspect("editor")
    assert out["ok"] is True
    assert out["state"] == {"isPlaying": False}
    assert fake.requests[-1] == ("manage_editor", {"action": "get_state"})


def test_inspect_active_scene_maps_to_manage_scene(bridge_factory):
    fake = bridge_factory(lambda t, p: {"status": "success", "result": {"name": "Main"}})
    out = server.inspect("activeScene")
    assert out["ok"] is True
    assert fake.requests[-1] == ("manage_scene", {"action": "get_active"})


def test_inspect_console_maps_to_read_console(bridge_factory):
    fake = bridge_factory(lambda t, p: {"status": "success", "result": {"lines": []}})
    server.inspect("console")
    t, params = fake.requests[-1]
    assert t == "read_console"
    assert params["action"] == "get"


def test_inspect_gameobject_name_uses_find(bridge_factory):
    fake = bridge_factory(lambda t, p: {"status": "success", "result": {"found": 1}})
    server.inspect("Player")
    t, params = fake.requests[-1]
    assert t == "find_gameobjects"
    assert params.get("searchTerm") == "Player"


def test_inspect_passthrough_json_selector(bridge_factory):
    fake = bridge_factory(lambda t, p: {"status": "success", "result": {"ok": 1}})
    server.inspect('{"type": "manage_scene", "params": {"action": "get_hierarchy"}}')
    assert fake.requests[-1] == ("manage_scene", {"action": "get_hierarchy"})


def test_inspect_error_response_surfaces(bridge_factory):
    bridge_factory(lambda t, p: {"status": "error", "error": "boom"})
    out = server.inspect("editor")
    assert out["ok"] is False
    assert out["error"] == "boom"


def test_inspect_unreachable_is_graceful(monkeypatch):
    monkeypatch.setattr(server, "_bridge",
                        lambda **k: server.UnityBridge(port=_free_port()))
    out = server.inspect("editor")
    assert out["ok"] is False
    assert "unreachable" in out["error"].lower()


# ── verify(): EditMode tests + console ────────────────────────────────────────

def test_verify_green_when_tests_pass_and_console_clean(bridge_factory):
    def responder(t, p):
        if t == "read_console":
            return {"status": "success", "result": {"lines": []}}
        if t == "run_tests":
            return {"status": "success", "result": {"job_id": "job-1", "status": "running"}}
        if t == "get_test_job":
            return {"status": "success",
                    "result": {"status": "completed", "passed": 12, "failed": 0, "total": 12}}
        return {"status": "success", "result": {}}

    bridge_factory(responder)
    out = server.verify(poll_interval=0.01, timeout=5)
    assert out["ok"] is True
    names = {c["name"] for c in out["checks"]}
    assert "no_compile_errors" in names
    assert any(n.endswith("_tests_pass") for n in names)
    assert out["code_version"]


def test_verify_red_on_test_failure(bridge_factory):
    def responder(t, p):
        if t == "read_console":
            return {"status": "success", "result": {"lines": []}}
        if t == "run_tests":
            return {"status": "success", "result": {"job_id": "job-2"}}
        if t == "get_test_job":
            return {"status": "success",
                    "result": {"status": "completed", "passed": 10, "failed": 2, "total": 12}}
        return {"status": "success", "result": {}}

    bridge_factory(responder)
    out = server.verify(poll_interval=0.01, timeout=5)
    assert out["ok"] is False
    failing = [c for c in out["checks"] if not c["ok"]]
    assert any("2 failed" in c["detail"] for c in failing)


def test_verify_red_on_console_errors(bridge_factory):
    def responder(t, p):
        if t == "read_console":
            return {"status": "success", "result": {"lines": ["Assets/X.cs(1,1): error CS0103"]}}
        if t == "run_tests":
            return {"status": "success", "result": {"job_id": "job-3"}}
        if t == "get_test_job":
            return {"status": "success", "result": {"status": "completed", "failed": 0, "total": 1}}
        return {"status": "success", "result": {}}

    bridge_factory(responder)
    out = server.verify(poll_interval=0.01, timeout=5)
    assert out["ok"] is False
    console_check = next(c for c in out["checks"] if c["name"] == "no_compile_errors")
    assert console_check["ok"] is False


# ── build(): refresh + capture temp->verify->atomic-swap ──────────────────────

def test_build_refresh_drives_bridge(bridge_factory):
    fake = bridge_factory(lambda t, p: {"status": "success", "result": {"refreshed": True}})
    out = server.build({"kind": "refresh", "compile": "request"})
    assert out["ok"] is True
    assert fake.requests[-1][0] == "refresh_unity"
    assert out["code_version"]


def test_build_capture_atomic_swap_on_clean_verify(bridge_factory, tmp_path):
    # The bridge "captures" a valid PNG into a temp file; build() must verify it and
    # atomically swap it into `out`.
    good_png = tmp_path / "captured.png"
    good_png.write_bytes(server_png_bytes())

    def responder(t, p):
        if t == "manage_scene" and p.get("action") == "screenshot":
            return {"status": "success",
                    "result": {"path": "Assets/x.png", "fullPath": str(good_png)}}
        return {"status": "success", "result": {}}

    bridge_factory(responder)
    out_path = tmp_path / "live" / "screenshot.png"
    out = server.build({"kind": "capture", "out": str(out_path)})
    assert out["ok"] is True
    assert out["artifact_ref"] == str(out_path.resolve()) or out["artifact_ref"] == os.path.abspath(str(out_path))
    assert os.path.isfile(out_path)
    assert out_path.read_bytes()[:8] == server_png_bytes()[:8]


def test_build_capture_broken_verify_leaves_prior_intact(bridge_factory, tmp_path):
    # A prior GOOD artifact exists; the new capture is a BROKEN (non-PNG) file.
    # build() must NOT replace the prior artifact.
    out_path = tmp_path / "live" / "screenshot.png"
    out_path.parent.mkdir(parents=True)
    prior_bytes = server_png_bytes() + b"PRIOR-GOOD"
    out_path.write_bytes(prior_bytes)

    broken = tmp_path / "broken.png"
    broken.write_bytes(b"this is not a png")

    def responder(t, p):
        if t == "manage_scene" and p.get("action") == "screenshot":
            return {"status": "success",
                    "result": {"path": "Assets/x.png", "fullPath": str(broken)}}
        return {"status": "success", "result": {}}

    bridge_factory(responder)
    out = server.build({"kind": "capture", "out": str(out_path)})
    assert out["ok"] is False
    # The prior good artifact is byte-for-byte intact.
    assert out_path.read_bytes() == prior_bytes


def test_screenshot_returns_image_ref(bridge_factory, tmp_path):
    good_png = tmp_path / "captured.png"
    good_png.write_bytes(server_png_bytes())
    out_path = tmp_path / "shot.png"

    def responder(t, p):
        if t == "manage_scene":
            return {"status": "success", "result": {"fullPath": str(good_png)}}
        return {"status": "success", "result": {}}

    bridge_factory(responder)
    out = server.screenshot({"out": str(out_path)})
    assert out["ok"] is True
    assert os.path.isfile(out["image_ref"])


# ── validate_artifact ─────────────────────────────────────────────────────────

def server_png_bytes():
    # Minimal valid-enough PNG: correct magic + an IHDR-ish tail (the validator
    # checks magic + non-empty, not full chunk integrity).
    return validate_artifact._PNG_MAGIC + b"\x00\x00\x00\rIHDR" + b"\x00" * 20


def test_validate_png_ok(tmp_path):
    p = tmp_path / "a.png"
    p.write_bytes(server_png_bytes())
    res = validate_artifact.validate_artifact(str(p))
    assert res["ok"] is True and res["kind"] == "png"


def test_validate_png_bad_magic(tmp_path):
    p = tmp_path / "a.png"
    p.write_bytes(b"nope")
    res = validate_artifact.validate_png(str(p))
    assert res["ok"] is False


def test_validate_nunit_pass(tmp_path):
    p = tmp_path / "results.xml"
    p.write_text('<test-run total="5" passed="5" failed="0" result="Passed"/>')
    res = validate_artifact.validate_artifact(str(p))
    assert res["ok"] is True and res["kind"] == "nunit"


def test_validate_nunit_fail(tmp_path):
    p = tmp_path / "results.xml"
    p.write_text('<test-run total="5" passed="4" failed="1" result="Failed"/>')
    res = validate_artifact.validate_nunit_xml(str(p))
    assert res["ok"] is False
    assert res["error"] == "test_failures"


def test_validate_nunit_counts_testcases_fallback(tmp_path):
    p = tmp_path / "results.xml"
    p.write_text(
        "<test-run>"
        '<test-case result="Passed"/>'
        '<test-case result="Failed"/>'
        "</test-run>"
    )
    res = validate_artifact.validate_nunit_xml(str(p))
    assert res["ok"] is False  # one failure


def test_validate_missing_file(tmp_path):
    res = validate_artifact.validate_artifact(str(tmp_path / "nope.png"))
    assert res["ok"] is False


# ── MCP registration is unit-testable without the SDK ─────────────────────────

def test_register_tools_with_mock_server():
    registered = []

    class _FakeServer:
        def tool(self, name=None):
            def deco(fn):
                registered.append(name)
                return fn
            return deco

    names = server.register_tools(_FakeServer())
    assert names == ["health", "inspect", "verify", "screenshot", "build"]
    assert registered == ["health", "inspect", "verify", "screenshot", "build"]


def test_importing_server_needs_no_mcp_sdk():
    # If we got here, `import server` at module top already succeeded without `mcp`.
    assert hasattr(server, "health")
    assert hasattr(server, "UnityBridge")
