# QA report: issue #8 Android dev build (branch task/8-android-dev-build)

## 1. Tests and console
- Edit Mode: 280/280 passed, 0 failed (after forced refresh and recompile).
- Play Mode: 32/32 passed, 0 failed.
- Console after refresh and both runs: 0 errors, 0 warnings from the project. One transient
  `MCP-FOR-UNITY [WebSocket] Unexpected receive error` warning appeared right after the domain reload. It is from the
  MCP bridge, not the game, and was gone after the test runs. Noted, not counted.

## 2. APK (not rebuilt; inspected with bundled aapt and apksigner, build-tools 36.0.0)
File: Builds/Android/BazaarSort-dev.apk, 47,443,544 bytes (about 45.2 MiB). **Build size figure: 47,443,544 bytes.**
- Package id: com.muthu.bazaarsort, versionCode 1, versionName 1.0
- minSdk 26, targetSdk 36
- ABI: arm64-v8a only (lib/arm64-v8a)
- Debuggable: yes (android:debuggable=true)
- Orientation: screenOrientation=1 (portrait), feature android.hardware.screen.portrait
- Signer: `C=US, O=Android, CN=Android Debug`, SHA-256 55b0a980...a575463. Only the APK's signer info was read; no
  keystore file was opened.

## 3. Keystore and secrets
- No *.keystore, *.jks or *.apk tracked or in the diff; the only APK on disk is the gitignored Builds/ output.
- ProjectSettings: androidUseCustomKeystore: 0; AndroidKeystoreName and AndroidKeyaliasName empty. No keystore paths or
  passwords. (The `ps4Passcode` and empty `metroCertificatePassword` fields are Unity template defaults for other
  platforms, unchanged by this branch.)
- .gitignore covers `Builds/`, `*.keystore`, `*.jks`.
- AndroidBuild.cs does not read, create or configure a keystore (comment and code).

## 4. Diff hygiene
- `git diff origin/main --stat`: 21 files, all under Assets/Scripts, Assets/Tests, docs. Nothing in ProjectSettings,
  Packages or URP assets. 8 commits ahead of origin/main.
- GameSceneBuilder.cs (editor script) has 54 added lines: for the dev screenshot/scene support; tests pass.
- Working tree: no modified tracked files. Only untracked `scripts/create-slice2-issues.sh`, left alone as instructed.
- Assets/Tests vs origin/main: only added files (DevBuildTests, DevOverlayPlayModeTests plus metas). No existing test
  modified or deleted; no thresholds changed.

## 5. Dev-only guard
DevOverlay installs through a `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` that returns unless
`DevBuild.IsLevelSelectEnabled(Debug.isDebugBuild)`. No scene references the overlay, so a release player never
creates it. The guard is Edit Mode tested (false -> disabled, editor reports isDebugBuild true). The overlay type is
compiled into release builds (DEVELOPMENT_BUILD is deprecated in 6000.6, UAC0009). Judgement: acceptable for Slice 1;
no rule broken. Note: the release path is verified by the pure-function test and code reading only, not by an actual
release-player run. Consider a release build check before launch.

## 6. Overlay logic and allocations
- The overlay holds no game rules: buttons call `LevelBootstrap.LoadLevel(index)`; layout is pure presentation math.
- fps readout: FpsSampler is an allocation-free struct; labels come from a pre-built string table (DevFpsLabels), and
  text is assigned only when a 0.5 s window completes. No per-frame string formatting. Verified by reading code; there
  is no GC-allocation test (a note: a GC.GetAllocatedBytesForCurrentThread test would make this automatic). uGUI Text
  rebuilds its mesh every 0.5 s on update, which is small.
- FrameRate.cs: targetFrameRate 60; vSyncCount 0 only on device (not in the editor).

## 7. docs/build-android.md
Paths checked: ADB at .../PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb exists. The UNITY path was not run.
Package id com.muthu.bazaarsort matches the APK. `adb install -r`, `adb uninstall`, the `monkey -p ... -c
android.intent.category.LAUNCHER 1` launch command, and the batchmode `-executeMethod
Game.EditorTools.AndroidBuild.BuildDevApk` call are correct in form. Method name/namespace were not executed here
because the APK was not rebuilt. The doc also says Play on device shows a "Development Build" watermark, L1-L5 and fps.

## 8. Screenshot
docs/qa/8/dev_level_select_1080x2400.png (1080x2400, LFS). Shows L1 to L5 buttons at the top (L1 highlighted
marigold, others teal), four shelves with placeholder letter tiles, and "0 fps / max 0 ms" at the bottom-left.
Buttons are about 168 px wide at 1080 px, about 56 dp at density 3, above the 48 dp floor. Fps shows 0 because it is a
captured still, not a live run. Tiles are letter placeholders; the T (grey) and I (white) are distinguishable. Text
is readable and does not overflow.

## 9. Acceptance criteria
1. Dev APK builds from editor; L1-L5 in dev builds only: met as far as verifiable. Menu item exists; the APK was
   produced via batchmode (in-editor build fails clearly because the Android module was installed after launch;
   restart the editor). Level select is guarded by Debug.isDebugBuild.
2. docs/build-android.md with exact steps: met.
3. 60 fps on a mid-range phone, model noted in the PR: **pending Muthu**. No device was available. fps readout,
   Profiler and gfxinfo instructions, and the PR template line are all in place. Frame time on device could not be
   measured by QA; this report makes no frame-time claim.

## Notes for the author (non-blocking)
- Release-build absence of the overlay is only tested through the pure guard.
- Add an allocation test for the fps readout if desired.

RESULT: PASS (on-phone 60 fps and device model pending Muthu; no failures)
