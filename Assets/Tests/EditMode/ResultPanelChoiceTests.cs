using Game.UI;
using Game.View;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="ResultPanelChoice"/>: a win shows Win while a next level exists, and More-levels-soon after the last
    /// available level (Muthu, PR #17).
    /// </summary>
    public class ResultPanelChoiceTests
    {
        [Test]
        public void Won_WithANextLevel_ShowsWin()
        {
            Assert.AreEqual(ResultPanel.Win, ResultPanelChoice.For(BoardOutcome.Won, true));
        }

        [Test]
        public void Won_OnTheLastLevel_ShowsMoreLevelsSoon()
        {
            Assert.AreEqual(ResultPanel.MoreLevelsSoon, ResultPanelChoice.For(BoardOutcome.Won, false));
        }

        [Test]
        public void Failed_ShowsFail_OnAnyLevel([Values(true, false)] bool hasNext)
        {
            Assert.AreEqual(ResultPanel.Fail, ResultPanelChoice.For(BoardOutcome.Failed, hasNext));
        }

        [Test]
        public void None_ShowsNothing([Values(true, false)] bool hasNext)
        {
            Assert.AreEqual(ResultPanel.None, ResultPanelChoice.For(BoardOutcome.None, hasNext));
        }
    }
}
