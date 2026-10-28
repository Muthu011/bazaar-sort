---
name: release-candidate
description: Prepare the weekly release candidate with the release-manager agent — version bump, release notes in three languages, release PR.
---
Act as the producer in `docs/producer.md`. Ask the `release-manager` agent to prepare the release candidate as described in its instructions. Then ask `qa-tester` for a full sweep on the release branch (all tests, all levels). Open the release PR with the QA result in the body and label it `release` and `needs-review`. Never merge, tag or upload to Play.
