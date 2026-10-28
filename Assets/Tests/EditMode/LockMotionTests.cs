using Game.View;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class LockMotionTests
    {
        [Test]
        public void Tick_StartsAndEndsAtRestScale_PeaksInTheMiddle()
        {
            Assert.AreEqual(1f, LockMotion.TickScale(0f));
            Assert.AreEqual(1f, LockMotion.TickScale(LockMotion.TickSeconds));
            Assert.AreEqual(1f, LockMotion.TickScale(5f));
            Assert.AreEqual(LockMotion.TickPeakScale, LockMotion.TickScale(LockMotion.TickSeconds * 0.5f), 1e-4f);
            Assert.Greater(LockMotion.TickScale(LockMotion.TickSeconds * 0.25f), 1f);
        }

        [Test]
        public void Pop_GrowsAndFadesOut()
        {
            Assert.AreEqual(1f, LockMotion.PopScale(0f));
            Assert.AreEqual(1f, LockMotion.PopAlpha(0f));
            float half = LockMotion.PopSeconds * 0.5f;
            Assert.Greater(LockMotion.PopScale(half), 1f);
            Assert.Less(LockMotion.PopScale(half), LockMotion.PopEndScale);
            Assert.AreEqual(0.5f, LockMotion.PopAlpha(half), 1e-4f);
            Assert.AreEqual(LockMotion.PopEndScale, LockMotion.PopScale(LockMotion.PopSeconds));
            Assert.AreEqual(0f, LockMotion.PopAlpha(LockMotion.PopSeconds));
        }

        [Test]
        public void Pop_IsMonotonic()
        {
            float lastScale = 1f;
            float lastAlpha = 1f;
            for (int i = 1; i <= 20; i++)
            {
                float t = LockMotion.PopSeconds * i / 20f;
                Assert.GreaterOrEqual(LockMotion.PopScale(t), lastScale);
                Assert.LessOrEqual(LockMotion.PopAlpha(t), lastAlpha);
                lastScale = LockMotion.PopScale(t);
                lastAlpha = LockMotion.PopAlpha(t);
            }
        }

        [Test]
        public void Durations_StayUnderStyleGuideLimit()
        {
            Assert.Less(LockMotion.TickSeconds, 0.6f);
            Assert.Less(LockMotion.PopSeconds, 0.6f);
        }
    }
}
