# Android dev build: build, install, check fps

For Slice 1 testing on a real phone (issue #8). Development builds only. They are signed with Unity's default
Android debug keystore. No release keystore is created, configured or committed.

Paths below assume Unity 6000.6.6f1 installed by Unity Hub on macOS:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity
ADB=/Applications/Unity/Hub/Editor/6000.6.6f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
```

## 1. Phone: developer options and USB debugging (once)

1. Settings > About phone > tap **Build number** 7 times. On Xiaomi/Redmi/POCO it's **MIUI/HyperOS version**,
   on Realme/OPPO it's **Version** under Software info. Enter your PIN if asked.
2. Settings > System > **Developer options**: turn on **USB debugging**. On Xiaomi phones also turn on
   **Install via USB**, which needs a Mi account and a SIM.
3. Connect the phone with a data USB cable. On the phone, accept **Allow USB debugging?** and tick "Always allow".
4. Check: `"$ADB" devices` lists the phone as `device`. If it says `unauthorized`, unlock the phone and accept the prompt.

## 2. Build the APK

Either:

- **Editor:** menu **Bazaar Sort > Build Android Dev APK**. Finder opens on the APK when it finishes.
- **Command line** (close the editor first, because Unity won't open the same project twice):

  ```sh
  cd ~/Desktop/Workspace/unity/bazar-sort
  "$UNITY" -batchmode -quit -projectPath . -buildTarget Android \
    -executeMethod Game.EditorTools.AndroidBuild.BuildDevApk -logFile Logs/android-build.log
  ```

  Exit code 0 means success. Search the log for the `AndroidBuild:` line to see the result, size and time.

The first IL2CPP build takes about 7 minutes. Later builds are faster.

If the build reports `Build target 'Android' not supported` or "no APK was written", the Android module was
installed while the editor was open. Quit and reopen Unity, then build again.

Output: `Builds/Android/BazaarSort-dev.apk`. `Builds/` is gitignored, so never commit the APK.

The build is a development build, IL2CPP, ARM64 only, minimum Android 8.0 (API 26), portrait only, with the
application id `com.muthu.bazaarsort`.

## 3. Install over USB

```sh
"$ADB" install -r Builds/Android/BazaarSort-dev.apk
```

`-r` replaces an earlier install and keeps its data. If it fails with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`
(an older build signed with a different key), uninstall first with `"$ADB" uninstall com.muthu.bazaarsort`.

## 4. Launch

Tap **Bazaar Sort** in the app drawer, or run:

```sh
"$ADB" shell monkey -p com.muthu.bazaarsort -c android.intent.category.LAUNCHER 1
```

Dev builds show a **Development Build** watermark in the bottom-right corner, a row of level buttons
**L1 to L5** at the top (tap one to jump to that World 1 level), and an fps readout at the bottom left.
Release builds don't show the level buttons or the fps readout.

## 5. Check fps

Use any one of these. The target is a steady **60 fps**, with frames under 16.6 ms.

1. **On-screen readout (simplest):** the bottom-left shows `NN fps` and `max NN ms`. These are the average and
   the slowest frame over the last half second. The fps turns red below 55. Play a few levels, then note the
   typical fps and the worst `max` you see while moving items and clearing a row.
2. **Unity Profiler over USB:** in the editor, open Window > Analysis > Profiler. With the app running, pick the
   phone (`AndroidPlayer(ADB@127.0.0.1:34999)`) in the target dropdown. If the phone isn't listed, run
   `"$ADB" forward tcp:34999 localabstract:Unity-com.muthu.bazaarsort` and choose
   "Direct connection" `127.0.0.1:34999`. Check the CPU frame time stays under 16.6 ms.
3. **gfxinfo:** play for about 30 seconds, then run:

   ```sh
   "$ADB" shell dumpsys gfxinfo com.muthu.bazaarsort framestats | head -40
   ```

   Read "Janky frames" and the 90th/95th percentile frame times. Unity draws to its own surface, so on many
   phones gfxinfo counts few or no Unity frames. If it shows no frames, use option 1 or 2.

## 6. Note the phone

Put the result in the pull request for issue #8, under "Device check", for example:

```
Phone: <brand model> (<RAM> GB, Android <version>, <chipset>)
fps: typical 60, lowest seen 57; max frame 19 ms (w1_l005 clear)
Method: on-screen readout / Profiler / gfxinfo
```

Find the model under Settings > About phone, or run `"$ADB" shell getprop ro.product.model`.
