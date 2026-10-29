using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DG.Tweening;
using Game.Core;
using Game.Levels;
using Game.UI;
using Game.View;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Key frames and a short PNG frame sequence of the issue #21 game feel, for QA (no recorder package). Must run in
    /// Play Mode (DOTween only ticks there): builds its own board, effects, HUD and result panels, freezes game time,
    /// and steps every clock by hand (board <see cref="BoardView.AdvanceAnimations"/>, DOTween's update, particle
    /// simulation) so each frame is exact and repeatable. Call <see cref="CaptureAll"/> through Unity MCP while playing.
    /// </summary>
    public static class GameFeelCapture
    {
        private const float Frame = 1f / 60f;
        private const float Density = 3f;

        public static List<string> CaptureAll(string outputDir)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("GameFeelCapture runs in Play Mode.");
            Tweens.Init();
            Directory.CreateDirectory(outputDir);
            var written = new List<string>();
            float previousScale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                // 1. Pick-up: w1_l001, s2's tumbler picked, early in the squash.
                using (var rig = new Rig(1080, 1920, Load("Assets/Levels/World1/w1_l001.json")))
                {
                    rig.View.ShowSelection(1, 1);
                    rig.Step(0.03f);
                    written.Add(rig.Save(Path.Combine(outputDir, "1_pickup_1080x1920.png")));
                }

                // 2. Mid-arc: the tumbler flying from s2 onto s1.
                using (var rig = new Rig(1080, 1920, Load("Assets/Levels/World1/w1_l001.json")))
                {
                    rig.View.ShowSelection(1, 1);
                    rig.Step(FeelMotion.PickUpSeconds);
                    rig.View.Board.TryMove(1, 1, 0);
                    rig.View.ClearSelection();
                    rig.Step(FeelMotion.ArcSeconds * 0.45f);
                    written.Add(rig.Save(Path.Combine(outputDir, "2_mid_arc_1080x1920.png")));

                    // 3. Clear burst (issue #21 file names kept; since PR #63 a clear flies no coin).
                    rig.Step(FeelMotion.ArcSeconds * 0.55f + ClearRevealMotion.ClearPopSeconds + 0.12f);
                    written.Add(rig.Save(Path.Combine(outputDir, "3_clear_burst_coin_1080x1920.png")));
                    rig.Step(0.6f);
                    written.Add(rig.Save(Path.Combine(outputDir, "3b_counter_ticked_1080x1920.png")));
                }

                // 4. Win: stars mid-drop, then 5. confetti. w1_l001 won by its line (pack-1.md).
                using (var rig = new Rig(1080, 1920, Load("Assets/Levels/World1/w1_l001.json")))
                {
                    var board = rig.View.Board;
                    board.TryMove(1, 1, 0);
                    rig.Step(0.8f);
                    board.TryMove(2, 2, 1);
                    rig.Step(0.8f);
                    board.TryMove(3, 0, 2);
                    rig.StepUntilOutcome();
                    rig.Step(UiFeel.StarStart(1) + UiFeel.StarDropSeconds + 0.04f);
                    written.Add(rig.Save(Path.Combine(outputDir, "4_star_drop_1080x1920.png")));
                    rig.Step(UiFeel.StarsSeconds + 0.45f - (UiFeel.StarStart(1) + UiFeel.StarDropSeconds + 0.04f));
                    written.Add(rig.Save(Path.Combine(outputDir, "5_stars_confetti_1080x1920.png")));
                }

                // Frame sequence at 30 fps, half size: tap, arc, clear on w1_l001.
                string seqDir = Path.Combine(outputDir, "sequence");
                Directory.CreateDirectory(seqDir);
                using (var rig = new Rig(540, 960, Load("Assets/Levels/World1/w1_l001.json")))
                {
                    int n = 0;
                    rig.View.ShowSelection(1, 1);
                    for (int i = 0; i < 6; i++)
                    {
                        written.Add(rig.Save(Path.Combine(seqDir, $"move_clear_{n++:D3}.png")));
                        rig.Step(1f / 30f);
                    }
                    rig.View.Board.TryMove(1, 1, 0);
                    rig.View.ClearSelection();
                    for (int i = 0; i < 30; i++)
                    {
                        written.Add(rig.Save(Path.Combine(seqDir, $"move_clear_{n++:D3}.png")));
                        rig.Step(1f / 30f);
                    }
                }
            }
            finally
            {
                Time.timeScale = previousScale;
            }
            return written;
        }

        private static Board Load(string path)
        {
            var result = LevelLoader.Load(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
            if (!result.Success) throw new InvalidOperationException("Level failed to load: " + path);
            return result.Board;
        }

        /// <summary>A throwaway camera, board, effects, HUD and panels rendering into a texture of the given size.</summary>
        private sealed class Rig : IDisposable
        {
            private static Action<UpdateType, float, float> _tweens;

            public readonly BoardView View;
            private readonly RenderTexture _target;
            private readonly Camera _camera;
            private readonly List<GameObject> _objects = new List<GameObject>();
            private readonly ResultPanels _panels;
            private readonly int _width;
            private readonly int _height;

            public Rig(int width, int height, Board board)
            {
                _width = width;
                _height = height;
                _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                _camera = Make("Capture Camera").AddComponent<Camera>();
                _camera.targetTexture = _target;
                View = Make("Capture Board View").AddComponent<BoardView>();
                var safe = new Rect(0, 0, width, height);
                View.Bind(board, _camera, safe, BoardLayoutSettings.Default);

                var effects = Make("Capture Effects").AddComponent<BoardEffects>();
                effects.Configure(View);
                var hud = Make("Capture HUD").AddComponent<GameHud>();
                hud.Configure(View, null, _camera);
                hud.Build(_camera);
                hud.Layout(safe, Density * width / 1080f);
                _panels = Make("Capture Panels").AddComponent<ResultPanels>();
                _panels.Configure(View, null, _camera);
                _panels.Build(_camera, false);
                _panels.Layout(safe, Density * width / 1080f);

                if (_tweens == null)
                {
                    var manager = typeof(DOTween).Assembly.GetType("DG.Tweening.Core.TweenManager");
                    var method = manager.GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                        null, new[] { typeof(UpdateType), typeof(float), typeof(float) }, null);
                    _tweens = (Action<UpdateType, float, float>)Delegate.CreateDelegate(typeof(Action<UpdateType, float, float>), method);
                }
            }

            private GameObject Make(string name)
            {
                var go = new GameObject(name);
                _objects.Add(go);
                return go;
            }

            /// <summary>Advances the board, DOTween and every particle system by <paramref name="seconds"/> in 60 fps steps.</summary>
            public void Step(float seconds)
            {
                // BoardEffects builds its burst in Start; make sure it exists before the first clear.
                foreach (var go in _objects)
                {
                    var fx = go.GetComponent<BoardEffects>();
                    if (fx != null) fx.SendMessage("Start", SendMessageOptions.DontRequireReceiver);
                }
                while (seconds > 0.0001f)
                {
                    float dt = Mathf.Min(Frame, seconds);
                    View.AdvanceAnimations(dt);
                    _tweens(UpdateType.Normal, dt, dt);
                    foreach (var ps in Object.FindObjectsByType<ParticleSystem>())
                    {
                        if (ps.particleCount > 0) ps.Simulate(dt, true, false, false);
                    }
                    seconds -= dt;
                }
            }

            public void StepUntilOutcome()
            {
                for (int i = 0; i < 600 && View.Outcome == BoardOutcome.None; i++) Step(Frame);
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
                // Immediately: a deferred Destroy would leave this rig drawn into the next rig's frames.
                foreach (var go in _objects) Object.DestroyImmediate(go); // effects and panels take their particles with them
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>())
                {
                    if (ps.gameObject.name == "Clear Burst") Object.DestroyImmediate(ps.gameObject);
                }
                _target.Release();
                Object.DestroyImmediate(_target);
            }
        }
    }
}
