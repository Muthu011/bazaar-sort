using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Star rule and coin rule (issue #44).</summary>
    public class WinRewardsTests
    {
        // ---- Stars: boundaries ----

        [TestCase(4, 4, 3, TestName = "Stars_AtTheMinimum_Are3")]
        [TestCase(4, 6, 3, TestName = "Stars_AtExactly1_5x_Are3")]
        [TestCase(4, 7, 2, TestName = "Stars_OneOver1_5x_Are2")]
        [TestCase(4, 10, 2, TestName = "Stars_AtExactly2_5x_Are2")]
        [TestCase(4, 11, 1, TestName = "Stars_OneOver2_5x_Are1")]
        [TestCase(4, 100, 1, TestName = "Stars_FarOver_Are1")]
        public void Stars_FollowTheThresholds(int minMoves, int moves, int expected)
        {
            Assert.AreEqual(expected, WinRewards.StarsFor(moves, minMoves));
        }

        [Test]
        public void Stars_NonIntegerThresholds_AreComparedAsRealNumbers()
        {
            // minMoves 5: 3 stars up to 7.5, 2 stars up to 12.5.
            Assert.AreEqual(3, WinRewards.StarsFor(7, 5), "7 <= 7.5");
            Assert.AreEqual(2, WinRewards.StarsFor(8, 5), "8 > 7.5");
            Assert.AreEqual(2, WinRewards.StarsFor(12, 5), "12 <= 12.5");
            Assert.AreEqual(1, WinRewards.StarsFor(13, 5), "13 > 12.5");

            // minMoves 1: 3 stars up to 1.5, 2 stars up to 2.5.
            Assert.AreEqual(3, WinRewards.StarsFor(1, 1));
            Assert.AreEqual(2, WinRewards.StarsFor(2, 1));
            Assert.AreEqual(1, WinRewards.StarsFor(3, 1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Stars_WithNoKnownMinimum_AreMax(int minMoves)
        {
            Assert.AreEqual(WinRewards.MaxStars, WinRewards.StarsFor(1000, minMoves));
        }

        [Test]
        public void Stars_FewerMovesThanTheMinimum_Are3()
        {
            Assert.AreEqual(3, WinRewards.StarsFor(0, 4));
            Assert.AreEqual(3, WinRewards.StarsFor(-5, 4));
        }

        [Test]
        public void Stars_HugeNumbers_DoNotOverflow()
        {
            Assert.AreEqual(3, WinRewards.StarsFor(int.MaxValue, int.MaxValue));
            Assert.AreEqual(1, WinRewards.StarsFor(int.MaxValue, 1));
        }

        [Test]
        public void Stars_AreAlwaysOneToThree()
        {
            for (int min = 0; min <= 30; min++)
            {
                for (int moves = 0; moves <= 100; moves++)
                {
                    int stars = WinRewards.StarsFor(moves, min);
                    Assert.That(stars, Is.InRange(1, 3), $"min {min}, moves {moves}");
                }
            }
        }

        [Test]
        public void Stars_NeverRiseWithMoreMoves()
        {
            for (int min = 1; min <= 30; min++)
            {
                for (int moves = 1; moves <= 100; moves++)
                {
                    Assert.LessOrEqual(WinRewards.StarsFor(moves + 1, min), WinRewards.StarsFor(moves, min));
                }
            }
        }

        // ---- Coins: first win (Muthu, PR #63) ----

        [TestCase(1, 10)]
        [TestCase(2, 15)]
        [TestCase(3, 20)]
        public void FirstWin_Pays10PlusFivePerStarAbove1(int stars, int expected)
        {
            Assert.AreEqual(expected, WinRewards.CoinsForWin(stars, 0));
        }

        [Test]
        public void FirstWin_ClampsOutOfRangeStars()
        {
            Assert.AreEqual(10, WinRewards.CoinsForWin(0, 0));
            Assert.AreEqual(20, WinRewards.CoinsForWin(9, 0));
            Assert.AreEqual(10, WinRewards.CoinsForWin(1, -2), "A negative previous best is a first win.");
        }

        // ---- Coins: replays ----

        [TestCase(1, 1, 0, TestName = "Replay_SameStars_1_Pays0")]
        [TestCase(2, 2, 0, TestName = "Replay_SameStars_2_Pays0")]
        [TestCase(3, 3, 0, TestName = "Replay_SameStars_3_Pays0")]
        [TestCase(3, 1, 0, TestName = "Replay_Fewer_3then1_Pays0")]
        [TestCase(3, 2, 0, TestName = "Replay_Fewer_3then2_Pays0")]
        [TestCase(2, 1, 0, TestName = "Replay_Fewer_2then1_Pays0")]
        [TestCase(1, 2, 5, TestName = "Replay_More_1then2_Pays5")]
        [TestCase(2, 3, 5, TestName = "Replay_More_2then3_Pays5")]
        [TestCase(1, 3, 10, TestName = "Replay_More_1then3_Pays10")]
        public void Replay_PaysFivePerStarAboveThePreviousBest(int previousBest, int stars, int expected)
        {
            Assert.AreEqual(expected, WinRewards.CoinsForWin(stars, previousBest));
        }

        [Test]
        public void Replay_ClampsOutOfRangeStars()
        {
            Assert.AreEqual(10, WinRewards.CoinsForWin(9, 1), "9 stars count as 3.");
            Assert.AreEqual(0, WinRewards.CoinsForWin(3, 9), "A previous best over 3 counts as 3.");
            Assert.AreEqual(0, WinRewards.CoinsForWin(0, 1), "0 stars count as 1.");
        }

        [Test]
        public void OneStarFirstWin_ThenThreeStarReplay_TotalsTheSameAsAThreeStarFirstWin()
        {
            int total = WinRewards.CoinsForWin(1, 0) + WinRewards.CoinsForWin(3, 1);
            Assert.AreEqual(20, total);
            Assert.AreEqual(WinRewards.CoinsForWin(3, 0), total);
        }

        [Test]
        public void OneToThreeInSteps_TotalsTheSameAsAThreeStarFirstWin()
        {
            int first = WinRewards.CoinsForWin(1, 0);
            int second = WinRewards.CoinsForWin(2, 1);
            int third = WinRewards.CoinsForWin(3, 2);
            Assert.AreEqual(new[] { 10, 5, 5 }, new[] { first, second, third });
            Assert.AreEqual(20, first + second + third);
        }

        // ---- Coins: config ----

        [Test]
        public void DefaultConfig_IsTen_AndFivePerExtraStar()
        {
            Assert.AreEqual(10, RewardConfig.Default.FirstWinCoins);
            Assert.AreEqual(5, RewardConfig.Default.CoinsPerExtraStar);
            Assert.AreEqual(RewardConfig.DefaultFirstWinCoins, RewardConfig.Default.FirstWinCoins);
            Assert.AreEqual(RewardConfig.DefaultCoinsPerExtraStar, RewardConfig.Default.CoinsPerExtraStar);
        }

        [Test]
        public void Coins_ComeFromTheConfig_NotLiterals()
        {
            var config = new RewardConfig(25, 7);
            Assert.AreEqual(25, WinRewards.CoinsForWin(1, 0, config), "First-win base from the config.");
            Assert.AreEqual(25 + 14, WinRewards.CoinsForWin(3, 0, config), "Extra stars from the config.");
            Assert.AreEqual(7, WinRewards.CoinsForWin(2, 1, config), "Replay steps from the config.");
            Assert.AreEqual(0, WinRewards.CoinsForWin(2, 2, config));
            Assert.AreEqual(WinRewards.CoinsForWin(3, 0, RewardConfig.Default), WinRewards.CoinsForWin(3, 0), "Null means Default.");
        }

        [Test]
        public void Config_RefusesNegativeCoins()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardConfig(-1, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardConfig(10, -1));
        }

        [Test]
        public void CanAfford_CoversExactPrice_NotOneMore()
        {
            Assert.IsTrue(WinRewards.CanAfford(50, 50));
            Assert.IsTrue(WinRewards.CanAfford(50, 0));
            Assert.IsFalse(WinRewards.CanAfford(49, 50));
            Assert.IsFalse(WinRewards.CanAfford(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WinRewards.CanAfford(10, -1));
        }

        [Test]
        public void AddCoins_SaturatesInsteadOfOverflowing()
        {
            Assert.AreEqual(30, WinRewards.AddCoins(10, 20));
            Assert.AreEqual(int.MaxValue, WinRewards.AddCoins(int.MaxValue - 5, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => WinRewards.AddCoins(10, -1));
        }
    }
}
