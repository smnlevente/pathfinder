using System.Collections;
using System.IO;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Meta progression (GDD §11): banking, Hub upgrades, unlocks and persistence.</summary>
    public class MetaTests
    {
        string _savePath;

        [SetUp]
        public void UseTemporarySave()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta-test-{System.Guid.NewGuid():N}.json");
            MetaProgression.SavePath = _savePath;
        }

        [TearDown]
        public void DeleteTemporarySave()
        {
            if (File.Exists(RunLog.FilePath))
                File.Delete(RunLog.FilePath);
            MetaProgression.SavePath = null;
            if (File.Exists(_savePath))
                File.Delete(_savePath);
        }

        static IEnumerator LoadRun(ulong seed)
        {
            GameClock.ResumeAll();
            RunSession.Clear();
            CombatWorld.NextSeedOverride = new Seed(seed);
            yield return SceneManager.LoadSceneAsync("Run", LoadSceneMode.Single);
            yield return null;
        }

        /// <summary>The database the Run scene loaded (referenced by its spawner and level controller).</summary>
        static ContentDatabase Database() => Resources.FindObjectsOfTypeAll<ContentDatabase>()[0];

        [UnityTest]
        public IEnumerator Defeat_BanksDarkMatterAndScraps()
        {
            yield return LoadRun(31);
            var director = Object.FindAnyObjectByType<RunDirector>();
            var player = CombatRegistry.Players[0];

            director.StartTestLevel(MapNodeType.DarkMatter);
            player.GodMode = true;
            foreach (var node in director.Level.UnharvestedNodes)
            {
                player.PlaceForLevel(node);
                break;
            }
            for (int i = 0; i < 75; i++)
                yield return new WaitForFixedUpdate();
            player.AddScraps(57);
            player.GodMode = false;

            player.ApplyDamage(new DamageInfo(10_000f, Team.Enemy, 0, player.Position, Vector2.up));
            yield return new WaitForFixedUpdate(); // "all players down" is detected on the simulation tick
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(director.BankedRewards.HasValue);
            var rewards = director.BankedRewards.Value;
            Assert.AreEqual(5, rewards.DarkMatter);
            Assert.AreEqual(5, rewards.ConvertedScraps, "10% of 57 Scraps.");
            Assert.AreEqual(10, MetaProgression.Current.DarkMatter);
            Assert.AreEqual(1, MetaProgression.Current.RunsPlayed);
            Assert.IsTrue(File.Exists(_savePath), "Banking saves the profile.");

            // Playtests: every finished run leaves a line in the run log, next to the save.
            var lines = File.ReadAllLines(RunLog.FilePath);
            var entry = JsonUtility.FromJson<RunLog.Entry>(lines[lines.Length - 1]);
            Assert.AreEqual(RunSeed.ToCode(new Seed(31)), entry.seed, "The log keeps the seed, so the run can be replayed.");
            Assert.IsFalse(entry.won);
            Assert.AreEqual(1, entry.galaxyReached);
            Assert.AreEqual(5, entry.darkMatter);
            Assert.IsFalse(string.IsNullOrEmpty(entry.killedBy));
        }

        [UnityTest]
        public IEnumerator Upgrades_ApplyToTheNextRun_AndPersist()
        {
            MetaProgression.Current.BankRun(darkMatter: 200, singularityCores: 2, scrapsCollected: 0, won: false, galaxyReached: 1);
            yield return LoadRun(32);
            var database = Database();
            MetaUpgradeDefinition Upgrade(string id) => System.Array.Find(database.MetaUpgrades, u => u.Id == id);

            Assert.IsTrue(MetaProgression.TryPurchase(Upgrade("meta.hull-plating")));
            Assert.IsTrue(MetaProgression.TryPurchase(Upgrade("meta.salvage-protocols")));
            Assert.IsTrue(MetaProgression.TryPurchase(Upgrade("meta.second-dash")));
            Assert.AreEqual(200 - 30 - 40, MetaProgression.Current.DarkMatter);
            Assert.AreEqual(0, MetaProgression.Current.SingularityCores);

            // Reload from disk, then start a fresh run: the bonuses apply to the new pilot.
            MetaProgression.Reload();
            yield return LoadRun(33);
            var player = CombatRegistry.Players[0];

            Assert.AreEqual(1, MetaProgression.Current.Tier("meta.hull-plating"), "Tiers persist across loads.");
            Assert.AreEqual(player.Definition.MaxHull + 10f, player.Vitals.MaxHull, 0.01f, "Hull Plating I: +10 max hull.");
            Assert.AreEqual(25, player.Scraps, "Salvage Protocols I: start with 25 Scraps.");
            Assert.AreEqual(0, player.TotalScrapsGained, "Starting Scraps aren't collected, so they don't bank back as Dark Matter.");
            Assert.AreEqual(2, player.Dash.MaxCharges, "Second Dash Charge.");
        }

        [UnityTest]
        public IEnumerator QuantumCharger_IsLockedUntilBought()
        {
            yield return LoadRun(34);
            var database = Database();
            var charger = database.FindPrimary("primary.quantum-charger");
            var unlock = System.Array.Find(database.MetaUpgrades, u => u.Id == "meta.unlock-quantum-charger");

            Assert.IsFalse(MetaProgression.IsAvailable(charger, database), "Locked by default (GDD §6.1: 150 DM).");
            Assert.IsTrue(MetaProgression.IsAvailable(database.FindPrimary("primary.photon-repeater"), database));
            Assert.IsFalse(MetaProgression.TryPurchase(unlock), "Can't afford it yet.");

            MetaProgression.Current.BankRun(150, 0, 0, false, 1);
            Assert.IsTrue(MetaProgression.TryPurchase(unlock));
            Assert.IsTrue(MetaProgression.IsAvailable(charger, database));
        }

        [UnityTest]
        public IEnumerator CorruptSave_StartsFresh()
        {
            File.WriteAllText(_savePath, "{ not json");
            MetaProgression.Reload();

            LogAssert.ignoreFailingMessages = true;
            var state = MetaProgression.Current;
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(0, state.DarkMatter);
            yield return null;
        }
    }
}
