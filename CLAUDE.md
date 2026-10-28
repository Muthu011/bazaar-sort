# Bazaar Sort (working title) — rules for every agent

## The game
A 2.5D goods-sort puzzle with authentic Indian everyday objects. Players move objects between shelves; 3 identical objects on a shelf clear and reveal the layer behind. Android first, Unity 6 LTS, 2D URP, portrait. Full plan: "Indian Object Sort Game: Go-Live Plan" (summary in `docs/game-design.md`).
Launch: Google Play India, 12 January 2027 (before Pongal). Owner and only human: Muthu.

## Repo layout
- `Assets/Scripts/Core/` — pure C# game logic (board, shelves, moves, solver). No UnityEngine dependence except math types. All rules live here and are unit tested.
  Core holds game rules only: no selection, drag, or other interaction state (that lives in View).
- `Assets/Scripts/Levels/` — level JSON parsing and validation (Game.Levels, Newtonsoft), building Core objects.
- `Assets/Scripts/View/` — MonoBehaviours, animation, input. Thin; calls Core.
- `Assets/Scripts/UI/` — screens built from code + a few prefab templates in `Assets/Prefabs/UI/`.
- `Assets/Scripts/Services/` — ads, IAP, analytics, remote config, save. Each behind an interface with a fake for tests.
- `Assets/Levels/World<N>/*.json` — level data, schema in `docs/level-schema.md`.
- `Assets/Art/` — approved art only (Git LFS). Provenance in `docs/art-provenance.csv`.
- `Assets/Tests/EditMode/`, `Assets/Tests/PlayMode/` — tests.
- `docs/` — design, schema, style guide, approved dependencies, QA reports (`docs/qa/`).

## How work flows
- Work comes only from GitHub issues labelled `ready`. One issue = one branch `task/<number>-<slug>` = one pull request.
- Prefer code over editor clicks: create scenes, prefabs and UI from C# or through Unity MCP tools, so changes are reviewable text.
- After switching branches or adding files, call the Unity MCP refresh tool and read the console before continuing.
- Only one task that needs the Unity editor runs at a time (label `needs-editor`).

## Definition of done (every pull request)
1. Acceptance criteria in the issue are met, item by item, and listed in the PR body.
2. Edit Mode and Play Mode tests pass; new logic has new tests.
3. Unity console shows no new errors or warnings.
4. New or changed levels pass the solver (solvable, no duplicates, difficulty in band).
5. Frame time stays under 16.6 ms in the test scene; build size change is noted.
6. Screenshots of any changed screen are attached under `docs/qa/<issue>/`.
7. PR is labelled `needs-review` and linked with `Closes #<number>`.

## Hard limits (never)
- Never push to `main`, force-push, merge a PR, or delete branches you didn't create.
- Never read, create or use keystores, Play Console credentials, API keys or payment details.
- Never publish to production, change prices, or start paid campaigns.
- Never add a package, SDK, font or asset source that isn't in `docs/approved-dependencies.md`. Propose it in an issue instead.
- Never delete or weaken tests or thresholds to make a check pass.
- Never use religious items disrespectfully (no discarding, crushing or mixing with footwear or waste) and never use deity images as puzzle pieces.
- If a file named `STOP` exists in the repo root, stop immediately.
- If something is unclear or blocked, comment on the issue, add the `blocked` label, and move on. Don't guess on design, art taste or money.

## Style
C#: Unity conventions, PascalCase public members, `_camelCase` private fields, no singletons except `GameServices`. Commit messages: `type(scope): summary (#issue)`, e.g. `feat(core): locked shelves (#14)`.
