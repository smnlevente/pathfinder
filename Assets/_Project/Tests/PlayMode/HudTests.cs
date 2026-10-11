using System.Collections;
using System.IO;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Equipment;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>The HUD's ship readout and its layout on wide screens (playtest 6).</summary>
    public class HudTests : SandboxTestBase
    {
        [UnityTest]
        public IEnumerator ShipReadout_ShowsAmmoAndHull_OnTheShip()
        {
            yield return LoadRun();
            var hud = Object.FindAnyObjectByType<CombatHud>();
            yield return null;
            Assert.IsTrue(hud.ShipReadoutShown, "A readout follows the ship.");
            Assert.AreEqual(1f, hud.ShipAmmoFill, 0.001f, "A full magazine: a full ring.");

            Script(_ => new ShipCommand { FirePrimary = true });
            yield return WaitTicks(30);
            yield return null;
            Assert.Less(hud.ShipAmmoFill, 1f, "The ring around the ship drains as it fires.");

            Player.ApplyDamage(new DamageInfo(Player.Vitals.MaxShield + 30f, Team.Enemy, 0, Player.Position, Vector2.up));
            yield return null;
            Assert.Less(hud.ShipHullFill, 1f, "The hull bar under the ship drops.");
        }

        [UnityTest]
        public IEnumerator ShipReadout_ShowsThePrecisionLoaderWindow()
        {
            yield return LoadRun();
            var hud = Object.FindAnyObjectByType<CombatHud>();
            Player.Equip(Item("equipment.precision-loader"));
            var magazine = ((AutomaticPrimary)Player.Primary).Magazine;
            Assert.IsFalse(Player.ReloadWindow.HasValue);

            Script(_ => new ShipCommand { FirePrimary = true });
            yield return WaitUntilTicks(() => magazine.IsReloading, 400);
            Script(_ => default);
            yield return null;
            Assert.IsTrue(Player.ReloadWindow.HasValue && hud.SweetSpotShown, "While reloading, the window shows on the ship's ring.");
        }

        /// <summary>Playtest 7: the slots around the ship show what's equipped, and where an item a menu points at would go.</summary>
        [UnityTest]
        public IEnumerator ShipSlots_ShowWhatsEquipped_AndWhereAnItemWouldGo()
        {
            yield return LoadRun();
            var slots = Object.FindAnyObjectByType<CombatHud>().Slots;
            var owner = new object();
            var mag = Item("equipment.extended-mag");
            var piercing = Item("equipment.piercing-rounds");
            var ricochet = Item("equipment.ricochet");
            Assert.AreEqual(EquipmentCategory.Weapon, mag.Category);
            Assert.AreEqual(EquipmentCategory.Weapon, piercing.Category);
            Assert.AreEqual(EquipmentCategory.Weapon, ricochet.Category);

            Player.Equip(mag);
            yield return null;
            Assert.AreEqual(2, slots.SlotCount(EquipmentCategory.Weapon), "Two weapon mod slots at the nose.");
            Assert.AreEqual(1, slots.SlotCount(EquipmentCategory.Secondary));
            Assert.AreEqual(3, slots.SlotCount(EquipmentCategory.Hull));
            Assert.AreEqual(1, slots.SlotCount(EquipmentCategory.Dash));
            Assert.AreEqual(SlotState.Filled, slots.StateOf(EquipmentCategory.Weapon, 0));
            Assert.AreEqual(SlotState.Empty, slots.StateOf(EquipmentCategory.Weapon, 1));

            try
            {
                EquipmentPreview.Show(owner, piercing);
                yield return null;
                Assert.AreEqual(SlotState.Target, slots.StateOf(EquipmentCategory.Weapon, 1), "A new item lights the free slot it would fill.");

                EquipmentPreview.Show(owner, mag);
                yield return null;
                Assert.AreEqual(SlotState.LevelUp, slots.StateOf(EquipmentCategory.Weapon, 0), "A duplicate lights the item it would level up.");

                Player.Equip(piercing);
                EquipmentPreview.Show(owner, ricochet);
                yield return null;
                Assert.AreEqual(SlotState.Swap, slots.StateOf(EquipmentCategory.Weapon, 0), "A full category lights up gold: it means a swap.");
                Assert.AreEqual(SlotState.Swap, slots.StateOf(EquipmentCategory.Weapon, 1));

                EquipmentPreview.Show(owner, ricochet, 1);
                yield return null;
                Assert.AreEqual(SlotState.Replace, slots.StateOf(EquipmentCategory.Weapon, 1), "In the swap step, the slot that would go.");
                Assert.AreEqual(SlotState.Filled, slots.StateOf(EquipmentCategory.Weapon, 0));
            }
            finally
            {
                EquipmentPreview.Clear(owner);
            }
            yield return null;
            Assert.AreEqual(SlotState.Filled, slots.StateOf(EquipmentCategory.Weapon, 1), "Nothing pointed at: just what's equipped.");
        }

        /// <summary>The HUD at 16:9 and at 21:9: on the wide screen the corner panels stay inside a centred 16:9 area.</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureHud()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun();
            Player.Equip(Item("equipment.precision-loader"));
            Spawn("enemy.gunner", new Vector2(4f, 7f));
            var magazine = ((AutomaticPrimary)Player.Primary).Magazine;
            Script(_ => new ShipCommand { FirePrimary = true, Aim = Vector2.up });
            yield return WaitUntilTicks(() => magazine.IsReloading, 400);
            Script(_ => default);
            yield return WaitTicks(20);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "hud_16x9.png"));

            int width = Screenshots.Width, height = Screenshots.Height;
            try
            {
                Screenshots.Width = 2520;
                Screenshots.Height = 1080;
                Screenshots.CaptureWithUi(Path.Combine(directory, "hud_21x9.png"));
            }
            finally
            {
                Screenshots.Width = width;
                Screenshots.Height = height;
            }
        }
    }
}
