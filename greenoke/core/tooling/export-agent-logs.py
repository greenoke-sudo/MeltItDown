#!/usr/bin/env python3
"""greenoke export-agent-logs — generalized session-log exporter.

Exports a Claude Code session transcript (JSONL) to a readable Markdown log, optionally
emitting per-subagent files. Project-agnostic: no project paths, languages, or tools are
assumed. The only inputs are a transcript JSONL file and an output path.

STATUS: partial stub. The Markdown rendering of a transcript JSONL is fully implemented and
generalized. What is intentionally left as a documented integration seam is *session
discovery* — i.e. "which JSONL file is the current session?" — because that is harness/host
specific (where the host writes session transcripts, and the two-step stamp/flush dance some
hosts need so the JSONL is flushed to disk before export). Point --transcript at the file
yourself, or wire discovery in resolve_transcript_path() for your host.

## Interface (the contract a host wires into)

Two-step export, when the host needs the session JSONL flushed before reading (run as TWO
separate invocations so the host flushes between them):

  1. Stamp:   export-agent-logs.py --stamp
                -> prints a nonce string to stdout. The host is expected to ensure the
                   current session's JSONL is flushed to disk by the time the nonce is
                   visible (e.g. the nonce appears in the transcript, proving the flush).

  2. Export:  export-agent-logs.py --export <out.md> --transcript <session.jsonl> \
                                   [--nonce <nonce>] [--include-subagents]
                -> renders the transcript to <out.md>. With --nonce, trims everything up to
                   and including the line carrying the nonce (so the stamp call itself isn't
                   in the log). With --include-subagents, also writes per-subagent files under
                   <out-stem>-subagents/.

If your host has no flush concern, skip --stamp and just run the export step.

## Transcript JSONL shape (the generalized assumption)

Each line is a JSON object. The renderer reads, per line, a best-effort:
  - role/type  : "user" | "assistant" | "system" | "tool" / "tool_result"
  - content    : string, or a list of content blocks ({type, text, ...})
  - subagent   : an optional id/name marking lines produced inside a spawned subagent
Lines that don't parse are passed through verbatim as fenced raw blocks. This is deliberately
lenient so it degrades gracefully across host transcript formats — tighten it for your host.

## Examples

    export-agent-logs.py --stamp
        print a nonce; host flushes the session JSONL before the export step
    export-agent-logs.py --export run.md --transcript session.jsonl
        render a transcript to Markdown
    export-agent-logs.py --export run.md --transcript session.jsonl --include-subagents
        also emit per-subagent files under run-subagents/
    export-agent-logs.py --export run.md --transcript session.jsonl --nonce abc123
        trim everything up to the stamp line, then render

## Environment

    GREENOKE_TRANSCRIPT
        default transcript path if --transcript is omitted

## Exit codes

    0    success
    1    runtime error (no transcript found, unreadable output path)
    2    usage error
"""

from __future__ import annotations

import argparse
import json
import os
import secrets
import sys
from pathlib import Path


def resolve_transcript_path(explicit: str | None) -> Path | None:
    """Resolve which JSONL transcript to render.

    Integration seam: host-specific session discovery goes here. The generalized
    implementation only honors an explicit --transcript or the GREENOKE_TRANSCRIPT env var.
    A host that knows where it writes session transcripts can implement "find the current
    session JSONL" in this function and return it.
    """
    if explicit:
        p = Path(explicit).expanduser()
        return p if p.exists() else None
    env = os.environ.get("GREENOKE_TRANSCRIPT")
    if env:
        p = Path(env).expanduser()
        return p if p.exists() else None
    # No host discovery wired in the generalized core. Caller must pass --transcript.
    return None


def _block_to_text(block) -> str:
    if isinstance(block, str):
        return block
    if isinstance(block, dict):
        if "text" in block and isinstance(block["text"], str):
            return block["text"]
        btype = block.get("type", "block")
        # Render tool calls / results compactly rather than dumping raw JSON inline.
        return f"```json\n{json.dumps(block, indent=2, ensure_ascii=False)}\n```\n_({btype})_"
    return str(block)


def _content_to_md(content) -> str:
    if content is None:
        return ""
    if isinstance(content, list):
        return "\n\n".join(_block_to_text(b) for b in content)
    return _block_to_text(content)


def _role_of(obj: dict) -> str:
    return str(obj.get("role") or obj.get("type") or "entry")


def _subagent_of(obj: dict) -> str | None:
    for key in ("subagent", "subagent_type", "agent", "agent_id"):
        v = obj.get(key)
        if v:
            return str(v)
    return None


def render(lines: list[str], nonce: str | None) -> tuple[str, dict[str, list[str]]]:
    """Return (main_markdown, {subagent_name: [md_chunks]})."""
    started = nonce is None
    main: list[str] = ["# Session log\n"]
    subagents: dict[str, list[str]] = {}

    for raw in lines:
        raw = raw.rstrip("\n")
        if not raw.strip():
            continue
        if not started:
            # Skip everything up to and including the line carrying the nonce.
            if nonce in raw:
                started = True
            continue

        try:
            obj = json.loads(raw)
        except json.JSONDecodeError:
            main.append(f"```\n{raw}\n```")
            continue

        role = _role_of(obj)
        body = _content_to_md(obj.get("content"))
        chunk = f"## {role}\n\n{body}\n" if body else f"## {role}\n"

        sub = _subagent_of(obj)
        if sub:
            subagents.setdefault(sub, []).append(chunk)
        main.append(chunk)

    return "\n".join(main), subagents


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(
        prog="export-agent-logs.py",
        description="Export a Claude Code session transcript (JSONL) to Markdown.",
    )
    ap.add_argument("--stamp", action="store_true",
                    help="print a flush nonce and exit (step 1 of the two-step export)")
    ap.add_argument("--export", metavar="OUT",
                    help="render the transcript to this Markdown file (step 2)")
    ap.add_argument("--transcript", metavar="JSONL",
                    help="path to the session transcript JSONL (or set GREENOKE_TRANSCRIPT)")
    ap.add_argument("--nonce", metavar="N",
                    help="trim everything up to and including the line carrying this nonce")
    ap.add_argument("--include-subagents", action="store_true",
                    help="also emit per-subagent files under <out-stem>-subagents/")
    args = ap.parse_args(argv)

    if args.stamp:
        # The host is expected to ensure the session JSONL is flushed by the time this nonce
        # is observable. Two-step usage: stamp, then (separate call) export with --nonce.
        print(f"greenoke-export-{secrets.token_hex(8)}")
        return 0

    if not args.export:
        ap.error("either --stamp or --export is required")
        return 2

    transcript = resolve_transcript_path(args.transcript)
    if transcript is None:
        print("error: no transcript found. Pass --transcript <session.jsonl> "
              "(or set GREENOKE_TRANSCRIPT, or wire resolve_transcript_path for your host).",
              file=sys.stderr)
        return 1

    lines = transcript.read_text(encoding="utf-8", errors="replace").splitlines()
    main_md, subagents = render(lines, args.nonce)

    out = Path(args.export).expanduser()
    try:
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(main_md, encoding="utf-8")
    except OSError as e:
        print(f"error: cannot write {out}: {e}", file=sys.stderr)
        return 1

    written = [str(out)]
    if args.include_subagents and subagents:
        sub_dir = out.with_name(out.stem + "-subagents")
        sub_dir.mkdir(parents=True, exist_ok=True)
        for name, chunks in subagents.items():
            safe = "".join(c if c.isalnum() or c in "-_." else "_" for c in name)
            sub_path = sub_dir / f"{safe}.md"
            sub_path.write_text(f"# Subagent: {name}\n\n" + "\n".join(chunks),
                                encoding="utf-8")
            written.append(str(sub_path))

    for w in written:
        print(f"wrote {w}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
