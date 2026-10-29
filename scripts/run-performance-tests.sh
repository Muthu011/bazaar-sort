#!/usr/bin/env bash
# Runs the Edit Mode tests in category "Performance" in Unity batch mode, where macOS cannot throttle an unfocused
# editor. Unity locks an open project, so while the editor is open this runs on a copy (Library included, so it
# doesn't reimport). Usage, from the repo root:
#   scripts/run-performance-tests.sh [copy-dir]      # default copy dir: $TMPDIR/bazaar-sort-perf
# Results: <copy-dir>/perf-results.xml and <copy-dir>/perf-log.txt. Exit code 0 only if every test passed.
set -euo pipefail

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity}"
REPO="$(cd "$(dirname "$0")/.." && pwd)"
COPY="${1:-${TMPDIR:-/tmp}/bazaar-sort-perf}"
MARKER=".bazaar-sort-perf-copy"

# rsync --delete empties the copy dir first, so refuse anything that isn't a dedicated copy: "/", the repo, a folder
# holding the repo, a folder inside the repo, or a non-empty folder this script didn't create (no marker file).
mkdir -p "$COPY"
COPY="$(cd "$COPY" && pwd -P)"
REPO="$(cd "$REPO" && pwd -P)"
case "$REPO/" in "$COPY"/*) echo "refusing: $COPY is the repo or contains it" >&2; exit 2 ;; esac
case "$COPY/" in "$REPO"/*) echo "refusing: $COPY is inside the repo" >&2; exit 2 ;; esac
if [ -n "$(ls -A "$COPY")" ] && [ ! -f "$COPY/$MARKER" ]; then
  echo "refusing: $COPY is not empty and has no $MARKER file" >&2; exit 2
fi
touch "$COPY/$MARKER"

rsync -a --delete --exclude Temp --exclude Logs --exclude UserSettings --exclude .git \
  --exclude perf-results.xml --exclude perf-log.txt --exclude "$MARKER" "$REPO/" "$COPY/"

"$UNITY" -batchmode -nographics -projectPath "$COPY" -runTests -testPlatform EditMode \
  -testCategory Performance -testResults "$COPY/perf-results.xml" -logFile "$COPY/perf-log.txt" || status=$?

grep -o '<test-case [^>]*>' "$COPY/perf-results.xml" \
  | sed -E 's/.* fullname="([^"]*)".* result="([^"]*)".* duration="([^"]*)".*/\1  \2  \3 s/' || true
grep -h "14-shelf" "$COPY/perf-log.txt" | grep -v "^ " | sort -u || true
exit "${status:-0}"
