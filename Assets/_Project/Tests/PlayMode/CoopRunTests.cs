using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>A local co-op run (M8): two pilots through levels, a boss, the repair station and a save.</summary>
    public class CoopRunTests
    {
        RunDirector _director;
        LevelController _level;
        Gamepad _pad;
        string _savePath;

        RunState Run => _director.Run;
        static PlayerShip Pilot(int index) => CombatRegistry.Players.First(p => p.PlayerIndex == index);

        [SetUp]
        public void JoinAndUseTemporarySave()
        {
            _savePath = System.IO.Path.Combine(Application.temporaryCachePath, $"meta-test-{Guid.NewGuid():N}.json");
            Pathfinder.Game.Meta.MetaProgression.SavePath = _savePath;
            Pathfinder.Game.Settings.GameSettings.ResetToDefaults(); // everyone in their default colour
            LocalPlayers.Reset();
            _pad = InputSystem.AddDevice<Gamepad>();
            Assert.AreEqual(1, LocalPlayers.Join(_pad));
        }

        [TearDown]
        public void LeaveAndDeleteSave()
        {
            Pathfinder.Game.Settings.GameSettings.ResetToDefaults(); // forget colours picked here (in memory only, in tests)
            LocalPlayers.Reset();
            if (_pad != null && _pad.added)
                InputSystem.RemoveDevice(_pad);
            string run = RunSaveFile.FilePath;
            Pathfinder.Game.Meta.MetaProgression.SavePath = null;
            foreach (string path in new[] { _savePath, run })
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
        }

        IEnumerator LoadRun(ulong seed, bool resume = false)
        {
            GameClock.ResumeAll();
            if (!resume)
                RunSession.Clear();
            CombatWorld.NextSeedOverride = resume ? RunSession.Current.Seed : new Seed(seed);
            yield return SceneManager.LoadSceneAsync("Run", LoadSceneMode.Single);
            yield return null;

            _director = Object.FindAnyObjectByType<RunDirector>();
            _level = _director.Level;
            Assert.AreEqual(2, CombatRegistry.Players.Count, "Both players fly.");
            foreach (var pilot in CombatRegistry.Players)
            {
                pilot.CommandSource = null;
                pilot.GodMode = false;
            }
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

        static void Kill(CombatEntity entity) =>
            entity.ApplyDamage(new DamageInfo(1_000_000f, entity.Team.Opponent(), 0, entity.Position, Vector2.up));

        [UnityTest]
        public IEnumerator ABoss_BringsEveryoneBack_AndEachPilotPicksTheirReward()
        {
            yield return LoadRun(17);
            _director.StartTestLevel(MapNodeType.Boss);
            yield return WaitTicks(3);
            var one = Pilot(0);
            var two = Pilot(1);
            Assert.Greater(Mathf.Abs(one.Position.x - two.Position.x), 2f, "They arrive side by side.");
            Assert.AreEqual(5500f * CoopRules.EnemyHealthScale(2), _level.Boss.Vitals.MaxHull, 1f, "The boss is tougher for two.");

            Kill(two);
            yield return WaitTicks(2);
            Assert.IsFalse(two.IsAlive);

            var brain = _level.Boss.GetComponent<Pathfinder.Game.Enemies.QuarryMawBrain>();
            var weakPoint = brain.Segments.First(s => s.IsWeakPoint);
            weakPoint.Armoured = false;
            weakPoint.ApplyDamage(new DamageInfo(1_000_000f, Team.Player, 0, weakPoint.Position, Vector2.up));
            yield return WaitTicks(5);
            Assert.IsTrue(two.IsAlive, "A downed pilot is back when the boss falls.");
            Assert.AreEqual(two.Vitals.MaxHull, two.Vitals.Hull, 0.5f, "And patched up with everyone.");

            var reward = Object.FindAnyObjectByType<EquipmentChoiceScreen>();
            yield return WaitUntil(() => reward.IsOpen, 3f);
            Assert.IsTrue(reward.IsOpen);
            Assert.AreSame(one, reward.Pilot, "Player 1 picks first.");
            var first = reward.Options[0];
            reward.Choose(0);
            if (reward.IsSwapping)
                reward.Swap(0);
            Assert.IsTrue(reward.IsOpen, "Then it's player 2's turn.");
            Assert.AreSame(two, reward.Pilot);
            reward.Skip();
            Assert.IsFalse(reward.IsOpen);
            Assert.IsNotNull(one.Equipment.Find(first), "Player 1's pick went to player 1.");
            Assert.IsTrue(one.IsPulled && two.IsPulled, "Then the wormhole takes them both.");
        }

        [UnityTest]
        public IEnumerator ARepairStation_ServesEachPilotOnce()
        {
            yield return LoadRun(21);
            _director.StartTestLevel(MapNodeType.Repair, row: 2);
            var one = Pilot(0);
            var two = Pilot(1);
            float before = one.Vitals.MaxHull;
            _level.ApplyRepair(one, LevelController.RepairChoice.Reinforce);
            _level.ApplyRepair(one, LevelController.RepairChoice.Reinforce);
            Assert.AreEqual(before + 15f, one.Vitals.MaxHull, 0.01f, "Once for player 1.");
            float twoBefore = two.Vitals.MaxHull;
            _level.ApplyRepair(two, LevelController.RepairChoice.Reinforce);
            Assert.AreEqual(twoBefore + 15f, two.Vitals.MaxHull, 0.01f, "And once for player 2.");
            yield return null;
        }

        sealed class InteractOnce : IShipCommandSource
        {
            bool _pressed;

            public ShipCommand ReadCommand(PlayerShip ship)
            {
                var command = new ShipCommand { Interact = !_pressed };
                _pressed = true;
                return command;
            }
        }

        [UnityTest]
        public IEnumerator PlayerTwosShop_IsTheirsAlone()
        {
            // Playtest 8: "I have to tell everyone to stop interacting, I'm shopping."
            yield return LoadRun(21);
            _director.StartTestLevel(MapNodeType.Shop, row: 4);
            var one = Pilot(0);
            var two = Pilot(1);
            two.AddScraps(500);
            two.PlaceForLevel(Vector2.zero); // the terminal stands in the middle of the arena
            two.CommandSource = new InteractOnce();
            var dialog = Object.FindAnyObjectByType<ChoiceDialog>();
            yield return WaitUntil(() => dialog.IsOpen, 2f);
            Assert.IsTrue(dialog.IsOpen, "Player 2 opens the Shop.");
            Assert.AreSame(two, dialog.Owner, "It's player 2's.");
            Assert.IsTrue(Pathfinder.Game.Presentation.Ui.MenuMouse.Blocked, "Player 1's mouse can't work it.");

            int scraps = two.Scraps;
            var pointer = dialog.Rows[0].Pointer;
            pointer.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            pointer.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left });
            Assert.AreEqual(scraps, two.Scraps, "A click from player 1's mouse buys nothing.");
            Assert.IsTrue(dialog.IsOpen);
            dialog.Close();
            Assert.IsFalse(Pathfinder.Game.Presentation.Ui.MenuMouse.Blocked, "Closed, the mouse works again.");

            // Player 1 plays with the keyboard and mouse: their own Shop takes clicks.
            one.PlaceForLevel(Vector2.zero);
            one.CommandSource = new InteractOnce();
            yield return WaitUntil(() => dialog.IsOpen, 2f);
            Assert.AreSame(one, dialog.Owner);
            Assert.IsFalse(Pathfinder.Game.Presentation.Ui.MenuMouse.Blocked, "Player 1's Shop takes their mouse.");
            dialog.Close();
        }

        /// <summary>Player 2's Shop, for visual review.</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureCoopShop()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun(21);
            _director.StartTestLevel(MapNodeType.Shop, row: 4);
            var two = Pilot(1);
            two.AddScraps(300);
            two.PlaceForLevel(Vector2.zero);
            two.CommandSource = new InteractOnce();
            var dialog = Object.FindAnyObjectByType<ChoiceDialog>();
            yield return WaitUntil(() => dialog.IsOpen, 2f);
            yield return null;
            yield return null;
            Screenshots.CaptureWithUi(System.IO.Path.Combine(directory, "coop_shop.png"));
            dialog.Close();
        }

        [UnityTest]
        public IEnumerator ADownedPilot_ComesBackForTheNextLevel()
        {
            yield return LoadRun(21);
            _director.StartTestLevel(MapNodeType.Scraps);
            yield return WaitTicks(2);
            var two = Pilot(1);
            Kill(two);
            yield return WaitTicks(2);
            Assert.IsFalse(two.IsAlive);
            Assert.AreEqual(RunDirector.Phase.Level, _director.State, "The run goes on.");

            _level.DebugTakeExit();
            yield return WaitUntil(() => _director.State == RunDirector.Phase.Map, 3f);
            Assert.IsTrue(_director.ChooseNode(Run.AvailableNodes[0]));
            yield return WaitTicks(2);
            Assert.IsTrue(two.IsAlive, "Back for the next level.");
            Assert.AreEqual(two.Vitals.MaxHull * CoopRules.ReviveHullFraction, two.Vitals.Hull, 0.5f, "With the revive's bit of hull.");
        }

        [UnityTest]
        public IEnumerator TheSave_KeepsBothPilots_AndContinuingPutsThemBack()
        {
            yield return LoadRun(33);
            var one = Pilot(0);
            var two = Pilot(1);
            var item = _level.RollEquipment(1, Pathfinder.Core.Equipment.Rarity.Common, two, new Pcg32(5UL, 1UL)).First();
            two.Equip(item);
            two.AddScraps(40);

            // The run saves whenever the map comes up.
            _director.StartTestLevel(MapNodeType.Repair);
            _level.DebugTakeExit();
            yield return WaitUntil(() => _director.State == RunDirector.Phase.Map, 3f);
            Assert.IsTrue(RunSaveFile.TryLoad(out var data));
            Assert.AreEqual(RunSaveFile.Version, data.version);
            Assert.AreEqual(2, data.pilots.Count, "Both pilots are saved.");
            Assert.AreEqual(item.Id, data.pilots[1].equipment.Single().id, "With their own equipment.");
            Assert.AreEqual(40, data.pilots[0].scraps, "The shared wallet.");

            RunSession.Resume(data);
            yield return LoadRun(0, resume: true);
            Assert.IsNotNull(Pilot(1).Equipment.Find(item), "Continuing puts player 2's equipment back on player 2.");
            Assert.IsNull(Pilot(0).Equipment.Find(item));
            Assert.AreEqual(40, Pilot(0).Scraps);
            Assert.AreSame(Pilot(0).Wallet, Pilot(1).Wallet);
        }

        [UnityTest]
        public IEnumerator TheHub_LetsAPlayerJoin_PickTheirOwnShip_AndLaunchesWithBoth()
        {
            LocalPlayers.Reset(); // start alone: this player joins in the Hub
            GameClock.ResumeAll();
            RunSession.Clear();
            yield return SceneManager.LoadSceneAsync(Pathfinder.Game.Boot.SceneNames.Hub, LoadSceneMode.Single);
            yield return null;
            var menu = Object.FindAnyObjectByType<HubMenu>();
            menu.OpenLoadout(survival: false);
            CollectionAssert.DoesNotContain(menu.LoadoutRows, "pilot", "Alone, there's no one else's loadout to pick.");
            CollectionAssert.AreEqual(new[] { "ship", "primary", "secondary", "paint", "launch", "add" }, menu.LoadoutRows);

            Assert.AreEqual(1, menu.AddPlayer(_pad), "Player 2 joins with their controller.");
            Assert.AreEqual(1, menu.EditingPlayer, "And picks their loadout next.");
            CollectionAssert.IsSubsetOf(new[] { "pilot", "ship", "primary", "secondary", "paint", "launch", "remove" }, menu.LoadoutRows);
            menu.Change("ship", 1);
            // Their own colour (playtest 8): stepping back from gold skips player 1's cyan.
            Assert.AreEqual(1, LocalPlayers.PaintOf(1));
            menu.Change("paint", -1);
            Assert.AreEqual(PilotPaint.Count - 1, LocalPlayers.PaintOf(1));
            Assert.AreEqual(0, LocalPlayers.PaintOf(0), "Player 1's is untouched.");

            menu.StartRun();
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((SceneManager.GetActiveScene().name != "Run" || CombatRegistry.Players.Count < 2) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, CombatRegistry.Players.Count, "Both players fly.");
            Assert.AreEqual(RunSession.Current.Loadout.ShipId, Pilot(0).Definition.Id, "Player 1 flies their pick.");
            Assert.AreEqual(LocalPlayers.LoadoutFor(1).ShipId, Pilot(1).Definition.Id, "Player 2 flies theirs.");
            Assert.AreNotEqual(Pilot(0).Definition.Id, Pilot(1).Definition.Id, "Each their own.");
            Assert.AreEqual(PilotPaint.Get(PilotPaint.Count - 1).Ui, CoopRules.PilotColor(1), "In the colour they picked.");
        }

        /// <summary>The Hub's loadout page with a second player, for visual review.</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureHubCoop()
        {
            string directory = Screenshots.Directory();
            LocalPlayers.Reset();
            GameClock.ResumeAll();
            RunSession.Clear();
            yield return SceneManager.LoadSceneAsync(Pathfinder.Game.Boot.SceneNames.Hub, LoadSceneMode.Single);
            yield return null;
            var menu = Object.FindAnyObjectByType<HubMenu>();
            menu.OpenLoadout(survival: false);
            menu.AddPlayer(_pad);
            yield return null;
            menu.List.ScrollTo(menu.Rows[menu.LoadoutRows.ToList().IndexOf("paint")].Rect); // down to their colour
            yield return null;
            Screenshots.CaptureWithUi(System.IO.Path.Combine(directory, "coop_hub_loadout.png"));
        }

        [UnityTest]
        public IEnumerator TheHub_HearsWhichControllerTheNewPlayerPresses_EvenOneThatShowsUpTwice()
        {
            // Playtest 8: one PlayStation pad drove both ships. It can show up as two devices (itself and a virtual Xbox
            // pad), and player 1 kept every device nobody had claimed.
            LocalPlayers.Reset();
            var twin = InputSystem.AddDevice<Gamepad>();
            var other = InputSystem.AddDevice<Gamepad>();
            try
            {
                GameClock.ResumeAll();
                RunSession.Clear();
                yield return SceneManager.LoadSceneAsync(Pathfinder.Game.Boot.SceneNames.Hub, LoadSceneMode.Single);
                yield return null;
                var menu = Object.FindAnyObjectByType<HubMenu>();
                menu.OpenLoadout(survival: false);

                // Player 1's pad (both its devices) chooses ADD PLAYER 2...
                Press(_pad, twin);
                yield return null;
                menu.ListenForJoin();
                Release(_pad, twin);
                yield return new WaitForSecondsRealtime(0.4f);

                // ...so pressing it again isn't a new player.
                Press(_pad, twin);
                yield return null;
                Release(_pad, twin);
                yield return null;
                Assert.AreEqual(1, LocalPlayers.Count, "Player 1's own controller doesn't join as player 2.");
                Assert.AreEqual(1, menu.ListeningFor, "Still listening for player 2.");

                // A moment later (two pads pressing within a few frames of each other count as one controller).
                yield return new WaitForSecondsRealtime(0.2f);
                Press(other);
                yield return null;
                Release(other);
                yield return null;
                Assert.AreEqual(2, LocalPlayers.Count, "The controller player 2 pressed joins.");
                Assert.AreEqual(1, LocalPlayers.OwnerOf(other));
                Assert.AreEqual(LocalPlayers.Controls.Gamepad, LocalPlayers.ControlsOf(0), "Player 1 keeps their controller...");
                CollectionAssert.IsSubsetOf(new InputDevice[] { _pad, twin }, LocalPlayers.DevicesFor(0), "...both its devices.");
                CollectionAssert.DoesNotContain(LocalPlayers.DevicesFor(0), other);
                CollectionAssert.DoesNotContain(LocalPlayers.DevicesFor(1), _pad);
                CollectionAssert.DoesNotContain(LocalPlayers.DevicesFor(1), twin);
                Assert.AreEqual("controls", menu.LoadoutRows[1], "Each player's controls are shown, and can be changed.");
            }
            finally
            {
                InputSystem.RemoveDevice(twin);
                InputSystem.RemoveDevice(other);
            }
        }

        /// <summary>Presses Triangle/Y (not a menu button) on these pads in one frame.</summary>
        static void Press(params Gamepad[] pads)
        {
            foreach (var pad in pads)
                InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.North));
        }

        static void Release(params Gamepad[] pads)
        {
            foreach (var pad in pads)
                InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState());
        }

        [UnityTest]
        public IEnumerator TheHub_LetsAPlayerLeave()
        {
            GameClock.ResumeAll();
            RunSession.Clear();
            yield return SceneManager.LoadSceneAsync(Pathfinder.Game.Boot.SceneNames.Hub, LoadSceneMode.Single);
            yield return null;
            var menu = Object.FindAnyObjectByType<HubMenu>();
            menu.OpenLoadout(survival: false);
            CollectionAssert.Contains(menu.LoadoutRows, "pilot", "Player 2 joined earlier this session.");
            menu.Change("pilot", 1);
            Assert.AreEqual(1, menu.EditingPlayer);
            menu.RemovePlayer(1);
            Assert.AreEqual(1, LocalPlayers.Count);
            CollectionAssert.DoesNotContain(menu.LoadoutRows, "pilot");
        }

        [UnityTest]
        public IEnumerator Survival_FliesWithBothPilots()
        {
            GameClock.ResumeAll();
            RunSession.Clear();
            CombatWorld.NextSeedOverride = new Seed(31UL);
            yield return SceneManager.LoadSceneAsync(Pathfinder.Game.Boot.SceneNames.Survival, LoadSceneMode.Single);
            yield return null;
            Assert.AreEqual(2, CombatRegistry.Players.Count, "Survival is co-op too.");
            Assert.AreSame(Pilot(0).Wallet, Pilot(1).Wallet);
        }

        [UnityTest]
        public IEnumerator AVersionOneSave_StillLoads()
        {
            yield return null;
            var data = new RunSaveFile.Data { version = 1, seed = RunSeed.ToCode(new Seed(44UL)) };
            data.pilot.ship = "ship.stormbringer";
            data.pilots = null;
            System.IO.File.WriteAllText(RunSaveFile.FilePath, JsonUtility.ToJson(data));
            Assert.IsTrue(RunSaveFile.TryLoad(out var loaded), "A save from before co-op still continues.");
            Assert.AreEqual(1, loaded.pilots.Count);
            Assert.AreEqual("ship.stormbringer", loaded.pilots[0].ship);
        }
    }
}
