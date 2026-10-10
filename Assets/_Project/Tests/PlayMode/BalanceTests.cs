using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>
    /// Balance probes (GDD §11.1): a god-mode bot plays real levels and logs what they pay, to check the economy against
    /// Shop and Lab prices. The assertions are loose sanity bounds; the logged numbers are what tuning looks at.
    /// </summary>
    public class BalanceTests
    {
        string _savePath;
        RunDirector _director;
        PlayerShip _player;

        [SetUp]
        public void UseTemporarySave()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta-test-{System.Guid.NewGuid():N}.json");
            MetaProgression.SavePath = _savePath;
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            MetaProgression.SavePath = null;
            if (File.Exists(_savePath))
                File.Delete(_savePath);
        }

        IEnumerator LoadRun(ulong seed)
        {
            GameClock.ResumeAll();
            RunSession.Clear();
            CombatWorld.NextSeedOverride = new Seed(seed);
            yield return SceneManager.LoadSceneAsync("Run", LoadSceneMode.Single);
            yield return null;
            _director = Object.FindAnyObjectByType<RunDirector>();
            _player = CombatRegistry.Players[0];
            _player.GodMode = true;
            _player.CommandSource = new Bot(_director);
        }

        /// <summary>Aims at and shoots the nearest enemy; flies to the Equipment cache or the nearest Dark Matter node.</summary>
        sealed class Bot : IShipCommandSource
        {
            readonly RunDirector _director;
            public Bot(RunDirector director) => _director = director;

            public ShipCommand ReadCommand(PlayerShip ship)
            {
                CombatEntity target = null;
                float best = float.MaxValue;
                foreach (var enemy in CombatRegistry.OnTeam(Team.Enemy))
                {
                    float distance = (enemy.Position - ship.Position).sqrMagnitude;
                    if (enemy.IsAlive && !enemy.Armoured && !enemy.IsIntangible && distance < best)
                    {
                        best = distance;
                        target = enemy;
                    }
                }

                var level = _director.Level;
                Vector2? goal = level.CachePosition;
                if (!goal.HasValue && level.UnharvestedNodes.Any())
                    goal = level.UnharvestedNodes.OrderBy(n => (n - ship.Position).sqrMagnitude).First();
                Vector2 move = goal.HasValue && (goal.Value - ship.Position).sqrMagnitude > 0.5f ? (goal.Value - ship.Position).normalized : Vector2.zero;

                return new ShipCommand
                {
                    Move = move,
                    Aim = target != null ? (target.Position - ship.Position).normalized : Vector2.up,
                    FirePrimary = target != null,
                    FireSecondary = target != null,
                };
            }
        }

        /// <summary>Plays one level to its exit and returns (Scraps, Dark Matter, seconds).</summary>
        IEnumerator Play(MapNodeType type, int row, System.Action<int, int, float> report)
        {
            int scraps = _player.TotalScrapsGained, darkMatter = _director.Run.DarkMatter;
            _director.StartTestLevel(type, row);
            var level = _director.Level;
            var choice = Object.FindAnyObjectByType<EquipmentChoiceScreen>();
            int start = Object.FindAnyObjectByType<CombatWorld>().Tick;

            Time.timeScale = 5f;
            float deadline = Time.realtimeSinceStartup + 120f;
            while (!level.ExitOpen || level.UnharvestedNodes.Any() && level.InstabilityRemaining > 0f)
            {
                if (choice.IsOpen)
                    choice.Skip();
                if (Time.realtimeSinceStartup > deadline)
                    break;
                yield return null;
            }
            Time.timeScale = 1f;

            float seconds = (Object.FindAnyObjectByType<CombatWorld>().Tick - start) / 60f;
            level.DebugTakeExit(); // leftover Scraps are collected on the way out
            float wait = Time.realtimeSinceStartup + 3f;
            while (_director.State != RunDirector.Phase.Map && Time.realtimeSinceStartup < wait)
                yield return null;

            report(_player.TotalScrapsGained - scraps, _director.Run.DarkMatter - darkMatter, seconds);
        }

        [UnityTest]
        public IEnumerator Economy_WhatLevelsPay()
        {
            yield return LoadRun(51);
            int combatScraps = 0;
            foreach (var type in new[] { MapNodeType.Scraps, MapNodeType.Equipment, MapNodeType.DarkMatter })
            {
                int scraps = 0, darkMatter = 0;
                float seconds = 0f;
                yield return Play(type, 2, (s, d, t) => { scraps = s; darkMatter = d; seconds = t; });
                Debug.Log($"[Balance] Galaxy 1 row 3 {type}: {scraps} Scraps, {darkMatter} Dark Matter in {seconds:F0} s");
                if (type != MapNodeType.DarkMatter)
                    combatScraps += scraps;
                else
                    Assert.Greater(darkMatter, 0, "Harvesting pays Dark Matter.");
            }

            Assert.Greater(combatScraps, 0, "Combat levels pay Scraps.");
        }
    }
}
