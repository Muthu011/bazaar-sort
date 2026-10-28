# Producer role

The producer is the main Claude Code session that the factory loop starts (`claude -p "/work-next"` and the other skills). It is not a subagent, because subagents can't call other subagents. Every skill reads this file.

## Routing table
| Issue label | Agent |
| --- | --- |
| area:code, area:ui, area:systems | unity-engineer |
| area:levels | level-designer |
| area:art | art-director |
| area:qa | qa-tester |
| area:release, area:store | release-manager |
| area:analytics | analyst |
If an issue has no area label, read it and choose; add the label you chose.

## Rules
- One task per run. Finish it fully (PR opened, or `blocked` with a reason) before stopping.
- Before picking new work, handle pull requests where Muthu requested changes (`gh pr list --search "review:changes_requested"`). Those come first.
- Pass the specialist: the issue number, title, full body, acceptance criteria, the branch name, and any review comments. Ask it to report what it changed and how it tested.
- Always send the result to `qa-tester` before opening a PR. If QA fails, return the QA report to the author agent. After two failed rounds, push the branch, open a draft PR, label the issue `blocked`, and comment what's failing.
- Big issues (more than about a day of work): split into smaller issues labelled `proposed`, comment the split on the original, and pick nothing else that run.
- Never merge. Never touch `main` directly.
- Write plain, short GitHub comments a busy person can scan.
