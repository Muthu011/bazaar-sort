using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.UI;
using Game.View;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Issue #57 in the real Game scene (built by GameSceneBuilder): the tutorial director is wired, L1 starts with
    /// its tutorial on a fresh save, the HUD title follows real level loads (L2, L11), and the whole frame is measured
    /// with the L1 hand animating ("[#57 frame]" log, same approach as the issue #21 measurement). Progress is a fake
    /// installed in <see cref="GameServices"/>, so the real save is never touched.
    /// </summary>
    public class TutorialGameScenePlayModeTests
    {
        private const string SceneName = "Game";
        private const double FrameBudgetMs = 16.6;

        private ProgressService _progress;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var settings = new SettingsService(new FakeKeyValueStore());
            _progress = new ProgressService(new FakeProgressStore());
            GameServices.Install(settings, new Haptics(new FakeHaptics(), settings), _progress);
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null; // scene loads
            yield return null; // Start: bootstrap loads the continue level
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var empty = SceneManager.CreateScene("Tutorial Test Empty");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(SceneName);
            GameServices.ResetToDefaults();
        }

        [UnityTest]
        public IEnumerator GameScene_L1StartsWithItsTutorial_AndHudTitlesFollowRealLoads()
        {
            var bootstrap = Object.FindAnyObjectByType<LevelBootstrap>();
            var tutorial = Object.FindAnyObjectByType<TutorialDirector>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            Assert.IsNotNull(bootstrap);
            Assert.IsNotNull(tutorial, "The Game scene has the tutorial director.");
            Assert.IsNotNull(hud);

            Assert.AreEqual("w1_l001", bootstrap.CurrentLevelName, "Fresh save continues at L1.");
            Assert.IsNotNull(tutorial.Guide);
            Assert.AreEqual(TutorialKind.Match3, tutorial.Guide.Kind);
            Assert.AreEqual("Level 1", hud.LevelText);

            Assert.IsTrue(bootstrap.LoadLevel(1));
            yield return null;
            Assert.AreEqual("w1_l002", bootstrap.CurrentLevelName);
            Assert.AreEqual("Level 2", hud.LevelText, "HUD shows L2's number on a real load.");
            Assert.AreEqual(TutorialKind.Layers, tutorial.Guide.Kind);
            Assert.IsTrue(tutorial.IsArrowVisible);

            Assert.IsTrue(bootstrap.LoadLevel(10));
            yield return null;
            Assert.AreEqual("w1_l011", bootstrap.CurrentLevelName);
            Assert.AreEqual("Level 11", hud.LevelText, "HUD shows L11's number on a real load.");
            Assert.AreEqual(TutorialKind.Locked, tutorial.Guide.Kind);
            Assert.AreEqual(TutorialDirector.LockedLine(1), tutorial.CueText);
        }

        [UnityTest]
        public IEnumerator GameScene_FrameTime_WithTheL1TutorialActive()
        {
            var tutorial = Object.FindAnyObjectByType<TutorialDirector>();
            Assert.IsNotNull(tutorial.Guide, "L1 tutorial is running.");

            var frameMs = new List<double>(400);
            var loopMs = new List<double>(400);
            var gc = new List<long>(400);
            using (var loop = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PlayerLoop", 1))
            using (var alloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                // Three full hand loops (2 s each at 60 fps).
                for (int frame = 0; frame < 363; frame++)
                {
                    yield return null;
                    if (frame < 3) continue;
                    Assert.IsTrue(tutorial.Guide != null && tutorial.Guide.IsActive, "Tutorial stays active while measuring.");
                    frameMs.Add(Time.unscaledDeltaTime * 1000.0);
                    if (loop.Valid) loopMs.Add(loop.LastValue / 1e6);
                    if (alloc.Valid) gc.Add(alloc.LastValue);
                }
            }

            Debug.Log($"[#57 frame] Game scene, w1_l001 with the match3 tutorial, editor play mode: frame interval " +
                $"{Stats(frameMs)} ms; PlayerLoop {Stats(loopMs)} ms; GC Allocated In Frame (whole editor frame) " +
                $"{StatsLong(gc)} bytes");
            Assert.Greater(frameMs.Count, 300);
            if (loopMs.Count > 0) Assert.Less(Mean(loopMs), FrameBudgetMs, "Mean PlayerLoop is inside the 60 fps budget.");
        }

        private static double Mean(List<double> values)
        {
            double sum = 0;
            foreach (var v in values) sum += v;
            return sum / values.Count;
        }

        private static string Stats(List<double> values)
        {
            if (values.Count == 0) return "n/a";
            var sorted = new List<double>(values);
            sorted.Sort();
            return $"n={sorted.Count} mean={Mean(sorted):F2} p95={sorted[(int)(sorted.Count * 0.95)]:F2} max={sorted[sorted.Count - 1]:F2}";
        }

        private static string StatsLong(List<long> values)
        {
            if (values.Count == 0) return "n/a";
            long max = 0;
            int nonZero = 0;
            foreach (var v in values)
            {
                if (v > max) max = v;
                if (v > 0) nonZero++;
            }
            return $"n={values.Count} frames with allocation={nonZero} max={max}";
        }
    }
}
