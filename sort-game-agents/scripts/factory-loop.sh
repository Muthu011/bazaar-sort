#!/usr/bin/env bash
# Bazaar Sort factory loop (macOS/Linux/Git Bash). Run from the repo root with Unity open.
# Stop: touch STOP
IDLE_MIN=${IDLE_MIN:-120}; BUSY_MIN=${BUSY_MIN:-10}; START_HOUR=${START_HOUR:-7}; END_HOUR=${END_HOUR:-23}; MAX_TURNS=${MAX_TURNS:-80}
mkdir -p logs
run() { local log="logs/$(date +%F).log"; echo -e "\n===== $(date -Iseconds) $1 =====" >> "$log"; claude -p "$1" --max-turns "$MAX_TURNS" 2>&1 | tee -a "$log"; }
while true; do
  [ -f STOP ] && { echo "STOP file found. Exiting."; exit 0; }
  h=$((10#$(date +%H))); dow=$(date +%u); today=$(date +%F); wk=$(date +%G-W%V)
  if [ "$h" -lt "$START_HOUR" ] || [ "$h" -ge "$END_HOUR" ]; then sleep 900; continue; fi
  if [ "$h" -ge 8 ] && [ ! -f "logs/digest-$today.done" ]; then run "/daily-digest" >/dev/null; touch "logs/digest-$today.done"; fi
  if [ "$dow" = 1 ] && [ "$h" -ge 9 ] && [ ! -f "logs/plan-$wk.done" ]; then run "/plan-week" >/dev/null; touch "logs/plan-$wk.done"; fi
  if [ "$dow" = 4 ] && [ "$h" -ge 14 ] && [ ! -f "logs/rc-$wk.done" ]; then run "/release-candidate" >/dev/null; touch "logs/rc-$wk.done"; fi
  if run "/work-next" | grep -q "No ready work"; then sleep $((IDLE_MIN*60)); else sleep $((BUSY_MIN*60)); fi
done
