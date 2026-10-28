# QA report - #3 Level JSON loader and validation

Branch: `task/3-level-loader` - Date: 2026-10-28
Scope: `git diff origin/main..HEAD` (commits 3e522d1, 5afc425, c4aec2c, 8d4e1a5): new Game.Levels assembly (LevelData, LevelError, LevelLoader, LevelParser, LevelValidator, ObjectCatalog), three Edit Mode test files, 7 sample JSON files, Newtonsoft dependency, plus .meta files.

Unity MCP did not connect for QA. Test and console results below are the producer's run through Unity MCP on HEAD 8d4e1a5 (after forced refresh and recompile), not re-run by QA: EditMode 121/121 passed (60 existing + 61 new), PlayMode 1/1 passed, console 0 errors and 0 warnings. Code review, meta-file, style, asmdef and dependency checks were done by QA directly from the repo.

## Results
| Check | Result | Evidence |
| --- | --- | --- |
| Edit Mode tests | PASS (producer) | 121/121 passed |
| Play Mode tests | PASS (producer) | 1/1 passed |
| Console | PASS (producer) | 0 errors, 0 warnings |
| .meta files | PASS | Every added non-.meta file has a .meta; folders `Assets/Scripts/Levels` and `Assets/Tests/EditMode/LevelSamples` have .meta |
| Style | PASS | PascalCase public members, `_camelCase` private fields (`_loadSettings`, `_known`, `_errors`, `_levelId`), no singletons |
| Working tree | PASS | Clean before the report was added |
| Levels / solver | n/a | `w1_l001.json` unchanged |
| Screens / screenshots | n/a | No scene or UI |
| Frame time | n/a | No scene |
| Build size | NOTE | Newtonsoft adds roughly 0.5-1 MB before stripping to an Android build. Measure at the first Android build (#8) |

## Architecture decision
| Item | Result | Evidence |
| --- | --- | --- |
| Newtonsoft 3.2.2 approved and direct dependency | Met | `docs/approved-dependencies.md` line 10; `Packages/manifest.json` has `com.unity.nuget.newtonsoft-json: 3.2.2`; packages-lock updated |
| Game.Levels assembly reads JSON, builds Core objects | Met | `Game.Levels.asmdef` references Game.Core, `overrideReferences` with `Newtonsoft.Json.dll`, `noEngineReferences: true`. `LevelLoader.Build` creates `Shelf`/`Board` |
| Game.Core unchanged, no JSON/Unity | Met | `git diff origin/main -- Assets/Scripts/Core` is empty |
| No hand-rolled JSON parser | Met | Only `JsonTextReader` / `JToken.ReadFrom` in `LevelParser` |
| JsonTextReader/JToken, not reflection | Met | grep finds no `JsonConvert`, `DeserializeObject`, `JsonSerializer`, `[JsonProperty]` or `ToObject` anywhere in Assets scripts or tests. Fields read manually from `JObject`/`JArray`; IL2CPP stripping safe |

## Acceptance criteria
| # | Criterion | Status | Evidence |
| --- | --- | --- | --- |
| 1 | Loads `Assets/Levels/World1/w1_l001.json` into a Board | Met | `Load_World1Level1_BuildsBoard`, `Load_World1Level1_TumblerToShelf1Clears` (loads the real file) |
| 2 | Errors: wrong slot count, count not multiple of 3, unknown object id, no empty front slot | Met | `WrongSlotCount`, `ItemCountNotMultipleOfThree`, `UnknownObjectId`, `NoEmptyFrontSlot` codes in `LevelValidator`; `Load_WrongSlotCount_IsRejected`, `Load_ItemCountNotMultipleOfThree_IsRejected`, `Load_UnknownObjectId_IsRejected`, `Load_NoEmptyFrontSlot_IsRejected` |
| 3 | Edit Mode tests with one valid and four invalid sample files | Met | `LevelSamples/valid.json` plus `invalid_wrong_slot_count`, `invalid_item_count`, `invalid_unknown_object`, `invalid_no_empty_front_slot`; `Load_ValidSample_BuildsBoardWithLayers` |
| Extra | Reject starting front-row triple, including empty front row whose first non-empty hidden layer is a triple; error names level, shelf index, item id; deeper triples allowed; test per case | Met | `StartingTriple` error carries LevelId, ShelfIndex (+ShelfId), layer and ObjectId. Samples `invalid_starting_triple_front.json`, `invalid_starting_triple_revealed.json`. Tests: `Validate_FrontRowTriple_NamesLevelShelfAndItem`, `Validate_EmptyFrontWithTripleDirectlyBehind_IsError`, `Validate_EmptyFrontAndEmptyLayersThenTriple_IsError`, `Validate_TripleInDeeperHiddenLayer_IsAllowed`, `Validate_TripleBehindNonEmptyFront_IsAllowed`, `Validate_TripleTwoLayersBehindEmptyFrontButBehindNonEmptyLayer_IsAllowed`, `Load_StartingTripleInFrontRow_IsRejected`, `Load_StartingTripleRevealedBehindEmptyFront_IsRejected` |

## Code review (validator logic)
No blocking bugs found.
- Starting front row search (`CheckStartingFront`) skips empty layers like `Board` does, stops at the last layer, and treats `""` as empty like `Shelf`. A fully empty shelf yields 3 free slots and no triple.
- Free-slot detection uses the real starting front row (after reveal), not layer 0, so a shelf with an empty front row and a full layer behind is correctly not counted as free. The no-empty-slot error is suppressed when a malformed layer makes the front unknown, which avoids a misleading second error.
- Counting: unknown ids are not counted (one error per bad slot); mis-sized layers are still counted; errors follow file order.
- Parser: duplicate keys, trailing content, wrong types, empty text all give errors with line and position. Loader never throws on bad content and stops before validation on parse errors, so `Board` (which asserts on starting triples under UNITY_ASSERTIONS) is never built from unvalidated data.
- Error messages name the level, shelf index and id, layer, slot and problem, e.g. `w1_l001: shelf 2 ("s3"), layer 0, slot 1: unknown object id "tumblr"`.

## Notes (not failures)
1. Tray exemption is broad: any object id that appears in any tray `set` is skipped entirely from the multiple-of-3 check, even when it also appears on shelves. Documented in a code comment and deferred to World 2 trays (level 21). Revisit when trays are built.
2. Duplicate shelf ids within one level are not checked.
3. `locked`/`conveyor` kinds, `wrapped`, `role` and `tutorial` values are accepted but not validated or built specially (documented in the `LevelValidator` summary).
4. `ObjectCatalog` has World 1 ids only; later worlds must add theirs.
5. Build size: Newtonsoft adds roughly 0.5-1 MB before stripping. Measure at the first Android build (#8).
6. Unity MCP results are the producer's, not re-run by QA.

RESULT: PASS
