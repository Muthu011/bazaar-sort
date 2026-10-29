using System;
using System.Collections.Generic;
using System.IO;
using Game.Services;
using Game.UI;
using Game.View;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Screenshots and PNG frame sequences of the issue #57 tutorials (L1 hand pointer, L2 hidden-layer arrow, L11 locked
    /// line) for QA, with no recorder package. Must run in Play Mode: builds its own camera, board view, bootstrap and
    /// <see cref="TutorialDirector"/> rendering into a texture, freezes game time and steps the board and the cue by
    /// hand so every frame is exact. Moves go through the board (the input layer is covered by the Play Mode tests).
    /// Call <see cref="CaptureAll"/> through Unity MCP while playing; frames can be joined into a GIF with ffmpeg.
    /// </summary>
    public static class TutorialCapture
    {
        private const float Fps = 15f;

        public static List<string> CaptureAll(string outputDir)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("TutorialCapture runs in Play Mode.");
            Directory.CreateDirectory(outputDir);
            var written = new List<string>();
            float previousScale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                // Stills at the design resolution.
                using (var rig = new Rig(1080, 2400, "w1_l001"))
                {
                    rig.Step(0.35f); // hand faded in, on the tumbler
                    written.Add(rig.Save(Path.Combine(outputDir, "l1_hand_on_item_1080x2400.png")));
                    rig.Step(0.6f); // mid glide
                    written.Add(rig.Save(Path.Combine(outputDir, "l1_hand_gliding_1080x2400.png")));
                }
                using (var rig = new Rig(1080, 2400, "w1_l002"))
                {
                    rig.Step(0.25f);
                    written.Add(rig.Save(Path.Combine(outputDir, "l2_layer_arrow_1080x2400.png")));
                }
                using (var rig = new Rig(1080, 2400, "w1_l011"))
                {
                    rig.Step(0.1f);
                    written.Add(rig.Save(Path.Combine(outputDir, "l11_locked_line_1080x2400.png")));
                }
                using (var rig = new Rig(720, 1600, "w1_l001"))
                {
                    rig.Step(0.35f);
                    written.Add(rig.Save(Path.Combine(outputDir, "l1_hand_on_item_720x1600.png")));
                }

                // L1 sequence: two hand loops, the guided move and its clear, then free play (no cue).
                string l1 = Path.Combine(outputDir, "l1_frames");
                Directory.CreateDirectory(l1);
                using (var rig = new Rig(540, 1200, "w1_l001"))
                {
                    int frame = 0;
                    frame = rig.Record(l1, frame, 2f * TutorialCueMotion.HandPeriod);
                    rig.View.ShowSelection(1, 1);
                    frame = rig.Record(l1, frame, 0.3f);
                    rig.View.Board.TryMove(1, 1, 0);
                    rig.View.ClearSelection();
                    frame = rig.Record(l1, frame, 1.2f);
                    written.Add(l1 + " (" + frame + " frames)");
                }

                // L2 sequence: the arrow bobbing over the hidden row, then the clear that reveals it, and the arrow gone.
                string l2 = Path.Combine(outputDir, "l2_frames");
                Directory.CreateDirectory(l2);
                using (var rig = new Rig(540, 1200, "w1_l002"))
                {
                    int frame = 0;
                    frame = rig.Record(l2, frame, 2f);
                    rig.View.ShowSelection(1, 1);
                    frame = rig.Record(l2, frame, 0.3f);
                    rig.View.Board.TryMove(1, 1, 0);
                    rig.View.ClearSelection();
                    frame = rig.Record(l2, frame, 1.5f);
                    written.Add(l2 + " (" + frame + " frames)");
                }
            }
            finally
            {
                Time.timeScale = previousScale;
            }
            return written;
        }

        private sealed class Rig : IDisposable
        {
            public readonly BoardView View;
            private readonly TutorialDirector _tutorial;
            private readonly RenderTexture _target;
            private readonly Camera _camera;
            private readonly TextAsset _level;
            private readonly List<GameObject> _objects = new List<GameObject>();
            private readonly int _width;
            private readonly int _height;

            public Rig(int width, int height, string levelName)
            {
                _width = width;
                _height = height;
                _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                _camera = Make("Capture Camera").AddComponent<Camera>();
                _camera.targetTexture = _target;

                var viewObject = Make("Capture Board View");
                View = viewObject.AddComponent<BoardView>();
                View.Haptics = new FakeHaptics();
                var input = viewObject.AddComponent<BoardInput>();
                input.Configure(View);

                _level = new TextAsset(File.ReadAllText($"Assets/Levels/World1/{levelName}.json")) { name = levelName };
                var bootstrap = viewObject.AddComponent<LevelBootstrap>();
                bootstrap.Configure(new[] { _level }, View, _camera);
                bootstrap.Progress = new ProgressService(new FakeProgressStore()); // fresh save: the tutorial runs

                _tutorial = Make("Capture Tutorial").AddComponent<TutorialDirector>();
                _tutorial.Configure(bootstrap, input);
                bootstrap.LoadLevel(0);
                var safe = new Rect(0, 0, width, height);
                View.Relayout(safe);

                // The HUD, so the cue is checked against the level number and counter it shares the screen with.
                var hud = Make("Capture HUD").AddComponent<GameHud>();
                hud.Configure(View, bootstrap, _camera);
                hud.Build(_camera);
                hud.Layout(safe, 3f * width / 1080f);
            }

            private GameObject Make(string name)
            {
                var go = new GameObject(name);
                _objects.Add(go);
                return go;
            }

            public void Step(float seconds)
            {
                const float frame = 1f / 60f;
                while (seconds > 0.0001f)
                {
                    float dt = Mathf.Min(frame, seconds);
                    View.AdvanceAnimations(dt);
                    _tutorial.Advance(dt);
                    seconds -= dt;
                }
            }

            /// <summary>Saves a frame every 1/<see cref="Fps"/> s for <paramref name="seconds"/>; returns the next frame number.</summary>
            public int Record(string dir, int frame, float seconds)
            {
                int count = Mathf.RoundToInt(seconds * Fps);
                for (int i = 0; i < count; i++)
                {
                    Step(1f / Fps);
                    Save(Path.Combine(dir, $"frame_{frame:000}.png"));
                    frame++;
                }
                return frame;
            }

            public string Save(string path)
            {
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = _target;
                var image = new Texture2D(_width, _height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, _width, _height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                return path;
            }

            public void Dispose()
            {
                _camera.targetTexture = null;
                foreach (var go in _objects) Object.DestroyImmediate(go);
                Object.DestroyImmediate(_level);
                _target.Release();
                Object.DestroyImmediate(_target);
            }
        }
    }
}
