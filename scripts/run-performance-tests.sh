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

mkdir -p "$COPY"
rsync -a --delete --exclude Temp --exclude Logs --exclude UserSettings --exclude .git \
  --exclude perf-results.xml --exclude perf-log.txt "$REPO/" "$COPY/"

"$UNITY" -batchmode -nographics -projectPath "$COPY" -runTests -testPlatform EditMode \
  -testCategory Performance -testResults "$COPY/perf-results.xml" -logFile "$COPY/perf-log.txt" || status=$?

grep -o '<test-case [^>]*>' "$COPY/perf-results.xml" \
  | sed -E 's/.* fullname="([^"]*)".* result="([^"]*)".* duration="([^"]*)".*/\1  \2  \3 s/' || true
grep -h "14-shelf" "$COPY/perf-log.txt" | grep -v "^ " | sort -u || true
exit "${status:-0}"
