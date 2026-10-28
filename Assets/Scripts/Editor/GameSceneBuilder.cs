using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Levels;
using Game.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

        /// <summary>Portrait phone sizes the board is checked on: 16:9 and 20:9.</summary>
        public static readonly Vector2Int[] ScreenshotSizes = { new Vector2Int(1080, 1920), new Vector2Int(1080, 2400) };

        [MenuItem("Bazaar Sort/Build Game Scene")]
        public static void BuildGameScene()
        {
            var level = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultLevelPath);
            if (level == null)
            {
                Debug.LogError($"GameSceneBuilder: level not found at {DefaultLevelPath}.");
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
            bootstrap.Configure(level, view, camera);
            EditorUtility.SetDirty(bootstrap);

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

        /// <summary>Renders <paramref name="board"/> at <paramref name="width"/>x<paramref name="height"/> into a PNG at <paramref name="path"/>.</summary>
        public static string Capture(Board board, int width, int height, string path, System.Action<BoardView> beforeRender = null)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Screenshot Camera") { hideFlags = HideFlags.HideAndDontSave };
            var viewObject = new GameObject("Screenshot Board View") { hideFlags = HideFlags.HideAndDontSave };
            var previousActive = RenderTexture.active;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = target;
                var view = viewObject.AddComponent<BoardView>();
                view.Bind(board, camera, new Rect(0, 0, width, height), BoardLayoutSettings.Default);
                beforeRender?.Invoke(view);
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
