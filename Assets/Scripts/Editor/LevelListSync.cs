using System;
using System.IO;
using System.Linq;
using Game.Levels;
using Game.View;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Keeps <c>Assets/Levels/LevelList.asset</c> (<see cref="LevelList"/>) equal to the level files on disk, in play
    /// order (<see cref="LevelOrder"/>). Runs by itself after Unity imports, moves or deletes any .json under
    /// <c>Assets/Levels</c>, before every Android build, from "Bazaar Sort/Sync Level List", and from the scene builder.
    /// An Edit Mode test fails if the committed asset is stale.
    /// </summary>
    public static class LevelListSync
    {
        public const string LevelsRoot = "Assets/Levels";
        public const string AssetPath = "Assets/Levels/LevelList.asset";

        /// <summary>Level file paths under <see cref="LevelsRoot"/>, in play order.</summary>
        public static string[] FindLevelPaths()
        {
            if (!Directory.Exists(LevelsRoot)) return Array.Empty<string>();
            var files = Directory.GetFiles(LevelsRoot, "*.json", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'));
            return LevelOrder.Sort(files).ToArray();
        }

        [MenuItem("Bazaar Sort/Sync Level List")]
        public static void SyncMenu() => Sync();

        /// <summary>Creates or updates the asset. Returns it; logs when it changed.</summary>
        public static LevelList Sync()
        {
            var paths = FindLevelPaths();
            var levels = new TextAsset[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                levels[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(paths[i]);
                if (levels[i] == null)
                {
                    Debug.LogError($"LevelListSync: {paths[i]} is not imported as a text asset.");
                    return null;
                }
            }

            var list = AssetDatabase.LoadAssetAtPath<LevelList>(AssetPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<LevelList>();
                list.Assign(levels);
                AssetDatabase.CreateAsset(list, AssetPath);
                AssetDatabase.SaveAssets();
                return list;
            }

            if (list.Levels.SequenceEqual(levels)) return list;
            list.Assign(levels);
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssetIfDirty(list);
            Debug.Log($"LevelListSync: {AssetPath} now lists {levels.Length} levels.");
            return list;
        }

        /// <summary>Runs <see cref="Sync"/> once the import that touched a level file has finished.</summary>
        private sealed class Watcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (!Touches(imported) && !Touches(deleted) && !Touches(moved) && !Touches(movedFrom)) return;
                EditorApplication.delayCall -= DelayedSync;
                EditorApplication.delayCall += DelayedSync;
            }

            private static bool Touches(string[] paths)
            {
                foreach (var path in paths)
                {
                    if (path.StartsWith(LevelsRoot + "/", StringComparison.Ordinal)
                        && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }

            private static void DelayedSync()
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Sync();
            }
        }
    }
}
