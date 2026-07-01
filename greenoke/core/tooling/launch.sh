#!/usr/bin/env bash
# greenoke launcher — flag-only plugin load + opt-in capability-provider MCP (Decision D2).
#
# Starts a Claude Code session with:
#   --plugin-dir  <core>                       → the /greenoke:* skills + agents resolve.
#   --mcp-config  <adapter provider mcp.json>  → the capability provider's verbs are
#                                                live as MCP tools, ONLY in this session.
#
# Both are flags — there is NO persistent enabledPlugins registration and NO repo-wide
# .mcp.json. A normal `claude` session in this repo stays lean and never loads the
# provider; the provider is live only when the founder runs THIS launcher. If the repo
# already has its own `.mcp.json`, this launcher does not touch it — the greenoke
# provider rides in separately via --mcp-config.
#
# Usage:
#   ./greenoke/core/tooling/launch.sh [claude args...]
#
# Project-agnostic: the provider MCP config is resolved from the per-project adapter
# manifest (greenoke/adapter/greenoke.adapter.json → capability_provider), NOT hardcoded.
# Override the resolved config explicitly with:
#   GREENOKE_MCP_CONFIG=/path/to/mcp.json ./.../launch.sh
#   ./.../launch.sh --greenoke-mcp-config /path/to/mcp.json [claude args...]
#
# Inside the session:
#   /reload-plugins        # after editing core files
#   /greenoke:install      # confirm the wiring (provider health() → green/amber)
#   /greenoke:spec | :plan | :build

set -euo pipefail

# ── Resolve this script's real directory (follow symlinks) → core dir ──────────
SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do
  DIR="$(cd -P "$(dirname "$SOURCE")" >/dev/null 2>&1 && pwd)"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE"
done
TOOLING_DIR="$(cd -P "$(dirname "$SOURCE")" >/dev/null 2>&1 && pwd)"
CORE_DIR="$(cd -P "$TOOLING_DIR/.." >/dev/null 2>&1 && pwd)"

# Repo root = the directory the launcher is invoked from (where `claude` will run, and
# where the manifest's relative paths resolve). The launcher is generic; the project
# lives at the cwd, not next to core.
REPO_ROOT="$(pwd)"
MANIFEST="$REPO_ROOT/greenoke/adapter/greenoke.adapter.json"

# ── Optional explicit override of the provider MCP config ──────────────────────
GREENOKE_MCP_CONFIG="${GREENOKE_MCP_CONFIG:-}"
PASSTHRU=()
while [ "$#" -gt 0 ]; do
  case "$1" in
    --greenoke-mcp-config)
      GREENOKE_MCP_CONFIG="${2:-}"; shift 2 ;;
    --greenoke-mcp-config=*)
      GREENOKE_MCP_CONFIG="${1#*=}"; shift ;;
    *)
      PASSTHRU+=("$1"); shift ;;
  esac
done

# ── Validate the core plugin is present ────────────────────────────────────────
if [ ! -f "$CORE_DIR/.claude-plugin/plugin.json" ]; then
  echo "error: greenoke plugin not found at $CORE_DIR/.claude-plugin/plugin.json" >&2
  echo "       Is the greenoke-core submodule checked out? Try: git submodule update --init --recursive" >&2
  exit 1
fi

# ── Validate the claude CLI is available ───────────────────────────────────────
if ! command -v claude >/dev/null 2>&1; then
  echo "error: 'claude' CLI not found on PATH." >&2
  echo "       Install Claude Code, then re-run this launcher." >&2
  exit 1
fi

# ── Resolve the provider MCP config from the manifest (or the override) ─────────
# Precedence: explicit GREENOKE_MCP_CONFIG / --greenoke-mcp-config  >  derived from
# the manifest's capability_provider.launch (sibling mcp.json of the provider entry).
# If neither resolves, we launch plugin-only and warn (the spec/plan phases still work;
# only the live build proof-of-life needs the provider).
MCP_CONFIG=""
if [ -n "$GREENOKE_MCP_CONFIG" ]; then
  MCP_CONFIG="$GREENOKE_MCP_CONFIG"
elif [ -f "$MANIFEST" ]; then
  # Derive <provider-dir>/mcp.json from capability_provider.launch[1] (the entry
  # script). Generic: we read the manifest, never assume the bot's path. Prefer jq;
  # fall back to python3; tolerate either being absent.
  PROVIDER_ENTRY=""
  if command -v jq >/dev/null 2>&1; then
    PROVIDER_ENTRY="$(jq -r '.capability_provider.launch[-1] // empty' "$MANIFEST" 2>/dev/null || true)"
  elif command -v python3 >/dev/null 2>&1; then
    PROVIDER_ENTRY="$(python3 -c 'import json,sys; m=json.load(open(sys.argv[1])); l=(m.get("capability_provider") or {}).get("launch") or []; print(l[-1] if l else "")' "$MANIFEST" 2>/dev/null || true)"
  fi
  if [ -n "$PROVIDER_ENTRY" ]; then
    CANDIDATE="$REPO_ROOT/$(dirname "$PROVIDER_ENTRY")/mcp.json"
    [ -f "$CANDIDATE" ] && MCP_CONFIG="$CANDIDATE"
  fi
fi

# ── Build the claude argv + report what we load ────────────────────────────────
echo "greenoke launcher" >&2
echo "  repo root : $REPO_ROOT" >&2
echo "  plugin    : $CORE_DIR (--plugin-dir)" >&2

CLAUDE_ARGS=(--plugin-dir "$CORE_DIR")

if [ -n "$MCP_CONFIG" ]; then
  if [ ! -f "$MCP_CONFIG" ]; then
    echo "error: greenoke provider MCP config not found at: $MCP_CONFIG" >&2
    echo "       (resolved from capability_provider in $MANIFEST, or your override)." >&2
    echo "       Fix the path, or pass --greenoke-mcp-config <file>, or run plugin-only" >&2
    echo "       by unsetting GREENOKE_MCP_CONFIG." >&2
    exit 1
  fi
  # Best-effort sanity: the provider command in the config should be runnable. We
  # don't hard-fail (the config may target a tool installed elsewhere), but a missing
  # venv python is the #1 first-run snag, so surface it loudly.
  if command -v jq >/dev/null 2>&1; then
    PROV_CMD="$(jq -r '.mcpServers | to_entries[0].value.command // empty' "$MCP_CONFIG" 2>/dev/null || true)"
    if [ -n "$PROV_CMD" ] && [[ "$PROV_CMD" != /* ]]; then
      if [ ! -x "$REPO_ROOT/$PROV_CMD" ]; then
        echo "warning: provider command '$PROV_CMD' (from $MCP_CONFIG) is not executable" >&2
        echo "         at $REPO_ROOT/$PROV_CMD — is the project venv created?" >&2
        echo "         The session will still start; the provider tools will be unavailable" >&2
        echo "         until the venv exists. (spec/plan work without it.)" >&2
      fi
    fi
  fi
  echo "  provider  : $MCP_CONFIG (--mcp-config)" >&2
  CLAUDE_ARGS+=(--mcp-config "$MCP_CONFIG")
else
  echo "  provider  : (none resolved — launching plugin-only; spec/plan work, live build" >&2
  echo "              proof-of-life will be unavailable until a provider mcp.json exists)" >&2
fi

exec claude "${CLAUDE_ARGS[@]}" "${PASSTHRU[@]}"
