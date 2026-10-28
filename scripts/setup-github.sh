#!/usr/bin/env bash
# Creates the labels the agent workflow uses. Run once: bash scripts/setup-github.sh
set -e
mk() { gh label create "$1" --color "$2" --description "$3" --force; }
mk proposed BFD4F2 "Drafted by an agent, waiting for Muthu"
mk ready 0E8A16 "Approved, agents may start"
mk in-progress FBCA04 "An agent is working on it"
mk needs-review 1D76DB "PR waiting for Muthu"
mk blocked B60205 "Needs an answer from Muthu"
mk needs-editor 5319E7 "Requires the Unity editor via MCP"
mk needs-art-approval D93F0B "Art waiting for Muthu"
mk p1 B60205 "Priority 1"; mk p2 FBCA04 "Priority 2"; mk p3 C2E0C6 "Priority 3"
for a in code ui systems levels art qa release store analytics docs; do mk "area:$a" EDEDED "Area: $a"; done
mk digest CCCCCC "Daily digest"; mk plan CCCCCC "Weekly plan"; mk release 0E8A16 "Release candidate"
echo "Labels ready."
