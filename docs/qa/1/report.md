# QA report — #1 Unity project skeleton and folder layout

Branch: `task/1-project-skeleton` · Date: 2026-10-28 · Unity 6000.6.6f1 (macOS editor)

Checks were run by the producer session through Unity MCP. The `qa-tester` agent wasn't used: its tool allowlist (`mcp__unityMCP__*`) doesn't match the server name (`UnityMCP`), so it couldn't reach the editor.

## Results
| Check | Result | Evidence |
| --- | --- | --- |
| Compile | PASS | Forced refresh + compile; no errors or warnings in console |
| Edit Mode tests | PASS | 6/6 passed (`Game.Tests.EditMode.GameRulesTests`) |
| Play Mode tests | PASS | 1/1 passed (`SmokePlayModeTests.PlayerLoop_TicksAFrame_AndCoreIsReachable`) |
| Folders | PASS | Scripts/Core, View, UI, Services; Levels; Art; Prefabs/UI; Tests/EditMode, Tests/PlayMode |
| Assembly definitions | PASS | Game.Core (`noEngineReferences: true`), Game.View, Game.Tests.EditMode, Game.Tests.PlayMode |
| 2D URP | PASS | UniversalRP.asset uses Renderer2D; unchanged |
| Portrait only | PASS | Default orientation Portrait; upside-down and landscape autorotate off |
| Package id | PASS | `com.muthu.bazaarsort` (Android and Standalone) |
| Android target selected | **BLOCKED** | Android Build Support module not installed in this editor |
| Console clean | PASS* | *Only entry is the expected warning from `Bazaar Sort/Apply Project Settings` about the missing Android module |
| Levels / solver | n/a | No level changes |
| Frame time / build size | n/a | No gameplay scene yet; no Android build possible without the module |
| Screenshots | n/a | No screens changed |

## Known pre-existing noise
- `Curl error 16: Sending data failed with unitytls error code 10` appears occasionally from the editor's network layer. It isn't caused by this branch.

RESULT: PASS (except the Android-target item, which is blocked on installing the module)
