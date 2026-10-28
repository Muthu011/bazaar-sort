# QA report: issue #4 Placeholder board view (branch task/4-placeholder-board-view)

## 1. Tests
- Edit Mode: 146/146 passed, 0 failed.
- Play Mode: 6/6 passed, 0 failed.

## 2. Console
After a forced refresh with compile and after both test runs: 0 errors, 0 warnings.
(One MCP-for-Unity WebSocket log line appeared right after refresh; it is a tool-bridge message from domain reload, not project code, and was gone on the second read.)

## 3. Levels
No levels changed (diff vs origin/main has no Assets/Levels files). Solver check N/A.

## 4. Screenshots
docs/qa/4/w1_l001_1080x1920.png (16:9) and w1_l001_1080x2400.png (20:9) viewed. 4 shelves, all fully on-screen with margins on every side, nothing clipped. T / I / M tiles are clearly distinct by colour and letter. Letters are not cut off. The files are Git LFS pointers in the repo, and the actual files are present locally.
Not independently re-captured by QA: I reviewed the author's images and did not re-run the capture tool. Only the 4-shelf case is shown in screenshots; the 14-shelf layout is covered only by the BoardLayoutTests unit tests.

## 5. Acceptance criteria
- Shelves laid out for portrait 4 to 14 shelves, nothing off-screen on 16:9 and 20:9: MET for 4 shelves (screenshots); 4..14 covered by BoardLayoutTests (passing). No visual evidence for 14 shelves.
- Each object type has distinct colour and letter (placeholder table in code): MET. PlaceholderPalette + PlaceholderPaletteTests pass.
- View listens to Core events only, holds no rules: MET. BoardView subscribes to Moved, Cleared, LayerRevealed, Won, Stuck; unsubscribes in Unbind/OnDestroy; only reads Shelf/Board state (GetFrontItem, GetHiddenItem, HiddenLayerCount) and redraws. No move validation or clear logic. LevelBootstrap.Update only relayouts on screen or safe-area change.
- Screenshot of w1_l001 on 16:9 and 20:9 in the PR: MET (files committed under docs/qa/4/); PR body not checked since no PR exists yet.

## 6. Other checks
- Dependencies: no Packages/ changes. Font is Unity's built-in LegacyRuntime.ttf with legacy TextMesh (not a new font asset). Nothing added outside docs/approved-dependencies.md. Note: approved list names TextMeshPro for text; legacy TextMesh is a deviation in spirit, but it adds no dependency. Suggest the author switch to TMP when real UI text lands.
- asmdef changes: Game.View now references Game.Levels; test asmdefs gained references. Those are additions only. EditorBuildSettings adds Game.unity as scene 0 (SampleScene kept). Only deletions in the test diff are two asmdef lines rewritten with a trailing comma. No test or threshold weakened.
- Frame time: NOT profiled. I did not capture a frame-time measurement. Code inspection: static sprites/TextMesh, objects created once in Bind, redraws only toggle/recolour, Update does only a cheap compare. Expected far below 16.6 ms, but this is an inference. Editor frame time is also not representative of an Android device.
- Build size: no new packages, no new art or textures. Delta is C# code (~1000 lines), one scene (302 lines YAML), and procedurally generated sprites. Expected to be negligible, but a real build was not made so no measured number.
- Player-facing notes: tiles are about 215 px on a 1080-wide screen, well over the 48 px tap target. The first row of the board leaves a large empty strip at the top (HUD reserve). Hidden-layer tiles were not visible in w1_l001 screenshots, so the dimmed hidden strip rendering is not visually verified.

RESULT: PASS
Open items for the author, none blocking: (1) record a frame-time measurement and build size in the PR; (2) add a 14-shelf screenshot; (3) consider TMP over legacy TextMesh.
