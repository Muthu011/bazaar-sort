---
name: daily-digest
description: Write the morning digest for Muthu as a GitHub issue — merged yesterday, waiting for review, blocked questions, next up, editor and CI health.
---
Act as the producer in `docs/producer.md`.
Gather: PRs merged in the last 24 h; open PRs labelled `needs-review` (oldest first, with age); issues labelled `blocked` with their question; the next 5 `ready` issues; last nightly CI run status (`gh run list --workflow nightly.yml --limit 1`); the latest QA sweep result in `docs/qa/nightly/`.
Create an issue titled `Digest <YYYY-MM-DD>` labelled `digest` with these sections, in this order and kept short:
1. Needs you today (blocked questions and the oldest reviews, max 5 lines)
2. Shipped yesterday
3. Waiting for review
4. Next up
5. Health (nightly build, QA sweep, editor bridge, plan usage if visible)
Close yesterday's digest issue.
