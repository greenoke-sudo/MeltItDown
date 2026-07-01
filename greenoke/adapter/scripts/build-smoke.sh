#!/usr/bin/env bash
# build-smoke.sh — MeltItDown structural smoke check (EditMode tests via Unity batch mode).
#
# Runs the Unity EditMode test suite headless and fails nonzero on any test failure
# or Unity error. This is the `verification.build_smoke` command in
# greenoke/adapter/greenoke.adapter.json.
#
# IMPORTANT — the Unity Editor MUST be CLOSED for this to work.
#   Unity batch mode takes an exclusive project lock (Library/). If the Editor is
#   open on this project, `-batchmode` fails to acquire the lock and exits nonzero.
#   While the Editor IS open, do NOT use this script — the capability provider's
#   verify() runs the same tests through the live Unity MCP bridge instead (no
#   second Unity process, no lock contention). Use this script only for a clean,
#   Editor-closed CI / headless smoke run.
#
# Unity: 6 (6000.0.62f1) on macOS. Override the binary with $UNITY_BIN.
set -euo pipefail

# Resolve the repo root robustly: prefer git, else walk up from this script's
# location (scripts/ -> adapter -> greenoke -> repo root = three levels up).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null)"; then
  :
else
  REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
fi

UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.0.62f1/Unity.app/Contents/MacOS/Unity}"

if [ ! -x "$UNITY" ]; then
  echo "ERROR: Unity binary not found/executable at: $UNITY" >&2
  echo "       Set \$UNITY_BIN to your 6000.0.62f1 Unity executable." >&2
  exit 1
fi

RUN_DIR="$REPO_ROOT/.greenoke-run"
mkdir -p "$RUN_DIR"
RESULTS="$RUN_DIR/test-results.xml"
LOG="$RUN_DIR/unity-tests.log"

echo "build-smoke: running EditMode tests"
echo "  unity      : $UNITY"
echo "  projectPath: $REPO_ROOT"
echo "  results    : $RESULTS"
echo "  log        : $LOG"
echo "  NOTE: the Unity Editor must be CLOSED (project lock); while it is open, use the provider's verify() via the MCP bridge."

set +e
"$UNITY" \
  -batchmode \
  -projectPath "$REPO_ROOT" \
  -runTests \
  -testPlatform EditMode \
  -testResults "$RESULTS" \
  -logFile "$LOG" \
  -quit
STATUS=$?
set -e

if [ "$STATUS" -ne 0 ]; then
  echo "build-smoke: FAILED (Unity exit $STATUS). See $LOG" >&2
  exit "$STATUS"
fi

echo "build-smoke: PASSED. Results: $RESULTS"
