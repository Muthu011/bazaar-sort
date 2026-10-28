---
name: work-next
description: Take the next piece of work from the GitHub board, get it built and tested by the right agent, and open a pull request. Run by the factory loop every cycle.
---
Act as the producer described in `docs/producer.md`. Do exactly one task, then stop.

1. If a `STOP` file exists in the repo root, say so and stop.
2. Check the Unity bridge: call the Unity MCP console-read tool. If it fails, note "editor down" and skip any issue labelled `needs-editor` this run.
3. `git fetch origin` and make sure the working tree is clean. If it isn't, comment on the last in-progress issue and stop.
4. Pick work, in this order:
   a. Open PRs with requested changes: `gh pr list --search "review:changes_requested" --json number,headRefName,title`.
   b. Issues labelled `ready`, highest priority first (`p1` > `p2` > `p3`), then oldest: `gh issue list --label ready --state open --json number,title,labels,body --limit 30`.
   If nothing is ready, write "No ready work" and stop.
5. For a new issue: replace label `ready` with `in-progress`; create branch `task/<number>-<short-slug>` from `origin/main`; refresh Unity if the editor is up.
   For a PR fix: check out its branch and gather the review comments (`gh pr view <n> --comments`).
6. Route to the specialist agent from the routing table in `docs/producer.md`, giving it the issue (or review comments), acceptance criteria and branch name.
7. Send the result to `qa-tester`. On `RESULT: FAIL`, return the report to the author agent; maximum two rounds.
8. Commit any remaining changes, push the branch (`git push -u origin <branch>`).
9. Open or update the PR: title = issue title; body = summary, acceptance criteria checklist with evidence, tests run, QA report link, screenshots, `Closes #<number>`. Label `needs-review`. Label the issue `needs-review` too.
10. If blocked: push what exists, open a draft PR if useful, label the issue `blocked`, comment the reason and the exact question for Muthu.
11. Print a 3-line summary: task, outcome, PR link.
