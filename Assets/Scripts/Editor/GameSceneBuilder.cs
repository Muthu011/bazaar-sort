using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Levels;
using Game.UI;
using Game.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the placeholder game scene and captures board screenshots from code, so both are reviewable.
    /// Menu "Bazaar Sort/Build Game Scene", or call the static methods through Unity MCP.
    /// </summary>
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";
        public const string DefaultLevelPath = "Assets/Levels/World1/w1_l001.json";

        /// <summary>World 1 in play order: Next walks this list and loops after the last.</summary>
        public static readonly string[] World1LevelPaths =
        {
            "Assets/Levels/World1/w1_l001.json",
            "Assets/Levels/World1/w1_l002.json",
            "Assets/Levels/World1/w1_l003.json",
            "Assets/Levels/World1/w1_l004.json",
            "Assets/Levels/World1/w1_l005.json",
        };

        /// <summary>Pixels per dp used for screenshots: a 1080 px wide phone at 360 dp.</summary>
        public const float ScreenshotDensity = 3f;

        /// <summary>Portrait phone sizes the board is checked on: 16:9 and 20:9.</summary>
        public static readonly Vector2Int[] ScreenshotSizes = { new Vector2Int(1080, 1920), new Vector2Int(1080, 2400) };

        [MenuItem("Bazaar Sort/Build Game Scene")]
        public static void BuildGameScene()
        {
            var levels = new TextAsset[World1LevelPaths.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(World1LevelPaths[i]);
                if (levels[i] == null)
                {
                    Debug.LogError($"GameSceneBuilder: level not found at {World1LevelPaths[i]}.");
                    return;
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var viewObject = new GameObject("Board View");
            var view = viewObject.AddComponent<BoardView>();
            var input = viewObject.AddComponent<BoardInput>();
            input.Configure(view);
            EditorUtility.SetDirty(input);

            var bootstrapObject = new GameObject("Level Bootstrap");
            var bootstrap = bootstrapObject.AddComponent<LevelBootstrap>();
            bootstrap.Configure(levels, view, camera);
            EditorUtility.SetDirty(bootstrap);

            var panelsObject = new GameObject("Result Panels");
            var panels = panelsObject.AddComponent<ResultPanels>();
            panels.Configure(view, bootstrap, camera);
            EditorUtility.SetDirty(panels);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettingsFirst(ScenePath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Renders <paramref name="levelAssetPath"/> on the placeholder board at each of <see cref="ScreenshotSizes"/>
        /// and writes PNGs named "&lt;level&gt;_&lt;w&gt;x&lt;h&gt;.png" into <paramref name="outputDir"/> (relative to the project
        /// root). Uses temporary objects that are never saved. Returns the written paths.
        /// </summary>
        public static List<string> CaptureBoardScreenshots(string levelAssetPath, string outputDir)
        {
            var written = new List<string>();
            string json = File.ReadAllText(levelAssetPath);
            string levelName = Path.GetFileNameWithoutExtension(levelAssetPath);
            Directory.CreateDirectory(outputDir);

            foreach (var size in ScreenshotSizes)
            {
                var result = LevelLoader.Load(json, levelName);
                if (!result.Success)
                {
                    foreach (var error in result.Errors) Debug.LogError(error.Message);
                    return written;
                }

                written.Add(Capture(result.Board, size.x, size.y, Path.Combine(outputDir, $"{levelName}_{size.x}x{size.y}.png")));
            }
            return written;
        }

        /// <summary>
        /// Like <see cref="CaptureBoardScreenshots"/>, but with the front item at (<paramref name="shelf"/>,
        /// <paramref name="slot"/>) selected, to show the tap highlight. Files are named
        /// "&lt;level&gt;_selected_&lt;w&gt;x&lt;h&gt;.png".
        /// </summary>
        public static List<string> CaptureSelectionScreenshots(string levelAssetPath, string outputDir, int shelf, int slot)
        {
            var written = new List<string>();
            string json = File.ReadAllText(levelAssetPath);
            string levelName = Path.GetFileNameWithoutExtension(levelAssetPath);
            Directory.CreateDirectory(outputDir);

            foreach (var size in ScreenshotSizes)
            {
                var result = LevelLoader.Load(json, levelName);
                if (!result.Success)
                {
                    foreach (var error in result.Errors) Debug.LogError(error.Message);
                    return written;
                }

                string path = Path.Combine(outputDir, $"{levelName}_selected_{size.x}x{size.y}.png");
                written.Add(Capture(result.Board, size.x, size.y, path, view => view.ShowSelection(shelf, slot)));
            }
            return written;
        }

        /// <summary>
        /// Win panel at 1080x1920 and 1080x2400 and Stuck panel at 1080x1920, over the board in that state, into
        /// <paramref name="outputDir"/>. The 2400 shot uses a safe area with a 110 px top cutout and 60 px gesture bar.
        /// </summary>
        public static List<string> CaptureResultPanelScreenshots(string outputDir)
        {
            var written = new List<string>();
            Directory.CreateDirectory(outputDir);

            foreach (var size in ScreenshotSizes)
            {
                var won = LoadOrNull(DefaultLevelPath);
                if (won == null) return written;
                // w1_l001 winning line (docs: Assets/Levels/World1/pack-1.md).
                won.TryMove(1, 1, 0);
                won.TryMove(2, 2, 1);
                won.TryMove(3, 0, 2);
                var safe = SafeAreaFor(size);
                string path = Path.Combine(outputDir, $"win_panel_{size.x}x{size.y}.png");
                written.Add(Capture(won, size.x, size.y, path, view => view.CompleteAnimations(), ResultPanel.Win, safe));
            }

            var stuck = StuckBoard();
            stuck.TryMove(1, 0, 0);
            var stuckSize = ScreenshotSizes[0];
            written.Add(Capture(stuck, stuckSize.x, stuckSize.y, Path.Combine(outputDir, $"stuck_panel_{stuckSize.x}x{stuckSize.y}.png"),
                view => view.CompleteAnimations(), ResultPanel.Stuck, SafeAreaFor(stuckSize)));
            return written;
        }

        /// <summary>w1_l005, the last level, won by its line in pack-1.md: the More-levels-soon panel at 1080x1920.</summary>
        public static string CaptureMoreLevelsScreenshot(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            var won = LoadOrNull("Assets/Levels/World1/w1_l005.json");
            if (won == null) return null;
            int[] line = { 3,1,5, 0,0,1, 4,1,0, 0,0,1, 0,1,2, 1,2,2, 0,2,1, 1,0,0, 3,0,1, 0,0,2, 1,0,0, 0,0,2, 1,1,0, 0,0,1, 1,0,2, 4,0,1, 2,1,1, 3,2,2 };
            for (int i = 0; i < line.Length; i += 3) won.TryMove(line[i], line[i + 1], line[i + 2]);
            if (!won.IsWon)
            {
                Debug.LogError("CaptureMoreLevelsScreenshot: the w1_l005 line did not win.");
                return null;
            }
            var size = ScreenshotSizes[0];
            string path = Path.Combine(outputDir, $"more_levels_soon_{size.x}x{size.y}.png");
            return Capture(won, size.x, size.y, path, view => view.CompleteAnimations(), ResultPanel.MoreLevelsSoon, SafeAreaFor(size));
        }

        /// <summary>
        /// A frame part-way through the tumbler clear on w1_l001 (s2's tumbler onto s1), at the top of the pop, into
        /// "mid_clear_1080x1920.png".
        /// </summary>
        public static string CaptureMidClearScreenshot(string outputDir, float seconds)
        {
            Directory.CreateDirectory(outputDir);
            var board = LoadOrNull(DefaultLevelPath);
            if (board == null) return null;
            var size = ScreenshotSizes[0];
            string path = Path.Combine(outputDir, $"mid_clear_{size.x}x{size.y}.png");
            return Capture(board, size.x, size.y, path, view =>
            {
                board.TryMove(1, 1, 0);
                view.AdvanceAnimations(seconds);
            });
        }

        /// <summary>
        /// The dev level select (L1..L5, development builds only) over w1_l001 at 1080x2400 with the 20:9 safe area
        /// (110 px cutout, 60 px gesture bar), into "dev_level_select_1080x2400.png". The fps readout shows "0 fps"
        /// because a still frame has no frame history.
        /// </summary>
        public static string CaptureDevLevelSelectScreenshot(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            var levels = new TextAsset[World1LevelPaths.Length];
            for (int i = 0; i < levels.Length; i++) levels[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(World1LevelPaths[i]);

            var size = ScreenshotSizes[1];
            var safe = SafeAreaFor(size);
            string path = Path.Combine(outputDir, $"dev_level_select_{size.x}x{size.y}.png");
            var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Screenshot Camera") { hideFlags = HideFlags.HideAndDontSave };
            var viewObject = new GameObject("Screenshot Board View") { hideFlags = HideFlags.HideAndDontSave };
            var overlayObject = new GameObject("Screenshot Dev Overlay") { hideFlags = HideFlags.HideAndDontSave };
            var previousActive = RenderTexture.active;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = target;
                var view = viewObject.AddComponent<BoardView>();
                var bootstrap = viewObject.AddComponent<LevelBootstrap>();
                bootstrap.Configure(levels, view, camera);
                var overlay = overlayObject.AddComponent<DevOverlay>();
                overlay.Configure(bootstrap, camera);
                overlay.Build();
                if (!bootstrap.LoadLevel(0)) return null;
                view.Bind(view.Board, camera, safe, BoardLayoutSettings.Default);
                overlay.Layout(safe, ScreenshotDensity);
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                return path;
            }
            finally
            {
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(overlayObject);
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// Four shelves; moving s2's idli stand onto s1 fills s1 and reveals a full row on s2, leaving no free slot:
        /// the board is stuck. Same shape as the Play Mode stuck test level.
        /// </summary>
        public static Board StuckBoard()
        {
            return new Board(
                new Shelf(new[] { "tumbler", "tawa", null }),
                new Shelf(new[] { "idli_stand", null, null }, new[] { "masala_dabba", "tawa", "tumbler" }),
                new Shelf(new[] { "masala_dabba", "masala_dabba", "idli_stand" }),
                new Shelf(new[] { "tumbler", "tawa", "idli_stand" }));
        }

        private static Rect SafeAreaFor(Vector2Int size)
        {
            // 20:9 phones have a camera cutout and a gesture bar; 16:9 ones here are full screen.
            return size.y >= 2400 ? new Rect(0, 60, size.x, size.y - 60 - 110) : new Rect(0, 0, size.x, size.y);
        }

        private static Board LoadOrNull(string levelAssetPath)
        {
            var result = LevelLoader.Load(File.ReadAllText(levelAssetPath), Path.GetFileNameWithoutExtension(levelAssetPath));
            if (result.Success) return result.Board;
            foreach (var error in result.Errors) Debug.LogError(error.Message);
            return null;
        }

        /// <summary>Renders <paramref name="board"/> at <paramref name="width"/>x<paramref name="height"/> into a PNG at <paramref name="path"/>.</summary>
        public static string Capture(Board board, int width, int height, string path, System.Action<BoardView> beforeRender = null,
            ResultPanel panel = ResultPanel.None, Rect? safeArea = null)
        {
            var safe = safeArea ?? new Rect(0, 0, width, height);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Screenshot Camera") { hideFlags = HideFlags.HideAndDontSave };
            var viewObject = new GameObject("Screenshot Board View") { hideFlags = HideFlags.HideAndDontSave };
            var previousActive = RenderTexture.active;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = target;
                var view = viewObject.AddComponent<BoardView>();
                view.Bind(board, camera, safe, BoardLayoutSettings.Default);
                beforeRender?.Invoke(view);
                if (panel != ResultPanel.None)
                {
                    var panels = viewObject.AddComponent<ResultPanels>();
                    panels.Build(camera, false);
                    panels.Layout(safe, ScreenshotDensity);
                    panels.Show(panel);
                    Canvas.ForceUpdateCanvases();
                }
                camera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                return path;
            }
            finally
            {
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void AddSceneToBuildSettingsFirst(string path)
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path != path) scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
