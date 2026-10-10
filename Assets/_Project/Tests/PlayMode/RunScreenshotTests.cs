using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
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
    /// <summary>Screenshots of the run's level objects (GDD §10), the galaxy map and the boss for visual review.</summary>
    public class RunScreenshotTests
    {
        RunDirector _director;
        PlayerShip _player;

        // Run endings bank into the meta save. Never touch the real profile from tests.
        string _savePath;

        [SetUp]
        public void UseTemporarySave()
        {
            _savePath = System.IO.Path.Combine(Application.temporaryCachePath, $"meta-test-{System.Guid.NewGuid():N}.json");
            Pathfinder.Game.Meta.MetaProgression.SavePath = _savePath;
        }

        [TearDown]
        public void DeleteTemporarySave()
        {
            Pathfinder.Game.Meta.MetaProgression.SavePath = null;
            if (System.IO.File.Exists(_savePath))
                System.IO.File.Delete(_savePath);
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
            _player.CommandSource = null;
            _player.GodMode = true;
        }

        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureLevelObjects()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun(21);

            // The in-game map is IMGUI, which batch mode can't capture, so draw the generated graph instead.
            Screenshots.SaveMapPreview(_director.Run, Path.Combine(directory, "run_map.png"));

            foreach (var type in new[] { MapNodeType.DarkMatter, MapNodeType.Repair, MapNodeType.Shop })
            {
                _director.StartTestLevel(type, row: 2);
                yield return null;
                var level = _director.Level;
                // Dark Matter: stand on a node so its harvest ring fills. Stations: stand between the station and the exit.
                _player.PlaceForLevel(type == MapNodeType.DarkMatter ? level.UnharvestedNodes.First() : new Vector2(2.5f, 5f));
                for (int i = 0; i < 30; i++)
                    yield return new WaitForFixedUpdate();
                yield return null;
                Screenshots.CaptureWithUi(Path.Combine(directory, $"run_{type.ToString().ToLowerInvariant()}.png"));

                level.DebugTakeExit();
                float deadline = Time.realtimeSinceStartup + 3f;
                while (_director.State != RunDirector.Phase.Map && Time.realtimeSinceStartup < deadline)
                    yield return null;
            }
        }

        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureQuarryMaw()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun(22);
            _director.StartTestLevel(MapNodeType.Boss);
            yield return null;
            var boss = _director.Level.Boss;
            var brain = boss.GetComponent<QuarryMawBrain>();
            _player.PlaceForLevel(new Vector2(0f, -4f));

            // Surfaced and chasing: the trailing body with its yellow weak points, and the HUD with the boss bar.
            for (int i = 0; i < 100; i++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "boss_surfaced.png"));

            // Diving: maw open and half sunk into the ground, a window to shoot it.
            yield return WaitTicks(() => brain.SinkDepth > 0.45f && !brain.IsBurrowed, 600);
            yield return null;
            Screenshots.CaptureCamera(Path.Combine(directory, "boss_diving.png"));

            // Sweep: the chevron lane of an incoming charge.
            var weakPoint = brain.Segments.First(segment => segment.IsWeakPoint);
            yield return WaitTicks(() => !brain.IsBurrowed && !brain.IsRoaring, 600);
            weakPoint.ApplyDamage(new DamageInfo(boss.Vitals.Hull - boss.Vitals.MaxHull * 0.63f, Team.Player, _player.EntityId, weakPoint.Position, Vector2.up));
            var telegraph = (IChargeTelegraph)brain;
            yield return WaitTicks(() => telegraph.ShowsCharge, 600);
            for (int i = 0; i < 40; i++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Screenshots.CaptureCamera(Path.Combine(directory, "boss_sweep.png"));

            // Enraged: the cracked maw, circling the ship and spewing bullet streams.
            yield return WaitTicks(() => !brain.IsBurrowed && !brain.IsRoaring, 600);
            weakPoint.ApplyDamage(new DamageInfo(boss.Vitals.Hull - boss.Vitals.MaxHull * 0.3f, Team.Player, _player.EntityId, weakPoint.Position, Vector2.up));
            yield return WaitTicks(() => brain.CurrentPhase == QuarryMawBrain.Phase.Enrage && !brain.IsRoaring, 600);
            Assert.AreEqual(QuarryMawBrain.Phase.Enrage, brain.CurrentPhase);
            // It may start at the arena edge: wait until it has closed in to its orbit, in view, and is firing.
            yield return WaitTicks(() => Vector2.Distance(boss.Position, _player.Position) < 9.5f, 600);
            for (int i = 0; i < 40; i++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Screenshots.CaptureCamera(Path.Combine(directory, "boss_enraged.png"));
        }

        /// <summary>The Binary Wardens in galaxy 2: linked, and with one core offline, with their twin health bars.</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureBinaryWardens()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun(23);
            while (_director.Run.GalaxyIndex == 0)
            {
                yield return WaitRealtime(() => _director.State == RunDirector.Phase.Map, 5f);
                _director.ChooseNode(_director.Run.AvailableNodes[0]);
                _director.Level.DebugTakeExit();
                yield return WaitRealtime(() => _director.State != RunDirector.Phase.Level, 3f);
            }
            yield return WaitRealtime(() => _director.State == RunDirector.Phase.Map, 5f);

            _director.StartTestLevel(MapNodeType.Boss);
            yield return null;
            var alpha = _director.Level.Boss;
            var brain = alpha.GetComponent<WardenBrain>();
            _player.PlaceForLevel(new Vector2(0f, -2f)); // near the middle, where both cores are in view

            // Linked: both cores circling, the tether between them, and a health bar for each.
            yield return WaitTicks(() => brain.TetherActive, 400);
            yield return WaitTicks(() => false, 50);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "wardens_linked.png"));

            // One core offline: sunk and dark, its reboot ring filling, the HUD counting down.
            var beta = brain.TwinEnemy;
            beta.ApplyDamage(new DamageInfo(1_000_000f, Team.Player, _player.EntityId, beta.Position, Vector2.up));
            yield return WaitTicks(() => false, 100);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "wardens_offline.png"));
        }

        static IEnumerator WaitRealtime(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        /// <summary>The galaxy map at the start and mid-run, the Shop dialog and the equipment choice cards (GDD §12).</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureMenus()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun(23);
            yield return null;
            yield return null;
            var map = Object.FindAnyObjectByType<GalaxyMapScreen>();
            Assert.IsTrue(map.IsOpen);
            Assert.Greater(map.EdgeCount, 10, "Every jump lane is drawn.");
            Screenshots.CaptureWithUi(Path.Combine(directory, "ui_map.png"));

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(_director.ChooseNode(_director.Run.AvailableNodes[0]));
                _director.Level.DebugTakeExit();
                float deadline = Time.realtimeSinceStartup + 3f;
                while (_director.State != RunDirector.Phase.Map && Time.realtimeSinceStartup < deadline)
                    yield return null;
            }
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "ui_map_progress.png"));

            _director.StartTestLevel(MapNodeType.Shop, row: 4);
            yield return null;
            _player.AddScraps(150);
            _player.PlaceForLevel(new Vector2(0f, -1.5f));
            _player.CommandSource = new Interacting();
            for (int i = 0; i < 3; i++)
                yield return new WaitForFixedUpdate();
            _player.CommandSource = null;
            var dialog = Object.FindAnyObjectByType<ChoiceDialog>();
            Assert.IsTrue(dialog.IsOpen, "Interacting at the terminal opens the Shop.");
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "ui_shop.png"));
            dialog.Close();

            var choice = Object.FindAnyObjectByType<EquipmentChoiceScreen>();
            choice.Show("EQUIPMENT CACHE", _director.Level.RollEquipment(3, Pathfinder.Core.Equipment.Rarity.Rare, _player, new Pcg32(5, 5)), _player, 15, null);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "ui_choice.png"));
            choice.Skip();
        }

        sealed class Interacting : IShipCommandSource
        {
            public ShipCommand ReadCommand(PlayerShip ship) => new ShipCommand { Interact = true };
        }

        static IEnumerator WaitTicks(Func<bool> condition, int maxTicks)
        {
            for (int i = 0; i < maxTicks && !condition(); i++)
                yield return new WaitForFixedUpdate();
        }
    }
}
