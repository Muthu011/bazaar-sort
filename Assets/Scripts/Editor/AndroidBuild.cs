using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the development APK for testing on a phone (issue #8). Menu "Bazaar Sort/Build Android Dev APK", or
    /// headless:
    /// <code>Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Game.EditorTools.AndroidBuild.BuildDevApk</code>
    ///
    /// Signing: custom keystore settings are left off, so Gradle signs with Unity's default Android debug keystore.
    /// This script never reads, creates or configures a keystore and never touches store or release settings.
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputPath = "Builds/Android/BazaarSort-dev.apk";

        /// <summary>Android 8.0. Covers practically every phone in use in India in 2026 (Android Go included).</summary>
        public const AndroidSdkVersions MinSdk = AndroidSdkVersions.AndroidApiLevel26;

        [MenuItem("Bazaar Sort/Build Android Dev APK")]
        public static void BuildDevApkMenu()
        {
            var report = BuildDevApk();
            if (report != null && report.summary.result == BuildResult.Succeeded && File.Exists(OutputPath))
            {
                EditorUtility.RevealInFinder(OutputPath);
            }
        }

        /// <summary>
        /// Applies the dev player settings, builds <see cref="OutputPath"/> as a development build and logs the result,
        /// size and time. In batchmode, exits with code 1 on failure. Returns the report (null if it could not start).
        /// </summary>
        public static BuildReport BuildDevApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Fail("AndroidBuild: Android Build Support is not installed in this editor. Install the Android module (with SDK, NDK and OpenJDK) from Unity Hub.");
                return null;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Fail("AndroidBuild: could not switch the active build target to Android.");
                return null;
            }

            ApplyDevPlayerSettings();
            LevelListSync.Sync(); // every level file on disk is in the build, in play order

            if (!File.Exists(GameSceneBuilder.ScenePath))
            {
                Fail($"AndroidBuild: {GameSceneBuilder.ScenePath} is missing. Run \"Bazaar Sort/Build Game Scene\" first.");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            if (File.Exists(OutputPath)) File.Delete(OutputPath); // never report a stale APK
            bool wasAppBundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = false; // an .apk for adb install, not an .aab

            var options = new BuildPlayerOptions
            {
                scenes = new[] { GameSceneBuilder.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };

            var timer = Stopwatch.StartNew();
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = wasAppBundle;
            }
            timer.Stop();

            var summary = report.summary;
            long bytes = File.Exists(OutputPath) ? new FileInfo(OutputPath).Length : 0;
            string line = $"AndroidBuild: {summary.result} in {timer.Elapsed.TotalSeconds:F0} s, " +
                $"{bytes / (1024f * 1024f):F1} MB ({bytes} bytes), {summary.totalErrors} errors, " +
                $"{summary.totalWarnings} warnings, backend {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}, " +
                $"output {Path.GetFullPath(OutputPath)}";

            // A "Succeeded" report without an APK happens when the Android module was installed while the editor
            // was open (its build extension is not loaded until restart), so the file is the real check.
            if (summary.result == BuildResult.Succeeded && bytes > 0)
            {
                Debug.Log(line);
            }
            else
            {
                Fail(bytes > 0 ? line : line + " -- no APK was written. If Android Build Support was installed while " +
                    "this editor was open, restart Unity and build again.");
            }
            return report;
        }

        /// <summary>
        /// Sets the player settings a phone build needs, only where they differ: portrait only, application id, min
        /// API 26, ARM64 only, IL2CPP. Leaves signing, versions and store settings alone.
        /// </summary>
        public static void ApplyDevPlayerSettings()
        {
            var android = NamedBuildTarget.Android;

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            if (!PlayerSettings.allowedAutorotateToPortrait) PlayerSettings.allowedAutorotateToPortrait = true;
            if (PlayerSettings.allowedAutorotateToPortraitUpsideDown) PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            if (PlayerSettings.allowedAutorotateToLandscapeLeft) PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            if (PlayerSettings.allowedAutorotateToLandscapeRight) PlayerSettings.allowedAutorotateToLandscapeRight = false;

            if (PlayerSettings.GetApplicationIdentifier(android) != ProjectSetup.PackageId)
                PlayerSettings.SetApplicationIdentifier(android, ProjectSetup.PackageId);

            if (PlayerSettings.Android.minSdkVersion < MinSdk) PlayerSettings.Android.minSdkVersion = MinSdk;
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (PlayerSettings.GetScriptingBackend(android) != ScriptingImplementation.IL2CPP)
                PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);

            AssetDatabase.SaveAssets();
        }

        private static void Fail(string message)
        {
            Debug.LogError(message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
