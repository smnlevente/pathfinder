using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>The run structure (GDD §3, §9, §10), driven through the same API the galaxy map screen uses.</summary>
    public class RunTests
    {
        RunDirector _director;
        LevelController _level;
        CombatWorld _world;
        PlayerShip _player;

        RunState Run => _director.Run;

        IEnumerator LoadRun(ulong seed)
        {
            GameClock.ResumeAll();
            RunSession.Clear();
            CombatWorld.NextSeedOverride = new Seed(seed);
            yield return SceneManager.LoadSceneAsync("Run", LoadSceneMode.Single);
            yield return null;

            _director = Object.FindAnyObjectByType<RunDirector>();
            _level = _director.Level;
            _world = Object.FindAnyObjectByType<CombatWorld>();
            _player = CombatRegistry.Players[0];
            _player.CommandSource = null;
            _player.GodMode = false;
        }

        /// <summary>Finds a run seed whose first galaxy satisfies a condition, without loading any scene.</summary>
        static ulong FindSeed(Func<RunState, bool> condition)
        {
            for (ulong seed = 1; seed < 5000; seed++)
            {
                if (condition(new RunState(new Seed(seed))))
                    return seed;
            }
            throw new InvalidOperationException("No seed matched.");
        }

        IEnumerator WaitTicks(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                yield return new WaitForFixedUpdate();
        }

        IEnumerator WaitUntil(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        static void KillAll()
        {
            var enemies = new List<Enemy>(CombatRegistry.Enemies);
            foreach (var enemy in enemies)
                enemy.ApplyDamage(new DamageInfo(1_000_000f, Team.Player, 0, enemy.Position, Vector2.up));
        }

        /// <summary>Keeps killing whatever spawns until the condition holds, standing in for a perfect pilot.</summary>
        IEnumerator FightUntil(Func<bool> condition, float seconds)
        {
            _player.GodMode = true;
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                KillAll();
                yield return new WaitForFixedUpdate();
            }
        }

        IEnumerator TakeExit()
        {
            Assert.IsTrue(_level.ExitOpen, "The exit should be open.");
            _player.PlaceForLevel(_level.Config.ExitPosition);
            yield return WaitUntil(() => _director.State != RunDirector.Phase.Level, 3f);
        }

        [UnityTest]
        public IEnumerator Run_StartsOnTheGalaxyMap()
        {
            yield return LoadRun(11);

            Assert.AreEqual(RunDirector.Phase.Map, _director.State);
            Assert.IsTrue(GameClock.Paused, "The simulation holds still behind the map.");
            CollectionAssert.AreEqual(Run.Map.EntryNodes, Run.AvailableNodes);
            Assert.AreEqual(Run.Seed, _world.Seed, "One seed drives both the run and the arena.");
        }

        [UnityTest]
        public IEnumerator ScrapsLevel_ClearingTheWavesPaysABonusAndOpensTheExit()
        {
            ulong seed = FindSeed(run => run.Map.EntryNodes.Any(n => n.Type == MapNodeType.Scraps));
            yield return LoadRun(seed);
            var node = Run.AvailableNodes.First(n => n.Type == MapNodeType.Scraps);

            Assert.IsTrue(_director.ChooseNode(node));
            Assert.AreEqual(RunDirector.Phase.Level, _director.State);
            Assert.IsFalse(_level.ExitOpen);

            yield return FightUntil(() => _level.ExitOpen, 40f);
            Assert.IsTrue(_level.ExitOpen, "Clearing all four waves opens the exit.");
            Assert.GreaterOrEqual(_player.Scraps, 30, "The completion bonus is at least 30 Scraps in galaxy 1.");

            yield return TakeExit();
            Assert.AreEqual(RunDirector.Phase.Map, _director.State);
            Assert.AreEqual(node, Run.CurrentNode);
            Assert.AreEqual(1, Run.LevelsCleared);
            CollectionAssert.AreEqual(node.Next, Run.AvailableNodes);
        }

        [UnityTest]
        public IEnumerator EquipmentLevel_CacheCarrierDropsTheCacheThatCompletesTheLevel()
        {
            yield return LoadRun(12);
            var node = Run.AvailableNodes.First(n => n.Type == MapNodeType.Equipment);
            _director.ChooseNode(node);

            yield return FightUntil(() => _level.CachePosition.HasValue, 40f);
            Assert.IsTrue(_level.CachePosition.HasValue, "The final wave's Cache Carrier drops the cache.");
            Assert.IsFalse(_level.ExitOpen, "The exit stays shut until the cache is collected.");

            int scrapsBefore = _player.Scraps;
            _player.PlaceForLevel(_level.CachePosition.Value);
            yield return WaitTicks(2);
            Assert.IsTrue(_level.CacheCollected, "Touching the cache collects it.");
            Assert.Greater(_player.Scraps, scrapsBefore, "Until M4 the cache pays Scraps.");

            // The exit also needs the final wave cleared.
            yield return FightUntil(() => _level.ExitOpen, 20f);
            Assert.IsTrue(_level.ExitOpen, "Cache collected and waves cleared: the exit opens.");
        }

        [UnityTest]
        public IEnumerator DarkMatterLevel_HarvestingANodeBanksDarkMatter()
        {
            yield return LoadRun(13);
            _director.StartTestLevel(MapNodeType.DarkMatter);

            Assert.IsTrue(_level.ExitOpen, "The exit is available from the start.");
            Assert.That(_level.DarkMatterNodes, Is.InRange(8, 12));
            float instability = _level.InstabilityRemaining;

            _player.GodMode = true;
            _player.PlaceForLevel(_level.UnharvestedNodes.First());
            yield return WaitTicks(75);

            Assert.AreEqual(1, _level.DarkMatterHarvested);
            Assert.AreEqual(5, Run.DarkMatter, "5 Dark Matter per node in galaxy 1.");
            Assert.Less(_level.InstabilityRemaining, instability, "The instability timer counts down.");

            yield return TakeExit();
            Assert.AreEqual(RunDirector.Phase.Map, _director.State);
            Assert.AreEqual(0, CombatRegistry.Enemies.Count, "Leaving clears the arena.");
        }

        [UnityTest]
        public IEnumerator RepairLevel_RepairsOrReinforcesOnce()
        {
            yield return LoadRun(14);
            _director.StartTestLevel(MapNodeType.Repair, row: 2);
            yield return WaitTicks(1);

            _player.ApplyDamage(new DamageInfo(_player.Vitals.MaxShield + 60f, Team.Enemy, 0, _player.Position, Vector2.up));
            float hull = _player.Vitals.Hull;

            _level.ApplyRepair(_player, LevelController.RepairChoice.Repair);
            Assert.AreEqual(hull + _player.Vitals.MaxHull * 0.4f, _player.Vitals.Hull, 0.01f, "Repair restores 40% of max hull.");

            float maxHull = _player.Vitals.MaxHull;
            _level.ApplyRepair(_player, LevelController.RepairChoice.Reinforce);
            Assert.AreEqual(maxHull, _player.Vitals.MaxHull, "The station works only once.");
        }

        [UnityTest]
        public IEnumerator RepairLevel_ReinforceRaisesMaxHull()
        {
            yield return LoadRun(15);
            _director.StartTestLevel(MapNodeType.Repair, row: 2);
            float maxHull = _player.Vitals.MaxHull;

            _level.ApplyRepair(_player, LevelController.RepairChoice.Reinforce);

            Assert.AreEqual(maxHull + 15f, _player.Vitals.MaxHull, 0.01f);
        }

        [UnityTest]
        public IEnumerator Shop_SellsRepairsForScraps()
        {
            yield return LoadRun(16);
            _director.StartTestLevel(MapNodeType.Shop, row: 4);
            yield return WaitTicks(1);
            _player.AddScraps(30);
            _player.ApplyDamage(new DamageInfo(_player.Vitals.MaxShield + 20f, Team.Enemy, 0, _player.Position, Vector2.up));

            Assert.IsTrue(_level.BuyRepair(_player, 20));
            Assert.AreEqual(10, _player.Scraps);
            Assert.AreEqual(_player.Vitals.MaxHull, _player.Vitals.Hull, 0.01f);
            Assert.IsFalse(_level.BuyRepair(_player, 25), "Not enough Scraps left.");
            Assert.AreEqual(10, _player.Scraps);
        }

        [UnityTest]
        public IEnumerator Boss_DefeatPaysCoresAndJumpsToTheNextGalaxy()
        {
            yield return LoadRun(17);

            // Skip through the galaxy to the boss.
            while (true)
            {
                var next = Run.AvailableNodes[0];
                Assert.IsTrue(_director.ChooseNode(next));
                if (next.Type == MapNodeType.Boss)
                    break;
                _level.DebugTakeExit();
                yield return WaitUntil(() => _director.State == RunDirector.Phase.Map, 3f);
            }

            Assert.IsNotNull(_level.Boss, "The boss level spawns its boss.");
            Assert.AreEqual(150f * 3f * 2f, _level.Boss.Vitals.MaxHull, 1f, "Placeholder boss: Carrier ×3, Armored ×2.");

            _player.GodMode = true;
            _level.Boss.ApplyDamage(new DamageInfo(1_000_000f, Team.Player, 0, _level.Boss.Position, Vector2.up));
            yield return WaitTicks(1);
            Assert.AreEqual(1, Run.SingularityCores);
            Assert.IsTrue(_level.ExitOpen);

            yield return TakeExit();
            Assert.AreEqual(RunDirector.Phase.GalaxyCleared, _director.State);
            yield return WaitUntil(() => _director.State == RunDirector.Phase.Map, 5f);
            Assert.AreEqual(1, Run.GalaxyIndex);
            CollectionAssert.AreEqual(Run.Map.EntryNodes, Run.AvailableNodes);
        }

        [UnityTest]
        public IEnumerator ThirdBoss_WinsTheRun()
        {
            yield return LoadRun(18);

            while (!Run.IsWon)
            {
                yield return WaitUntil(() => _director.State == RunDirector.Phase.Map, 5f);
                Assert.IsTrue(_director.ChooseNode(Run.AvailableNodes[0]));
                _level.DebugTakeExit();
                yield return WaitUntil(() => _director.State != RunDirector.Phase.Level, 3f);
            }

            Assert.AreEqual(RunDirector.Phase.Victory, _director.State);
            Assert.IsTrue(Object.FindAnyObjectByType<RunFlow>().IsVictory);
        }

        [UnityTest]
        public IEnumerator Death_EndsTheRun()
        {
            yield return LoadRun(19);
            _director.ChooseNode(Run.AvailableNodes[0]);

            _player.ApplyDamage(new DamageInfo(10_000f, Team.Enemy, 0, _player.Position, Vector2.up));
            yield return WaitTicks(2);

            Assert.AreEqual(RunDirector.Phase.Defeat, _director.State);
            Assert.IsTrue(Run.IsLost);
            Assert.IsTrue(Object.FindAnyObjectByType<RunFlow>().IsGameOver);
        }

        [UnityTest]
        public IEnumerator LaterGalaxies_HaveTougherEnemies()
        {
            yield return LoadRun(20);
            while (Run.GalaxyIndex == 0)
            {
                yield return WaitUntil(() => _director.State == RunDirector.Phase.Map, 5f);
                _director.ChooseNode(Run.AvailableNodes[0]);
                _level.DebugTakeExit();
                yield return WaitUntil(() => _director.State != RunDirector.Phase.Level, 3f);
            }
            yield return WaitUntil(() => _director.State == RunDirector.Phase.Map, 5f);

            _director.StartTestLevel(MapNodeType.Scraps);
            yield return WaitUntil(() => CombatRegistry.Enemies.Any(e => !e.IsElite), 8f);

            Assert.AreEqual(1.6f, _world.EnemyHealthMultiplier, 1e-4f, "Galaxy 2 enemies have ×1.6 health (GDD §7.3).");
            var enemy = CombatRegistry.Enemies.First(e => !e.IsElite);
            Assert.AreEqual(enemy.Definition.MaxHealth * 1.6f, enemy.Vitals.MaxHull, 0.01f);
        }
    }
}
