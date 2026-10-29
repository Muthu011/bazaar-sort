using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Portrait phone sizes for the Game view (issue #40): 1080x2400 (20:9), 1080x1920 (16:9) and 720x1600. Game view
    /// sizes live in the user's editor prefs, not the project, so they are added from code: once on editor load and from
    /// the menu "Bazaar Sort/Add Phone Game View Sizes". Adding is idempotent (a size with the same width and height is
    /// never added twice). Unity has no public API for this, so it goes through UnityEditor.GameViewSizes by reflection;
    /// if Unity changes those internals, this logs one warning and does nothing.
    /// </summary>
    [InitializeOnLoad]
    public static class GameViewPresets
    {
        public static readonly (string Name, int Width, int Height)[] Sizes =
        {
            ("Phone 20:9", 1080, 2400),
            ("Phone 16:9", 1080, 1920),
            ("Phone 720p 20:9", 720, 1600),
        };

        private const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static GameViewPresets()
        {
            EditorApplication.delayCall += () => AddAll();
        }

        [MenuItem("Bazaar Sort/Add Phone Game View Sizes")]
        public static void AddFromMenu()
        {
            int added = AddAll();
            Debug.Log($"GameViewPresets: {added} phone size(s) added to the Game view ({Sizes.Length} in the list).");
        }

        /// <summary>Adds every missing size to the Android and Standalone groups. Returns how many were added.</summary>
        public static int AddAll()
        {
            try
            {
                int added = 0;
                foreach (var group in new[] { GameViewSizeGroupType.Android, GameViewSizeGroupType.Standalone })
                {
                    foreach (var size in Sizes)
                    {
                        if (IndexOf(group, size.Width, size.Height) >= 0) continue;
                        AddFixed(group, size.Name, size.Width, size.Height);
                        added++;
                    }
                }
                if (added > 0) Save();
                return added;
            }
            catch (Exception e)
            {
                Debug.LogWarning("GameViewPresets: could not add Game view sizes (" + e.GetType().Name + ": " + e.Message + ").");
                return 0;
            }
        }

        /// <summary>Index of the fixed-resolution size <paramref name="width"/> x <paramref name="height"/> in the group, or -1.</summary>
        public static int IndexOf(GameViewSizeGroupType groupType, int width, int height)
        {
            var group = Group(groupType);
            var groupClass = group.GetType();
            int total = (int)groupClass.GetMethod("GetTotalCount", Any).Invoke(group, null);
            var getSize = groupClass.GetMethod("GetGameViewSize", Any);
            for (int i = 0; i < total; i++)
            {
                var size = getSize.Invoke(group, new object[] { i });
                var t = size.GetType();
                if ((int)t.GetProperty("width", Any).GetValue(size, null) == width
                    && (int)t.GetProperty("height", Any).GetValue(size, null) == height
                    && t.GetProperty("sizeType", Any).GetValue(size, null).ToString() == "FixedResolution")
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The Game view's selected size index (current group), or -1 with no Game view open.</summary>
        public static int SelectedIndex()
        {
            var view = GameView();
            return view == null ? -1 : (int)view.GetType().GetProperty("selectedSizeIndex", Any).GetValue(view, null);
        }

        /// <summary>Selects size <paramref name="index"/> of the current group in the Game view (and restores with the old index).</summary>
        public static void Select(int index)
        {
            var view = GameView();
            if (view == null || index < 0) return;
            view.GetType().GetMethod("SizeSelectionCallback", Any).Invoke(view, new object[] { index, null });
        }

        /// <summary>Selects the phone size <paramref name="width"/> x <paramref name="height"/>; returns the previous index.</summary>
        public static int Select(int width, int height)
        {
            int previous = SelectedIndex();
            AddAll();
            Select(IndexOf(CurrentGroupType(), width, height));
            return previous;
        }

        public static GameViewSizeGroupType CurrentGroupType()
        {
            return (GameViewSizeGroupType)SizesType().GetProperty("currentGroupType", Any).GetValue(null, null);
        }

        private static Type SizesType() => typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes", true);

        private static object Instance() => SizesType().BaseType.GetProperty("instance", Any).GetValue(null, null);

        private static object Group(GameViewSizeGroupType groupType)
        {
            return SizesType().GetMethod("GetGroup", Any).Invoke(Instance(), new object[] { groupType });
        }

        private static void AddFixed(GameViewSizeGroupType groupType, string name, int width, int height)
        {
            var assembly = typeof(Editor).Assembly;
            var sizeType = assembly.GetType("UnityEditor.GameViewSizeType", true);
            var sizeClass = assembly.GetType("UnityEditor.GameViewSize", true);
            var ctor = sizeClass.GetConstructor(new[] { sizeType, typeof(int), typeof(int), typeof(string) });
            var size = ctor.Invoke(new[] { Enum.Parse(sizeType, "FixedResolution"), width, height, (object)name });
            var group = Group(groupType);
            group.GetType().GetMethod("AddCustomSize", Any).Invoke(group, new[] { size });
        }

        private static void Save() => SizesType().GetMethod("SaveToHDD", Any).Invoke(Instance(), null);

        private static EditorWindow GameView()
        {
            var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            var views = Resources.FindObjectsOfTypeAll(type);
            return views.Length == 0 ? null : (EditorWindow)views[0];
        }
    }
}
