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

        /// <summary>Pixels per dp used for screenshots: a 1080 px wide phone at 360 dp.</summary>
        public const float ScreenshotDensity = 3f;

        /// <summary>Portrait phone sizes the board is checked on: 16:9 and 20:9.</summary>
        public static readonly Vector2Int[] ScreenshotSizes = { new Vector2Int(1080, 1920), new Vector2Int(1080, 2400) };

        [MenuItem("Bazaar Sort/Build Game Scene")]
        public static void BuildGameScene()
        {
            // Every level file, in play order, through the generated list (so new level files need no scene change).
            var levels = LevelListSync.Sync();
            if (levels == null || levels.Levels.Length == 0)
            {
                Debug.LogError("GameSceneBuilder: no levels found under " + LevelListSync.LevelsRoot + ".");
                return;
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
            bootstrap.Configure(levels, view, camera); // saved progress on: continue at the first unplayed level
            EditorUtility.SetDirty(bootstrap);

            var panelsObject = new GameObject("Result Panels");
            var panels = panelsObject.AddComponent<ResultPanels>();
            panels.Configure(view, bootstrap, camera);
            EditorUtility.SetDirty(panels);

            var effectsObject = new GameObject("Board Effects");
            var effects = effectsObject.AddComponent<BoardEffects>();
            effects.Configure(view);
            EditorUtility.SetDirty(effects);

            var hudObject = new GameObject("HUD");
            var hud = hudObject.AddComponent<GameHud>();
            hud.Configure(view, bootstrap, camera);
            EditorUtility.SetDirty(hud);

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
        /// Win panel at 1080x1920 and 1080x2400 and Fail panel at 1080x1920, over the board in that state, into
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

            written.AddRange(CaptureFailPanelScreenshots(outputDir));
            return written;
        }

        /// <summary>
        /// The Fail panel ("Out of space!", issue #41) over <see cref="StuckBoard"/> after its losing move, at 1080x1920
        /// and 1080x2400 (safe area as in <see cref="CaptureResultPanelScreenshots"/>), plus a 1080x1920 variant with the
        /// hidden rewarded Continue forced on, into <paramref name="outputDir"/>.
        /// </summary>
        public static List<string> CaptureFailPanelScreenshots(string outputDir)
        {
            var written = new List<string>();
            Directory.CreateDirectory(outputDir);
            foreach (var size in ScreenshotSizes)
            {
                var failed = StuckBoard();
                failed.TryMove(1, 0, 0);
                if (!failed.IsFailed)
                {
                    Debug.LogError("CaptureFailPanelScreenshots: the losing move did not fail the board.");
                    return written;
                }
                string path = Path.Combine(outputDir, $"fail_panel_{size.x}x{size.y}.png");
                written.Add(Capture(failed, size.x, size.y, path, view => view.CompleteAnimations(), ResultPanel.Fail, SafeAreaFor(size)));
            }

            // QA only: the rewarded Continue ("Free Extra Slot") is hidden in the game until Slice 6; show it here so
            // the layout can be reviewed.
            var preview = StuckBoard();
            preview.TryMove(1, 0, 0);
            var previewSize = ScreenshotSizes[0];
            string previewPath = Path.Combine(outputDir, $"fail_panel_with_continue_{previewSize.x}x{previewSize.y}.png");
            written.Add(Capture(preview, previewSize.x, previewSize.y, previewPath, view => view.CompleteAnimations(),
                ResultPanel.Fail, SafeAreaFor(previewSize), panels => panels.ShowContinueForPreview(true)));
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
        /// The dev level select (every level in the level list, development builds only) over w1_l001 at 1080x2400 with
        /// the 20:9 safe area (110 px cutout, 60 px gesture bar), into "dev_level_select_1080x2400.png". With
        /// <paramref name="open"/>, the level card is open, drawn from <paramref name="progress"/> (null: no progress).
        /// The fps readout shows "0 fps" because a still frame has no frame history.
        /// </summary>
        public static string CaptureDevLevelSelectScreenshot(string outputDir, bool open = false,
            Game.Services.IProgressService progress = null, int currentIndex = 0)
        {
            Directory.CreateDirectory(outputDir);
            var levels = LevelListSync.Sync().Levels;

            var size = ScreenshotSizes[1];
            var safe = SafeAreaFor(size);
            string path = Path.Combine(outputDir, open ? $"dev_level_select_open_{size.x}x{size.y}.png" : $"dev_level_select_{size.x}x{size.y}.png");
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
                bootstrap.Progress = progress;
                var overlay = overlayObject.AddComponent<DevOverlay>();
                overlay.Configure(bootstrap, camera);
                overlay.Build();
                if (!bootstrap.LoadLevel(currentIndex)) return null;
                view.Bind(view.Board, camera, safe, BoardLayoutSettings.Default);
                overlay.Layout(safe, ScreenshotDensity);
                if (open) overlay.Open();
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

        /// <summary>A portrait phone for issue #40 screenshots: size, density and simulated safe-area insets (px).</summary>
        public readonly struct Phone
        {
            public Phone(int width, int height, float density, int bottomInset, int topInset)
            {
                Width = width;
                Height = height;
                Density = density;
                BottomInset = bottomInset;
                TopInset = topInset;
            }

            public int Width { get; }
            public int Height { get; }
            public float Density { get; }
            public int BottomInset { get; }
            public int TopInset { get; }
            public Rect SafeArea => new Rect(0, BottomInset, Width, Height - BottomInset - TopInset);
        }

        /// <summary>
        /// The three Game view presets (issue #40) at phone density: 1080x2400 at 3 px/dp with a 110 px camera cutout and
        /// a 63 px gesture bar; 1080x1920 at 3 px/dp full screen (no cutout, the game hides the status bar); 720x1600 at
        /// 2 px/dp with a 74 px cutout and a 42 px gesture bar. Same insets as the Edit and Play Mode layout tests.
        /// </summary>
        public static readonly Phone[] PortraitPhones =
        {
            new Phone(1080, 2400, 3f, 63, 110),
            new Phone(1080, 1920, 3f, 0, 0),
            new Phone(720, 1600, 2f, 42, 74),
        };

        /// <summary>
        /// Issue #40: the full portrait screen (board, top and bottom bars, dev Levels button and fps readout) for each
        /// level index in <paramref name="levelIndices"/> (0-based in the level list) on each of <see cref="PortraitPhones"/>,
        /// into "&lt;level&gt;_&lt;w&gt;x&lt;h&gt;.png", plus "settings_open_1080x2400.png" on the first level. The simulated
        /// cutout and gesture bar are drawn as dark strips with a camera dot and a home line, so the insets can be seen.
        /// The fps readout reads "0 fps" because a still frame has no frame history. Returns the written paths.
        /// </summary>
        public static List<string> CapturePortraitScreenshots(string outputDir, params int[] levelIndices)
        {
            Directory.CreateDirectory(outputDir);
            var written = new List<string>();
            var levels = LevelListSync.Sync().Levels;
            foreach (int index in levelIndices)
            {
                foreach (var phone in PortraitPhones)
                {
                    string path = Path.Combine(outputDir, $"{levels[index].name}_{phone.Width}x{phone.Height}.png");
                    var result = CapturePortrait(levels, index, phone, false, path);
                    if (result != null) written.Add(result);
                }
            }
            if (levelIndices.Length > 0)
            {
                var phone = PortraitPhones[0];
                string path = Path.Combine(outputDir, $"settings_open_{phone.Width}x{phone.Height}.png");
                var result = CapturePortrait(levels, levelIndices[0], phone, true, path);
                if (result != null) written.Add(result);
            }
            return written;
        }

        private static string CapturePortrait(TextAsset[] levels, int index, Phone phone, bool settingsOpen, string path)
        {
            var safe = phone.SafeArea;
            var target = new RenderTexture(phone.Width, phone.Height, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Screenshot Camera") { hideFlags = HideFlags.HideAndDontSave };
            var viewObject = new GameObject("Screenshot Board View") { hideFlags = HideFlags.HideAndDontSave };
            var hudObject = new GameObject("Screenshot HUD") { hideFlags = HideFlags.HideAndDontSave };
            var overlayObject = new GameObject("Screenshot Dev Overlay") { hideFlags = HideFlags.HideAndDontSave };
            var previousActive = RenderTexture.active;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = target;
                var view = viewObject.AddComponent<BoardView>();
                var bootstrap = viewObject.AddComponent<LevelBootstrap>();
                bootstrap.Configure(levels, view, camera);
                if (!bootstrap.LoadLevel(index)) return null;
                view.Bind(view.Board, camera, safe, BoardLayoutSettings.Default);

                var hud = hudObject.AddComponent<GameHud>();
                hud.Configure(view, bootstrap, camera);
                hud.Build(camera);
                hud.Layout(safe, phone.Density);
                hud.ShowLevel(index + 1);
                if (settingsOpen) hud.OpenSettings();

                var overlay = overlayObject.AddComponent<DevOverlay>();
                overlay.FpsEnabled = DevBuild.IsFpsReadoutEnabled(true); // a development build, like the editor
                overlay.Configure(bootstrap, camera);
                overlay.Build();
                overlay.Layout(safe, phone.Density);

                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(phone.Width, phone.Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, phone.Width, phone.Height), 0, 0);
                DrawInsets(image, phone);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                return path;
            }
            finally
            {
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(overlayObject);
                Object.DestroyImmediate(hudObject);
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>Darkens the simulated cutout and gesture-bar insets and draws a camera dot and a home line.</summary>
        private static void DrawInsets(Texture2D image, Phone phone)
        {
            var strip = new Color(0.05f, 0.05f, 0.06f);
            var line = new Color(0.85f, 0.85f, 0.85f);
            int w = phone.Width;
            int h = phone.Height;
            float unit = phone.Density;
            for (int y = 0; y < phone.BottomInset; y++)
                for (int x = 0; x < w; x++) image.SetPixel(x, y, strip);
            for (int y = h - phone.TopInset; y < h; y++)
                for (int x = 0; x < w; x++) image.SetPixel(x, y, strip);

            if (phone.TopInset > 0)
            {
                // Punch-hole camera, 12 dp across, centred in the cutout strip.
                float r = 6f * unit;
                var c = new Vector2(w * 0.5f, h - phone.TopInset * 0.5f);
                for (int y = (int)(c.y - r); y <= (int)(c.y + r); y++)
                    for (int x = (int)(c.x - r); x <= (int)(c.x + r); x++)
                        if ((new Vector2(x, y) - c).sqrMagnitude <= r * r) image.SetPixel(x, y, new Color(0.2f, 0.22f, 0.3f));
            }
            if (phone.BottomInset > 0)
            {
                // Gesture (home) line, 108 x 4 dp.
                int half = (int)(54f * unit);
                int thick = (int)(2f * unit);
                int cy = phone.BottomInset / 2;
                for (int y = cy - thick; y < cy + thick; y++)
                    for (int x = w / 2 - half; x < w / 2 + half; x++) image.SetPixel(x, y, line);
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
            ResultPanel panel = ResultPanel.None, Rect? safeArea = null, System.Action<ResultPanels> panelSetup = null)
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
                    panelSetup?.Invoke(panels);
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
