using Game.View;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="ScreenDensity"/>: dpi / 160, with a fallback when the device reports no dpi.</summary>
    public class ScreenDensityTests
    {
        [TestCase(160f, 1f)]
        [TestCase(320f, 2f)]
        [TestCase(420f, 2.625f)]
        [TestCase(480f, 3f)]
        public void FromDpi_IsDpiOver160(float dpi, float expected)
        {
            Assert.AreEqual(expected, ScreenDensity.FromDpi(dpi, 1080, 1920), 0.0001f);
        }

        [TestCase(720, 1280, 2f)]
        [TestCase(1080, 2400, 3f)]
        [TestCase(2400, 1080, 3f)]
        public void FromDpi_Zero_FallsBackToShortSideOver360Dp(int width, int height, float expected)
        {
            Assert.AreEqual(expected, ScreenDensity.FromDpi(0f, width, height), 0.0001f);
        }

        [Test]
        public void FromDpi_Zero_OnTinyScreen_IsNeverBelowOne()
        {
            Assert.AreEqual(1f, ScreenDensity.FromDpi(0f, 200, 300), 0.0001f);
        }

        [Test]
        public void FromDpi_Negative_FallsBack()
        {
            Assert.AreEqual(2f, ScreenDensity.FromDpi(-1f, 720, 1280), 0.0001f);
        }
    }
}
