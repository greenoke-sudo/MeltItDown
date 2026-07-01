#!/usr/bin/env bash
# greenoke diff-dump — generalized diff export for code review.
#
# Dumps the working-tree changes (vs a base ref) to a single file an agent or reviewer
# can read in one pass. Project-agnostic: no language, path, or tool assumptions — it just
# drives git in whatever repo it's run from.
#
# Usage:
#   ./greenoke/core/tooling/diff-dump.sh [output-file] [base-ref]
#
#   output-file   where to write the dump (default: greenoke-diff.txt in the cwd)
#   base-ref      what to diff against (default: the merge-base with the upstream/default
#                 branch if detectable, else HEAD — i.e. staged+unstaged working changes)
#
# Examples:
#   ./greenoke/core/tooling/diff-dump.sh                       # working changes -> greenoke-diff.txt
#   ./greenoke/core/tooling/diff-dump.sh review.txt            # working changes -> review.txt
#   ./greenoke/core/tooling/diff-dump.sh review.txt main       # changes vs main -> review.txt

set -euo pipefail

OUT="${1:-greenoke-diff.txt}"
BASE="${2:-}"

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "error: not inside a git work tree." >&2
  exit 1
fi

{
  echo "# greenoke diff dump"
  echo "# generated: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "# repo: $(git rev-parse --show-toplevel)"
  echo "# HEAD: $(git rev-parse --short HEAD 2>/dev/null || echo '(no commits)')"
  echo

  if [ -n "$BASE" ]; then
    echo "## Diff vs $BASE (committed + working)"
    echo
    echo "### Files changed"
    git diff --stat "$BASE" || true
    echo
    echo "### Patch"
    git diff "$BASE"
  else
    echo "## Working tree changes (staged + unstaged vs HEAD)"
    echo
    echo "### Files changed"
    git status --short
    echo
    echo "### Patch (tracked changes)"
    git diff HEAD
    echo
    echo "### Untracked files"
    git ls-files --others --exclude-standard
  fi
} > "$OUT"

echo "wrote diff dump -> $OUT"
