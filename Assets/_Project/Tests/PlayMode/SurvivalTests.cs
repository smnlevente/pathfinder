using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Random;
using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Phase = Pathfinder.Game.Run.SurvivalController.Phase;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Survival (GDD §3.2): the clock, upgrades every minute, guardians every five, and how it ends.</summary>
    public class SurvivalTests
    {
        SurvivalController _survival;
        CombatWorld _world;
        WaveDirector _director;
        EquipmentChoiceScreen _choice;
        PlayerShip _player;
        string _savePath;

        SurvivalConfig Config => _survival.Config;

        // Survival endings bank into the meta save. Never touch the real profile from tests.
        [SetUp]
        public void UseTemporarySave()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta-test-{Guid.NewGuid():N}.json");
            MetaProgression.SavePath = _savePath;
        }

        [TearDown]
        public void DeleteTemporarySave()
        {
            MetaProgression.SavePath = null;
            if (File.Exists(_savePath))
                File.Delete(_savePath);
        }

        IEnumerator LoadSurvival(ulong seed = 31)
        {
            GameClock.ResumeAll();
            RunSession.Clear();
            CombatWorld.NextSeedOverride = new Seed(seed);
            yield return SceneManager.LoadSceneAsync(SceneNames.Survival, LoadSceneMode.Single);
            yield return null;

            _survival = Object.FindAnyObjectByType<SurvivalController>();
            _world = Object.FindAnyObjectByType<CombatWorld>();
            _director = Object.FindAnyObjectByType<WaveDirector>();
            _choice = Object.FindAnyObjectByType<EquipmentChoiceScreen>();
            Assert.IsNotNull(_survival, "The Survival scene has a Survival controller.");
            _player = CombatRegistry.Players[0];
            _player.CommandSource = null;
            _player.GodMode = true; // these tests are about the mode, not the fight
        }

        static IEnumerator WaitTicks(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                yield return new WaitForFixedUpdate();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        /// <summary>Hits every enemy, guardian parts included, until the guardian falls: a stand-in for a perfect pilot.</summary>
        IEnumerator SlayGuardian()
        {
            for (int i = 0; i < 900 && _survival.Guardian != null; i++)
            {
                foreach (var enemy in new List<Enemy>(CombatRegistry.Enemies))
                {
                    if (!enemy.IsAlive)
                        continue;
                    enemy.Armoured = false;
                    enemy.ApplyDamage(new DamageInfo(1_000_000f, Team.Player, 0, enemy.Position, Vector2.up));
                }
                yield return new WaitForFixedUpdate();
            }
            Assert.IsNull(_survival.Guardian, "The guardian falls.");
        }

        /// <summary>Moves the clock to the next guardian and waits for it to arrive.</summary>
        IEnumerator SummonGuardian()
        {
            _survival.DebugAdvance(Config.GuardianInterval);
            yield return WaitUntil(() => _survival.State == Phase.Guardian, 6f);
            Assert.AreEqual(Phase.Guardian, _survival.State, "The guardian arrives after its warning.");
        }

        [UnityTest]
        public IEnumerator Start_RunsTheClockAndTheWaves()
        {
            yield return LoadSurvival();
            Assert.AreEqual(Phase.Fighting, _survival.State);
            Assert.AreEqual(0, _director.Wave, "The first wave waits a moment.");

            float clock = _survival.Clock;
            yield return WaitTicks(190);
            Assert.AreEqual(190f / 60f, _survival.Clock - clock, 0.02f, "The clock runs with the fight.");
            Assert.AreEqual(1, _director.Wave, "The first wave comes after 2 s.");
            Assert.AreEqual(Config.Budget(_survival.Clock), _director.FixedBudget, "The wave budget follows the clock.");
            Assert.AreEqual(0, _director.GalaxyIndex, "The waves start with the Shattered Expanse.");
            StringAssert.Contains(SurvivalController.FormatClock(_survival.Clock), _survival.Header);
        }

        [UnityTest]
        public IEnumerator Pressure_GrowsWithTheClock()
        {
            yield return LoadSurvival();
            int budget = _director.FixedBudget;
            float health = _world.EnemyHealthMultiplier;
            float elites = _director.FixedEliteChance;

            float clock = _survival.Clock;
            _survival.DebugAdvance(240f);
            Assert.AreEqual(clock + 240f, _survival.Clock, 0.01f);
            Assert.Greater(_director.FixedBudget, budget * 2, "Four minutes in, waves are much bigger.");
            Assert.Greater(_world.EnemyHealthMultiplier, health, "Enemies are tougher.");
            Assert.AreEqual(Config.EnemyHealthAt(_survival.Clock), _world.EnemyHealthMultiplier, 1e-4f);
            Assert.Greater(_director.FixedEliteChance, elites, "Elites are more likely.");
            Assert.AreEqual(4 * Config.DarkMatterPerMinute[0], _survival.Run.DarkMatter, "Every minute on the clock pays Dark Matter.");
            Assert.AreEqual(0, _survival.UpgradesOffered, "A debug skip skips the upgrades on the way.");

            var enemy = _world.SpawnEnemy(_director.Config.Enemies[0].Enemy, new Vector2(10f, 10f));
            Assert.AreEqual(_director.Config.Enemies[0].Enemy.MaxHealth * _world.EnemyHealthMultiplier, enemy.Vitals.MaxHull, 0.01f,
                "New enemies spawn with the clock's health.");
        }

        [UnityTest]
        public IEnumerator Upgrade_ComesEveryMinute_AndSkippingRepairs()
        {
            yield return LoadSurvival();
            _player.DrainHull(50f);
            _survival.DebugAdvance(Config.UpgradeInterval - 0.25f);
            yield return WaitUntil(() => _choice.IsOpen, 3f);

            Assert.IsTrue(_choice.IsOpen, "An equipment choice comes every minute.");
            Assert.AreEqual(1, _survival.UpgradesOffered);
            Assert.AreEqual(Config.UpgradeChoices + _player.Definition.ExtraEquipmentChoices, _choice.Options.Count);
            Assert.IsTrue(GameClock.Paused, "The fight holds while choosing.");

            float hull = _player.Vitals.Hull;
            _choice.Skip();
            Assert.AreEqual(Mathf.Min(_player.Vitals.MaxHull, hull + _player.Vitals.MaxHull * Config.SkipRepairFraction), _player.Vitals.Hull, 0.01f,
                "Skipping repairs the hull: there's no Shop to spend Scraps in.");
            Assert.AreEqual(2f * Config.UpgradeInterval, _survival.NextUpgradeTime, 0.01f, "The next one is a minute later.");
        }

        [UnityTest]
        public IEnumerator Upgrade_TakingAnItemEquipsIt()
        {
            yield return LoadSurvival();
            _survival.DebugAdvance(Config.UpgradeInterval - 0.25f);
            yield return WaitUntil(() => _choice.IsOpen, 3f);

            var item = _choice.Options[0];
            _choice.Choose(0);
            if (_choice.IsSwapping)
                _choice.Swap(0);
            Assert.IsFalse(_choice.IsOpen);
            Assert.IsNotNull(_player.Equipment.Find(item), "The chosen item is equipped.");
        }

        [UnityTest]
        public IEnumerator Guardian_HoldsTheClock_AndItsFallMovesTheWavesOn()
        {
            yield return LoadSurvival();
            _survival.DebugAdvance(Config.GuardianInterval);
            yield return WaitTicks(2);
            Assert.AreEqual(Phase.Warning, _survival.State, "A guardian is announced before it comes.");
            StringAssert.Contains("QUARRY MAW", _survival.Banner);

            yield return WaitUntil(() => _survival.State == Phase.Guardian, 6f);
            var guardian = _survival.Guardian;
            Assert.AreEqual("boss.quarry-maw", guardian.Definition.Id, "The first guardian is the Quarry Maw.");
            Assert.AreEqual(5500f, guardian.Vitals.MaxHull, 1f, "It has its galaxy 1 health, whatever the clock has reached.");
            Assert.AreEqual(Config.GuardianBudgetMultiplier, _director.BudgetMultiplier, "The waves thin out while it's alive.");
            Assert.AreEqual(Loc.T("boss.quarry-maw.hint"), _survival.Prompt, "The fight opens with a hint on how to win it.");

            float clock = _survival.Clock;
            yield return WaitTicks(120);
            Assert.AreEqual(clock, _survival.Clock, "The clock holds while the guardian lives.");

            float hull = _player.Vitals.Hull;
            yield return SlayGuardian();
            Assert.AreEqual(Phase.Fighting, _survival.State);
            Assert.AreEqual(1, _survival.GuardiansDefeated);
            Assert.AreEqual(1, _survival.Run.SingularityCores, "The Maw pays a Singularity Core.");
            Assert.AreEqual(1, _director.GalaxyIndex, "The waves move on to the Graveyard Drift.");
            Assert.AreEqual(1f, _director.BudgetMultiplier, "Full waves are back.");
            Assert.GreaterOrEqual(_player.Vitals.Hull, hull);

            yield return WaitUntil(() => _choice.IsOpen, 3f);
            Assert.IsTrue(_choice.IsOpen, "The guardian's reward follows.");
            Assert.That(_choice.Options.All(o => o.Rarity >= Rarity.Rare), "Guardian rewards are Rare or better.");
            _choice.Skip();

            yield return WaitTicks(60);
            Assert.Greater(_survival.Clock, clock, "The clock runs again.");
            Assert.AreEqual(Config.GuardianInterval + Config.UpgradeInterval, _survival.NextUpgradeTime, 0.01f,
                "The upgrade due with the guardian is replaced by its reward.");
        }

        [UnityTest]
        public IEnumerator SecondGuardian_IsTheBinaryWardens_AtGalaxyTwoHealth()
        {
            yield return LoadSurvival();
            yield return SummonGuardian();
            yield return SlayGuardian();
            yield return WaitUntil(() => _choice.IsOpen, 3f);
            _choice.Skip();

            yield return SummonGuardian();
            yield return WaitTicks(3);
            Assert.AreEqual("boss.binary-wardens", _survival.Guardian.Definition.Id);
            Assert.AreEqual(2200f * 1.6f, _survival.Guardian.Vitals.MaxHull, 1f, "The Wardens have their galaxy 2 health.");
            Assert.AreEqual(2f * Config.GuardianInterval, _survival.Clock, 0.01f);
        }

        [UnityTest]
        public IEnumerator ThirdGuardian_WinsAndBanks()
        {
            yield return LoadSurvival();
            for (int i = 0; i < SurvivalController.GuardianCount; i++)
            {
                yield return SummonGuardian();
                yield return SlayGuardian();
                if (i < SurvivalController.GuardianCount - 1)
                {
                    yield return WaitUntil(() => _choice.IsOpen, 3f);
                    _choice.Skip();
                }
            }

            Assert.AreEqual(Phase.Won, _survival.State, "The Hive Matriarch's fall wins.");
            Assert.IsTrue(Object.FindAnyObjectByType<RunFlow>().IsVictory);
            Assert.IsFalse(_director.IsRunning, "The waves stop.");
            yield return WaitTicks(2);
            Assert.AreEqual(0, CombatRegistry.Enemies.Count, "The Swarm dies with her.");

            Assert.IsTrue(_survival.BankedRewards.HasValue);
            Assert.AreEqual(1 + 2 + 3, _survival.BankedRewards.Value.SingularityCores, "Each guardian pays its galaxy's cores.");
            Assert.AreEqual(Mathf.FloorToInt(3f * Config.GuardianInterval), MetaProgression.Current.BestSurvivalSeconds, "The best time is kept.");
            Assert.IsTrue(_survival.IsNewBest);
        }

        [UnityTest]
        public IEnumerator Death_EndsAndBanksTheTime()
        {
            yield return LoadSurvival();
            _survival.DebugAdvance(125f);
            _player.GodMode = false;
            _player.ApplyDamage(new DamageInfo(10_000f, Team.Enemy, 0, _player.Position, Vector2.up));
            yield return WaitTicks(2);

            Assert.AreEqual(Phase.Lost, _survival.State);
            Assert.IsTrue(Object.FindAnyObjectByType<RunFlow>().IsGameOver);
            Assert.IsTrue(_survival.BankedRewards.HasValue);
            Assert.AreEqual(2 * Config.DarkMatterPerMinute[0], _survival.BankedRewards.Value.DarkMatter, "Two minutes of Dark Matter.");
            Assert.AreEqual(125, MetaProgression.Current.BestSurvivalSeconds);

            float clock = _survival.Clock;
            yield return WaitTicks(30);
            Assert.AreEqual(clock, _survival.Clock, "The clock stops with the ship.");
        }

        [UnityTest]
        public IEnumerator Restart_KeepsTheLoadout()
        {
            GameClock.ResumeAll();
            var loadout = new Pathfinder.Core.Run.PilotLoadout("ship.titan", "primary.plasma-cutter", "secondary.nova-pulse");
            var run = RunSession.Begin(loadout: loadout);
            CombatWorld.NextSeedOverride = run.Seed;
            yield return SceneManager.LoadSceneAsync(SceneNames.Survival, LoadSceneMode.Single);
            yield return null;
            var seed = RunSession.Current.Seed;

            Object.FindAnyObjectByType<RunFlow>().Restart();
            yield return null;
            yield return null;
            Assert.AreEqual(SceneNames.Survival, SceneManager.GetActiveScene().name);
            Assert.AreEqual(loadout.ShipId, RunSession.Current.Loadout.ShipId, "Trying again keeps the Hub's loadout.");
            Assert.AreNotEqual(seed, RunSession.Current.Seed, "with a fresh seed.");
            Assert.AreEqual(loadout.ShipId, CombatRegistry.Players[0].Definition.Id);
        }

        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureSurvival()
        {
            string directory = Screenshots.Directory();
            yield return LoadSurvival(32);
            yield return SummonGuardian();
            yield return SlayGuardian();
            yield return WaitUntil(() => _choice.IsOpen, 3f);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "survival_reward.png"));
            _choice.Skip();

            // Six minutes in: the Graveyard Drift's waves around the ship.
            _survival.DebugAdvance(62f); // past the 6:00 upgrade
            yield return WaitUntil(() => CombatRegistry.Enemies.Count >= 8, 20f);
            yield return WaitTicks(60);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "survival_waves.png"));
        }
    }
}
