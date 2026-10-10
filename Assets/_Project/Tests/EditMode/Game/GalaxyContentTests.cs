using System.Linq;
using NUnit.Framework;
using Pathfinder.Game.Content;
using Pathfinder.Game.Run;
using UnityEditor;
using UnityEngine;

namespace Pathfinder.Tests.Game
{
    /// <summary>Which enemies and bosses each galaxy gets (GDD §7.2, §7.5).</summary>
    public class GalaxyContentTests
    {
        const string WavesPath = "Assets/_Project/Data/Waves/Waves_ShatteredExpanse.asset";
        const string RunConfigPath = "Assets/_Project/Data/Run/RunConfig.asset";

        [Test]
        public void LaterGalaxyEnemies_StayOutOfEarlierGalaxies_ExceptLateInEndlessMode()
        {
            var config = ScriptableObject.CreateInstance<WaveDirectorConfig>();
            var entry = new WaveDirectorConfig.Entry { Weight = 1f, MinWave = 2, MinGalaxy = 1 };

            Assert.AreEqual(int.MaxValue, config.FirstWave(entry, galaxyIndex: 0, endless: false), "Never in galaxy 1's levels.");
            Assert.AreEqual(2, config.FirstWave(entry, galaxyIndex: 1, endless: false), "From its own galaxy on, its usual first wave.");
            Assert.AreEqual(2, config.FirstWave(entry, galaxyIndex: 2, endless: false));
            Assert.AreEqual(config.EndlessLaterGalaxyWave, config.FirstWave(entry, galaxyIndex: 0, endless: true), "The sandbox brings it in later.");
            Object.DestroyImmediate(config);
        }

        [Test]
        public void ShippedWavePool_HasGalaxyTwoEnemiesFromGalaxyTwo()
        {
            var waves = AssetDatabase.LoadAssetAtPath<WaveDirectorConfig>(WavesPath);
            Assert.IsNotNull(waves);
            foreach (string id in new[] { "enemy.scorcher", "enemy.arc-drone", "enemy.crusher" })
            {
                var entry = waves.Enemies.SingleOrDefault(e => e.Enemy != null && e.Enemy.Id == id);
                Assert.IsNotNull(entry.Enemy, $"{id} is in the wave pool.");
                Assert.AreEqual(1, entry.MinGalaxy, $"{id} starts in galaxy 2.");
            }
            Assert.IsTrue(waves.Enemies.Where(e => e.Enemy.Id == "enemy.drone").All(e => e.MinGalaxy == 0), "The original enemies appear everywhere.");
        }

        [Test]
        public void EachGalaxy_HasItsBoss()
        {
            var run = AssetDatabase.LoadAssetAtPath<RunConfig>(RunConfigPath);
            Assert.IsNotNull(run);
            Assert.AreEqual("boss.quarry-maw", run.BossFor(0).Id);
            Assert.AreEqual("boss.binary-wardens", run.BossFor(1).Id);
            Assert.AreEqual("boss.quarry-maw", run.BossFor(2).Id, "The final galaxy brings back a tougher Quarry Maw.");
        }
    }
}
