using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Local co-op in the Sandbox (M8): two pilots, each with their own gamepad, sharing one screen.</summary>
    public class CoopTests : SandboxTestBase
    {
        readonly List<Gamepad> _pads = new List<Gamepad>();

        [SetUp]
        public void DefaultColours()
        {
            Pathfinder.Game.Settings.GameSettings.ResetToDefaults(); // everyone in their default colour
            LocalPlayers.Reset();
        }

        [TearDown]
        public void LeaveCoop()
        {
            Pathfinder.Game.Settings.GameSettings.ResetToDefaults(); // forget colours picked here (in memory only, in tests)
            LocalPlayers.Reset();
            foreach (var pad in _pads)
            {
                if (pad.added)
                    InputSystem.RemoveDevice(pad);
            }
            _pads.Clear();
        }

        /// <summary>Joins virtual gamepads as players 2, 3… and loads the Sandbox with everyone.</summary>
        IEnumerator LoadCoop(int pilots = 2)
        {
            for (int i = 1; i < pilots; i++)
            {
                var pad = InputSystem.AddDevice<Gamepad>();
                _pads.Add(pad);
                Assert.AreEqual(i, LocalPlayers.Join(pad));
            }
            yield return LoadRun(pilots: pilots);
        }

        PlayerShip Pilot(int index) => CombatRegistry.Players.First(p => p.PlayerIndex == index);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static Color PartColor(PlayerShip ship, string part) =>
            ship.GetComponentsInChildren<Renderer>(true).First(r => r.name == part).sharedMaterial.GetColor(BaseColorId);

        /// <summary>Colours read back from a material come back a hair off (colour space round trips).</summary>
        static void AssertColor(Color expected, Color actual, string message = null)
        {
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(expected[i], actual[i], 0.01f, $"{message} ({expected} vs {actual})");
        }

        [UnityTest]
        public IEnumerator EachShip_WearsItsPilotsColour_AndRepaintsWhenItChanges()
        {
            // Playtest 8: "it's hard to distinguish who is who". Both fly the same ship here.
            yield return LoadCoop();
            yield return null;
            var one = Pilot(0);
            var two = Pilot(1);
            Assert.AreEqual(one.Definition, two.Definition);
            AssertColor(PilotPaint.Get(0).Glow, PartColor(one, "Cockpit"), "Player 1's lights are cyan.");
            AssertColor(PilotPaint.Get(1).Glow, PartColor(two, "Cockpit"), "Player 2's are gold.");
            AssertColor(PilotPaint.Get(1).Glow, PartColor(two, "WingtipL"));
            Assert.AreNotEqual(PartColor(one, "Hull"), PartColor(two, "Hull"), "In co-op the hulls wear the colours too.");
            var gold = PartColor(two, "Hull");
            Assert.Greater(gold.r, gold.b, "Player 2's hull is gold.");

            LocalPlayers.SetPaint(1, 4);
            yield return null;
            AssertColor(PilotPaint.Get(4).Glow, PartColor(two, "Cockpit"), "Repainted green.");
            var green = PartColor(two, "Hull");
            Assert.Greater(green.g, green.r);
            Assert.AreEqual(PilotPaint.Get(4).Ui, CoopRules.PilotColor(1), "Their ring, tag and panel follow.");
        }

        [UnityTest]
        public IEnumerator Alone_TheLightsWearThePaint_AndTheHullKeepsItsShipsOwnColour()
        {
            LocalPlayers.SetPaint(0, 1);
            yield return LoadRun();
            yield return null;
            var hull = PartColor(Player, "Hull");
            AssertColor(PilotPaint.Get(1).Glow, PartColor(Player, "Cockpit"), "Gold lights.");

            LocalPlayers.SetPaint(0, 4);
            yield return null;
            AssertColor(PilotPaint.Get(4).Glow, PartColor(Player, "Cockpit"), "Green lights.");
            AssertColor(hull, PartColor(Player, "Hull"), "Alone, the hull keeps the ship's own colour (M7).");
        }

        [UnityTest]
        public IEnumerator TwoPilots_ArriveSideBySide_InTheirColours_AndShareTheirScraps()
        {
            yield return LoadCoop();
            var one = Pilot(0);
            var two = Pilot(1);
            Assert.IsTrue(one.IsLocal && two.IsLocal);
            Assert.AreEqual(one.Position.y, two.Position.y, 0.01f, "Side by side.");
            Assert.Greater(Mathf.Abs(one.Position.x - two.Position.x), 2f, "Not on top of each other.");

            yield return null;
            Assert.IsTrue(one.GetComponentInChildren<PilotMarker>().Shown && two.GetComponentInChildren<PilotMarker>().Shown,
                "Each wears a ring in their colour.");

            Assert.AreSame(one.Wallet, two.Wallet, "One shared wallet.");
            two.AddScraps(12);
            Assert.AreEqual(12, one.Scraps, "Scraps either pilot picks up are everyone's.");
            Assert.IsTrue(one.TrySpendScraps(5));
            Assert.AreEqual(7, two.Scraps);

            var hud = Object.FindAnyObjectByType<CombatHud>();
            Assert.IsTrue(hud.PilotPanels[0].Root.activeSelf && hud.PilotPanels[1].Root.activeSelf, "Each pilot has their own panel.");
            Assert.AreEqual("P2", hud.PilotPanels[1].Tag);
            Assert.IsTrue(hud.SecondSlotsShown, "Player 2's ship and slots show bottom right.");
        }

        [UnityTest]
        public IEnumerator EachPilot_IsFlownWithTheirOwnController()
        {
            yield return LoadCoop();
            var one = Pilot(0);
            var two = Pilot(1);
            var inputOne = one.GetComponent<LocalInputCommandSource>();
            var inputTwo = two.GetComponent<LocalInputCommandSource>();
            Assert.AreEqual(1, inputTwo.PlayerIndex);

            InputSystem.QueueStateEvent(_pads[0], new GamepadState { leftStick = new Vector2(1f, 0f) });
            yield return null;
            Assert.Greater(inputTwo.ReadCommand(two).Move.x, 0.5f, "Player 2's stick flies player 2.");
            Assert.AreEqual(Vector2.zero, inputOne.ReadCommand(one).Move, "And not player 1.");
            InputSystem.QueueStateEvent(_pads[0], new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Alone_AnyDeviceFliesTheShip()
        {
            // Playtest 8: alone, nothing could fly the ship. (Keyboards are off in batch mode without focus, so this
            // checks a gamepad; the keyboard rides on the same device setting.)
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                yield return LoadRun();
                Assert.IsNull(LocalPlayers.DevicesFor(0), "Alone, player 1 listens to every device.");
                var input = Player.GetComponent<LocalInputCommandSource>();
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(1f, 0f) });
                yield return null;
                Assert.Greater(input.ReadCommand(Player).Move.x, 0.5f, "Alone, any gamepad flies the ship.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
            }
        }

        [UnityTest]
        public IEnumerator Menus_ListenOnlyToThePlayerTheyAreFor()
        {
            yield return LoadCoop();
            var menu = new MenuInput();
            var pad = _pads[0];
            try
            {
                menu.RestrictTo(LocalPlayers.DevicesFor(0));
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                yield return null;
                Assert.IsFalse(menu.Submitted, "Player 1's menu ignores player 2's controller.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;

                menu.RestrictTo(LocalPlayers.DevicesFor(1));
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                yield return null;
                Assert.IsTrue(menu.Submitted, "Player 2's menu answers to it.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
            }
            finally
            {
                menu.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Enemies_AreTougher_ButHazardsAreNot()
        {
            yield return LoadCoop();
            var drone = Spawn("enemy.drone", new Vector2(0f, 8f));
            Assert.AreEqual(EnemyDef("enemy.drone").MaxHealth * CoopRules.EnemyHealthScale(2), drone.Vitals.MaxHull, 0.01f,
                "×1.65 health with two pilots.");
            var asteroid = Spawn("hazard.asteroid", new Vector2(8f, 8f));
            Assert.AreEqual(EnemyDef("hazard.asteroid").MaxHealth, asteroid.Vitals.MaxHull, 0.01f, "Scenery stays as it is.");
        }

        [UnityTest]
        public IEnumerator ADownedPilot_StaysAsAWreck_UntilATeammateBringsThemBack()
        {
            yield return LoadCoop();
            var one = Pilot(0);
            var two = Pilot(1);
            two.PlaceForLevel(one.Position + new Vector2(8f, 0f));
            Kill(two);
            yield return WaitTicks(2);
            Assert.IsFalse(two.IsAlive, "Player 2 is down.");
            Assert.IsFalse(World.IsOver, "The run goes on while a pilot still flies.");
            Assert.IsTrue(two.GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled), "The wreck stays where it fell.");
            yield return null;
            var hud = Object.FindAnyObjectByType<CombatHud>();
            StringAssert.Contains("P2", hud.Readouts[1].Help, "The wreck calls for help.");

            yield return WaitTicks(60);
            Assert.AreEqual(0f, two.ReviveProgress, "Nobody near: nothing happens.");

            one.PlaceForLevel(two.Position + new Vector2(1.2f, 0f));
            int reviveTicks = Mathf.RoundToInt(CoopRules.ReviveTime / Time.fixedDeltaTime);
            yield return WaitTicks(reviveTicks / 2);
            Assert.IsFalse(two.IsAlive, "It takes a couple of seconds.");
            Assert.That(two.ReviveProgress, Is.InRange(0.45f, 0.9f));
            yield return WaitUntilTicks(() => two.IsAlive, reviveTicks);
            Assert.IsTrue(two.IsAlive, "Hovering next to the wreck brings player 2 back.");
            Assert.AreEqual(two.Vitals.MaxHull * CoopRules.ReviveHullFraction, two.Vitals.Hull, 0.5f, "With a little hull.");
            Assert.IsTrue(two.IsIntangible, "And a moment to get clear.");
            yield return null;
            Assert.AreEqual(string.Empty, hud.Readouts[1].Help);
        }

        [UnityTest]
        public IEnumerator ReviveProgress_DrainsWhenTheTeammateLeaves()
        {
            yield return LoadCoop();
            var one = Pilot(0);
            var two = Pilot(1);
            Kill(two);
            one.PlaceForLevel(two.Position + new Vector2(1f, 0f));
            yield return WaitTicks(60);
            one.PlaceForLevel(two.Position + new Vector2(10f, 0f));
            float halfway = two.ReviveProgress;
            Assert.That(halfway, Is.InRange(0.45f, 0.85f), "A second next to the wreck: about halfway.");

            yield return WaitTicks(60);
            Assert.That(two.ReviveProgress, Is.InRange(0.01f, halfway - 0.5f * CoopRules.ReviveDrain * 0.9f), "Left alone, the progress slowly drains.");
        }

        [UnityTest]
        public IEnumerator TheRunIsOnlyLost_WhenEveryPilotIsDown()
        {
            yield return LoadCoop();
            Kill(Pilot(1));
            yield return WaitTicks(2);
            Assert.IsFalse(World.IsOver);
            Kill(Pilot(0));
            yield return WaitTicks(2);
            Assert.IsTrue(World.IsOver, "Both down: it's over.");
        }

        [UnityTest]
        public IEnumerator TheCamera_PullsBack_ToKeepEveryoneInView()
        {
            yield return LoadCoop();
            var rig = Object.FindAnyObjectByType<CameraRig>();
            yield return WaitTicks(10);
            float close = rig.Distance;

            Pilot(0).PlaceForLevel(new Vector2(-24f, -14f));
            Pilot(1).PlaceForLevel(new Vector2(24f, 14f));
            yield return WaitTicks(5);
            Assert.Greater(rig.TargetDistance, close + 5f, "Spread out, the view pulls back.");
            yield return WaitTicks(150); // the zoom eases out
            var view = rig.GetComponent<Camera>();
            foreach (var pilot in CombatRegistry.Players)
            {
                Vector3 point = view.WorldToViewportPoint(pilot.transform.position);
                Assert.That(point.x, Is.InRange(0f, 1f), $"P{pilot.PlayerIndex + 1} is on screen (x).");
                Assert.That(point.y, Is.InRange(0f, 1f), $"P{pilot.PlayerIndex + 1} is on screen (y).");
            }

            Pilot(1).PlaceForLevel(Pilot(0).Position + new Vector2(1f, 0f));
            yield return WaitTicks(150);
            Assert.AreEqual(close, rig.Distance, 1f, "Together again, it comes back in.");
        }

        /// <summary>Two pilots far apart, one of them down and being revived, for visual review.</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureCoop()
        {
            string directory = Screenshots.Directory();
            yield return LoadCoop();
            var one = Pilot(0);
            var two = Pilot(1);
            one.GodMode = true;
            Spawn("enemy.drone", new Vector2(-4f, 9f));
            Spawn("enemy.gunner", new Vector2(6f, 10f));
            two.PlaceForLevel(one.Position + new Vector2(9f, 4f));
            yield return WaitTicks(30);
            yield return null;
            Screenshots.CaptureWithUi(System.IO.Path.Combine(directory, "coop_two_pilots.png"));

            Kill(two);
            one.PlaceForLevel(two.Position + new Vector2(-1.4f, -0.6f));
            // Freeze right on the moment: the test clock runs fast, so a frame can pass a good part of the revive.
            yield return WaitUntilTicks(() =>
            {
                if (two.ReviveProgress < 0.55f)
                    return false;
                Pathfinder.Game.Simulation.GameClock.Pause(this);
                return true;
            }, 120);
            yield return null;
            Screenshots.CaptureWithUi(System.IO.Path.Combine(directory, "coop_revive.png"));
            Pathfinder.Game.Simulation.GameClock.Resume(this);
        }
    }
}
