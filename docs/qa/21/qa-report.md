# QA report for issue #21, game feel pass

Branch `task/21-game-feel-pass` at 0fcf702, checked by QA on 2026-10-28.

## 1. Tests and console
- Refresh with forced compile, then console read: 0 errors, 0 warnings. A second read after the Play Mode run: still 0.
- Edit Mode: 430 / 430 passed, 0 failed, 0 skipped (24 s).
- Play Mode: 47 / 47 passed, 0 failed, 0 skipped (26 s).
- First Edit Mode attempt lost its connection during a domain reload (tool-side); the rerun was clean.
- A transient `Assets/InitTestScene*.unity` existed during the Play Mode run. It was gone at the end. `git status` shows only the two DOTween files modified and nothing untracked.

## 2. Changed tests against origin/main (no weakening found)
Diffed all test files changed on the branch. There are no `[Ignore]`, `[Explicit]` or timeout changes anywhere in `Assets/Tests`. No threshold or other test was edited. Only the asmdef references changed (Game.Services, DOTween.dll).
- `ClearRevealMotionTests.Reveal_MovesTowardsFront_Monotonically_OnTheLine` became `Reveal_MovesForwardOnTheLine_OvershootsALittle_ThenSettlesBack`. Required by the bounce. Stricter: it keeps the straight-line x check, adds forward-only before the peak and back-only after, requires overshoot above 1.02 and at most `RevealPeakProgress`, and requires an exact end on the front slot. 200 samples instead of 50. Three new tests were added (overshoot size, brightness and scale, clear squash).
- `Clear_PopsThenFades_AndTilesEndHiddenAndReset`: adds a flight step (`IsShelfFlying`, advance `ArcSeconds`, then asserts the landed state) before the unchanged pop and fade checks. Needed for the 0.25 s arc. All later asserts are unchanged.
- `Clear_DrivenByFrames_FinishesInUnderHalfASecond` became `..AfterLanding`. The 0.5 s + 0.1 s bound is now measured from landing, with an extra bound of flight + clear + 0.1 s from the start. Equivalent and still bounded.
- `Chain_RevealThenClear_...`: the final advance is now `ClearSeconds + ArcSeconds - RevealSeconds`. This is an exact timing derivation, not a loosened one. The assertions after it are unchanged (`IsAnimating` false, one OutcomeReady, Won).
- `MoveOntoAnimatingShelf_SnapsItToItsFinalStateFirst`: `Assert.IsFalse(IsShelfAnimating(0))` became `Assert.IsTrue(IsShelfFlying(0))`. This is justified because the new move always starts a flight, and the letter, scale and alpha asserts that prove the snap are unchanged. Minor note: it no longer proves directly that no clear step remains queued behind the flight. Consider asserting the step count is 1. This is not a fail.
- `ResultPanelLayoutTests`: one test added, none changed.

## 3. Allocation test
`Feel_AllocatesNothing_OverArcClearCoinRevealAndStarDrop` is real. Inside `Is.Not.AllocatingGCMemory()` it runs 150 frames of `_view.AdvanceAnimations(Frame)` plus DOTween's `TweenManager.Update(UpdateType.Normal, dt, dt)`, with the delegate created by reflection beforehand. A full warm-up chain is run first. After the measured block it asserts that the work really happened: Won outcome, 2 bursts in the block, coin counter "2", Win panel visible, stars at rest, at least 2 confetti bursts. It is not an empty loop. Limits: DOTween is stepped manually (the same method the DOTweenComponent calls), the particle system's native side is not counted by the managed GC measure, and nothing was measured on a device.

## 4. Layering
- `Game.Core` and `Game.Levels` have `noEngineReferences: true` and no references to Services, UI, View or DOTween. The branch has zero diff in `Assets/Scripts/Core` and `Assets/Scripts/Levels`. grep for DOTween, coin or star in Core, Levels and Solver returns nothing.
- Coin and star state: the counter is display-only in UI (`ClearCounter`). The only persisted value is `settings.haptics` (int) via PlayerPrefs. Stars are a constant 3. No wallet and no save of coins or stars.
- Singletons: `GameServices` is the only static locator. The other statics are sprite and material caches (`PlaceholderSprites`, `ParticleFx._material`), which are not service singletons. The `FindAnyObjectByType` calls (`DevOverlay`, `EventSystem`) already existed or are not singletons. Note: those caches are static, so they survive domain-reload-off Play Mode. Not a fail.
- Services are behind interfaces (`IHaptics`, `ISettingsService`, `IKeyValueStore`, `IHapticDevice`) with fakes in `Services/Fakes` (`FakeHaptics`, `FakeSettingsService`, `FakeKeyValueStore`).
- Note: `Game.Services` has `autoReferenced: true`.

## 5. Haptics gating
`Haptics.Play` checks `ISettingsService.HapticsEnabled` on every call before the device. `HapticsTests.PlaysNothing_WhenOff` plays both pulses (WrongMove and Light) and asserts `device.Total == 0`. `FollowsTheSetting_AsItChanges` checks the live toggle. The two call sites are the wrong-move shake and drop snap-back in `BoardView` (WrongMove) and the clear shown (Light). The pulses are short and light (12 ms, amp 60; 35 ms, amp 120). The setting defaults to on and is persisted.

## 6. `AndroidVibratePermission`
It reads correctly. It runs after Gradle generation and finds `unityLibrary/src/main/AndroidManifest.xml`. `AddPermission` returns the input unchanged if the permission is already present, so it is idempotent. Otherwise it inserts one `uses-permission` line after the first `<manifest ...>` tag, and the Gradle merger carries it into the app. It logs an error if the manifest is missing. Gaps, none blocking:
- No unit test covers `AddPermission`, although it is a pure static method. Recommend adding one for the insert and the idempotence.
- Edge cases: a self-closing `<manifest/>`, or a `<manifest` inside a comment, would be mishandled. Unlikely in Unity's generated file.
- Never verified in a real APK, which the author states.

## 7. Dependencies
- `Packages/` unchanged. `ProjectSettings` is unchanged in the 6 commits.
- No art, font or texture assets were added. Sprites, shadow, coin, star and dot are generated in code (`PlaceholderSprites`). Only `Assets/Plugins/Demigiant.meta` is added besides the DOTween import in 57869c2.
- `docs/approved-dependencies.md` DOTween row: "DOTween (free) 1.3.045, Demigiant ... Game.View and Game.UI only, never Core or Levels ... Chosen over Unity's own tweening (issue #21)". The choice is recorded and the scope is right. The licence column says "Muthu to confirm". That is an open item for Muthu, not a fail.

## 8. DOTween setup files in the working tree (not committed)
- `Assets/Resources/DOTweenSettings.asset`: `createASMDEF` 0 to 1. This is the normal result of DOTween's Setup panel (it creates a DOTween asmdef so assemblies can reference it). It is consistent with the `DOTween.dll` precompiled reference in the Play Mode asmdef and with `Game.View.asmdef`. Normal.
- `ProjectSettings/ProjectSettings.asset`: only `scriptingDefineSymbols` changed, from `{}` to `DOTWEEN` for every platform group. This is the standard DOTween define. Nothing else in ProjectSettings changed (the diff is 19 added lines and 1 removed, all in that block). Looks normal. Decision for Muthu: they should be committed (the define is needed in CI and on other machines) or the setup re-run. Not committed here as instructed.

## 9. Screenshots and LFS
- `docs/qa/21` holds 6 key frames at 1080x1920 plus 36 sequence frames, 42 PNGs, 2.3 MB. All 42 are Git LFS pointers (`git lfs ls-files` lists 42 and `.gitattributes` has `*.png filter=lfs`).
- Viewed `1_pickup`, `3_clear_burst_coin` and `5_stars_confetti`. Pick-up shows the lifted tile with an amber highlight and a shadow. The clear shows the three tiles greyed out and fading, with particles and the coin flying toward the counter at bottom right. The Win frame has the card, three stars, Next and Retry buttons, and confetti, and the counter reads 3.
- Cosmetic notes, not blocking: in the Win frame the board status line "Cleared!" is visible behind the confetti above the card, so the word shows twice. The HUD is dimmed under the scrim, which is fine. The toggle reads "Haptics On" in text and relies on the word for its state.
- No screen recording is in the repo. The issue asks for one in the PR. That is still the author's to attach.

## 10. Existing panels, safe area, 48 dp
- All the `ResultPanels` tests, including Next, Retry, Replay and safe-area anchors, pass in Edit Mode and Play Mode.
- The new `StarRow` test checks the card still fits at 3 safe sizes and that buttons stay at least `MinTouchDp`.
- The haptics toggle is 72 x 48 dp (`HudLayout`), tested at 48 dp or more at 3 screen sizes and inside the bottom reserve, with a 16 dp right margin. It is parented under a `Safe Area` RectTransform driven by `Screen.safeArea`. A tiny reserve keeps the 48 dp floor.
- The counter pill is 84 x 40 dp, but it is display-only, so it is not a tap target.

## 11. Stray files
No `Assets/InitTestScene*` files are left (verified after the runs).

## Not verified
- Frame time on a device and the real APK (the author reports none either). The author's 1.22 ms editor PlayerLoop figure was not re-measured. The `Feel_FrameTimeAndGc_AreMeasured` test passed.
- Real vibration. Haptics are tested only through fakes.

RESULT: PASS

Open items for the author (none blocking):
1. Attach the screen recording to the PR.
2. Add a unit test for `AndroidVibratePermission.AddPermission` (insert once, idempotent).
3. Tighten `MoveOntoAnimatingShelf` to also assert that no step is queued behind the flight.
4. Muthu to confirm the DOTween licence and to decide whether to commit the two DOTween setup files (the `DOTWEEN` define matters in CI).
5. Win frame shows the "Cleared!" status text twice (behind the confetti).
