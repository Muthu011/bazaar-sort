using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Adds the VIBRATE permission to the generated Android manifest (issue #21). Unity adds it on its own only when a
    /// script calls <c>Handheld.Vibrate</c>; our haptics go through <c>AndroidJavaObject</c> (Game.Services
    /// AndroidHapticDevice), which Unity cannot see, so without this Vibrator.vibrate would throw a SecurityException.
    /// VIBRATE is a "normal" permission: granted at install, no runtime prompt. Code, not a hand-edited manifest, so
    /// the change is reviewable and survives Unity regenerating the Gradle project.
    /// </summary>
    public sealed class AndroidVibratePermission : IPostGenerateGradleAndroidProject
    {
        public const string Permission = "android.permission.VIBRATE";

        public int callbackOrder => 0;

        /// <summary><paramref name="path"/> is the unityLibrary module of the generated Gradle project.</summary>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"AndroidVibratePermission: no manifest at {manifestPath}; haptics will not work.");
                return;
            }

            string manifest = File.ReadAllText(manifestPath);
            string updated = AddPermission(manifest);
            if (updated != manifest) File.WriteAllText(manifestPath, updated);
        }

        /// <summary>Returns <paramref name="manifest"/> with the VIBRATE permission, inserted once after &lt;manifest ...&gt;.</summary>
        public static string AddPermission(string manifest)
        {
            if (manifest.Contains("\"" + Permission + "\"")) return manifest;
            int open = manifest.IndexOf("<manifest", System.StringComparison.Ordinal);
            if (open < 0) return manifest;
            int close = manifest.IndexOf('>', open);
            if (close < 0) return manifest;
            string line = "\n  <uses-permission android:name=\"" + Permission + "\" />";
            return manifest.Insert(close + 1, line);
        }
    }
}
