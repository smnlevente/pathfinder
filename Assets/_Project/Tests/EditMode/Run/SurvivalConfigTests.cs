using NUnit.Framework;
using Pathfinder.Game.Run;
using UnityEditor;
using UnityEngine;

namespace Pathfinder.Tests.Run
{
    /// <summary>How Survival's pressure follows the clock (GDD §3.2).</summary>
    public class SurvivalConfigTests
    {
        const string ConfigPath = "Assets/_Project/Data/Run/SurvivalConfig.asset";

        [Test]
        public void EnemyHealth_FollowsTheListedSteps_AndKeepsGrowingAfterThem()
        {
            var config = ScriptableObject.CreateInstance<SurvivalConfig>();

            Assert.AreEqual(1f, config.EnemyHealthAt(0f), 1e-4f);
            Assert.AreEqual(1.3f, config.EnemyHealthAt(150f), 1e-4f, "Halfway to the first guardian.");
            Assert.AreEqual(1.6f, config.EnemyHealthAt(300f), 1e-4f, "Galaxy 2 health when the Wardens' waves begin.");
            Assert.AreEqual(2.4f, config.EnemyHealthAt(600f), 1e-4f);
            Assert.AreEqual(3.2f, config.EnemyHealthAt(900f), 1e-4f);
            Assert.AreEqual(4.0f, config.EnemyHealthAt(1200f), 1e-4f, "Past the last step it keeps the last step's slope.");
            Object.DestroyImmediate(config);
        }

        [Test]
        public void BudgetAndElites_GrowWithTheClock_ElitesUpToTheirCap()
        {
            var config = ScriptableObject.CreateInstance<SurvivalConfig>();

            Assert.AreEqual(6, config.Budget(0f));
            Assert.AreEqual(27, config.Budget(600f), "6 × (1 + 0.35 × 10)");
            Assert.Greater(config.Budget(900f), config.Budget(600f));
            Assert.AreEqual(config.EliteChanceBase, config.EliteChance(0f), 1e-4f);
            Assert.AreEqual(config.EliteChanceMax, config.EliteChance(60f * 60f), 1e-4f, "Elites stop growing at their cap.");
            Object.DestroyImmediate(config);
        }

        [Test]
        public void ShippedConfig_GivesAnUpgradeEveryMinuteAndAGuardianEveryFive()
        {
            var config = AssetDatabase.LoadAssetAtPath<SurvivalConfig>(ConfigPath);
            Assert.IsNotNull(config, "Build Content creates the Survival config.");
            Assert.AreEqual(60f, config.UpgradeInterval);
            Assert.AreEqual(300f, config.GuardianInterval);
        }
    }
}
