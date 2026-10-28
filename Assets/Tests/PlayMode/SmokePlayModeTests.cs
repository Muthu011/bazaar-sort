using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class SmokePlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerLoop_TicksAFrame_AndCoreIsReachable()
        {
            var go = new GameObject("SmokeTestObject");
            int startFrame = Time.frameCount;

            yield return null;

            Assert.Greater(Time.frameCount, startFrame, "Player loop did not advance a frame.");
            Assert.IsTrue(go != null, "Test object was destroyed unexpectedly.");
            Assert.IsTrue(GameRules.IsMatch(GameRules.MatchCount));

            Object.Destroy(go);
        }
    }
}
