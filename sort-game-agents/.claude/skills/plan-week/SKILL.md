---
name: plan-week
description: Draft the coming week's work as GitHub issues labelled proposed, from the go-live plan, last week's results and (after launch) the analyst's Monday report.
---
Act as the producer in `docs/producer.md`.
1. Read `docs/game-design.md` (milestones and dates), last week's closed issues, open `blocked` issues, and after launch ask the `analyst` agent for the Monday report.
2. Work out where the project stands against the current milestone and its date.
3. Draft 10–20 issues for the week using `.github/ISSUE_TEMPLATE/task.yml` fields: goal, acceptance criteria, area label, priority (`p1`–`p3`), `needs-editor` when the Unity editor is required. Each issue should take an agent less than a day.
4. Create them labelled `proposed` (not `ready`).
5. Create an issue `Week plan <YYYY-Www>` labelled `plan` that lists them in priority order with one line on why, plus risks to the milestone date. Muthu approves by relabelling issues `ready`.
