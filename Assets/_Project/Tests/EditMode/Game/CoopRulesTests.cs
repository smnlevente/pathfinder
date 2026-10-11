using NUnit.Framework;
using Pathfinder.Core.Run;
using Pathfinder.Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pathfinder.Tests.Game
{
    /// <summary>Local co-op's rules and who is playing (M8).</summary>
    public class CoopRulesTests
    {
        Gamepad _padA, _padB;

        [SetUp]
        public void AddPads()
        {
            LocalPlayers.Reset();
            _padA = InputSystem.AddDevice<Gamepad>();
            _padB = InputSystem.AddDevice<Gamepad>();
        }

        [TearDown]
        public void RemovePads()
        {
            LocalPlayers.Reset();
            LocalPlayers.SetLoadout(0, default);
            InputSystem.RemoveDevice(_padA);
            InputSystem.RemoveDevice(_padB);
        }

        [Test]
        public void EnemiesAreTougher_AndWavesBigger_ForEachExtraPilot()
        {
            Assert.AreEqual(1f, CoopRules.EnemyHealthScale(1));
            Assert.AreEqual(1.65f, CoopRules.EnemyHealthScale(2), 1e-4f);
            Assert.AreEqual(2.3f, CoopRules.EnemyHealthScale(3), 1e-4f);
            Assert.AreEqual(1f, CoopRules.WaveBudgetScale(1));
            Assert.AreEqual(1.5f, CoopRules.WaveBudgetScale(2), 1e-4f);
            Assert.AreEqual(2.5f, CoopRules.WaveBudgetScale(4), 1e-4f);
            Assert.That(CoopRules.ReviveHullFraction, Is.InRange(0.1f, 0.15f), "A revived pilot comes back with 10-15% hull, as asked.");
        }

        [Test]
        public void Pilots_ArriveSideBySide_EachInTheirOwnColour()
        {
            Assert.AreEqual(Vector2.zero, CoopRules.EntryOffset(0, 1), "Alone, right on the entry.");
            Assert.AreEqual(-CoopRules.EntryOffset(1, 2).x, CoopRules.EntryOffset(0, 2).x, 1e-4f, "Two pilots either side of it.");
            Assert.Greater(CoopRules.EntryOffset(1, 2).x - CoopRules.EntryOffset(0, 2).x, 2f, "Far enough apart not to overlap.");
            for (int i = 0; i < CoopRules.MaxPilots; i++)
            {
                for (int j = i + 1; j < CoopRules.MaxPilots; j++)
                    Assert.AreNotEqual(CoopRules.PilotColor(i), CoopRules.PilotColor(j), $"P{i + 1} and P{j + 1} have their own colours.");
            }
            Assert.AreEqual("P2", CoopRules.Tag(1));
        }

        [Test]
        public void Alone_PlayerOnePlaysWithAnything_WithCompany_EachPlayerHasTheirOwnControls()
        {
            Assert.AreEqual(1, LocalPlayers.Count, "Player 1 is always here.");
            Assert.IsNull(LocalPlayers.DevicesFor(0), "Alone, player 1 plays with every device.");

            LocalPlayers.SetLoadout(0, new PilotLoadout("ship.titan", "primary.photon-repeater", "secondary.nova-pulse"));
            Assert.AreEqual(1, LocalPlayers.Join(_padB));
            Assert.AreEqual(-1, LocalPlayers.Join(_padB), "A controller joins once.");
            Assert.AreEqual("ship.titan", LocalPlayers.LoadoutFor(1).ShipId, "A new player starts with player 1's loadout.");

            // Playtest 8: player 1 kept "everything nobody claimed", and a controller showing up twice drove both ships.
            Assert.AreEqual(LocalPlayers.Controls.KeyboardMouse, LocalPlayers.ControlsOf(0), "Player 1 now has the keyboard and mouse, explicitly.");
            CollectionAssert.DoesNotContain(LocalPlayers.DevicesFor(0), _padA, "Not a spare controller.");
            CollectionAssert.DoesNotContain(LocalPlayers.DevicesFor(0), _padB);
            CollectionAssert.AreEqual(new InputDevice[] { _padB }, LocalPlayers.DevicesFor(1), "Player 2 plays with their own controller only.");

            int version = LocalPlayers.Version;
            LocalPlayers.Leave(1);
            Assert.AreEqual(1, LocalPlayers.Count);
            Assert.AreNotEqual(version, LocalPlayers.Version, "Input sources notice when someone leaves.");
            Assert.IsNull(LocalPlayers.DevicesFor(0), "Alone again, player 1 plays with anything.");
            LocalPlayers.Leave(0);
            Assert.AreEqual(1, LocalPlayers.Count, "Player 1 can't leave.");
        }

        [Test]
        public void PlayerOne_KeepsTheControllerTheyWereUsing()
        {
            Assert.AreEqual(1, LocalPlayers.Join(new InputDevice[] { _padB }, playerOne: new InputDevice[] { _padA }));
            Assert.AreEqual(LocalPlayers.Controls.Gamepad, LocalPlayers.ControlsOf(0));
            CollectionAssert.AreEqual(new InputDevice[] { _padA }, LocalPlayers.DevicesFor(0), "Player 1 keeps the controller that chose to add a player.");
        }

        [Test]
        public void AControllerThatShowsUpTwice_BelongsToOnePlayer()
        {
            // A PlayStation pad can appear as itself and as a virtual Xbox pad (Steam, DS4Windows).
            Assert.AreEqual(1, LocalPlayers.Join(new InputDevice[] { _padA, _padB }));
            CollectionAssert.AreEquivalent(new InputDevice[] { _padA, _padB }, LocalPlayers.DevicesFor(1));
            Assert.AreEqual(1, LocalPlayers.OwnerOf(_padA));
            Assert.AreEqual(1, LocalPlayers.OwnerOf(_padB));
        }

        [Test]
        public void ChangingControls_SwapsWithWhoeverHadThem()
        {
            LocalPlayers.Join(_padB); // P1 keyboard & mouse, P2 pad B
            LocalPlayers.Assign(1, LocalPlayers.Controls.KeyboardMouse);
            Assert.AreEqual(LocalPlayers.Controls.KeyboardMouse, LocalPlayers.ControlsOf(1), "Player 2 takes the keyboard.");
            Assert.AreEqual(LocalPlayers.Controls.Gamepad, LocalPlayers.ControlsOf(0), "Player 1 gets player 2's controller.");
            CollectionAssert.AreEqual(new InputDevice[] { _padB }, LocalPlayers.DevicesFor(0));

            LocalPlayers.Assign(0, LocalPlayers.Controls.Gamepad, _padA);
            CollectionAssert.AreEqual(new InputDevice[] { _padA }, LocalPlayers.DevicesFor(0), "A free controller is just taken.");
            Assert.AreEqual(-1, LocalPlayers.OwnerOf(_padB));
        }

        [Test]
        public void AtMostFourPilots()
        {
            var extra = new Gamepad[4];
            try
            {
                for (int i = 0; i < extra.Length; i++)
                    extra[i] = InputSystem.AddDevice<Gamepad>();
                for (int i = 0; i < extra.Length; i++)
                    LocalPlayers.Join(extra[i]);
                Assert.AreEqual(CoopRules.MaxPilots, LocalPlayers.Count);
                Assert.IsFalse(LocalPlayers.CanJoin(_padA));
            }
            finally
            {
                foreach (var pad in extra)
                {
                    if (pad != null)
                        InputSystem.RemoveDevice(pad);
                }
            }
        }
    }
}
