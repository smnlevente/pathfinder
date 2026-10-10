using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Equipment;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Equipment (GDD §8) and ship traits (GDD §5.3), in the combat sandbox.</summary>
    public class EquipmentTests : SandboxTestBase
    {
        void EquipTimes(string id, int times)
        {
            for (int i = 0; i < times; i++)
                Player.Equip(Item(id));
        }

        [UnityTest]
        public IEnumerator Overclock_RaisesFireRate()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.overclock"));
            int shots = 0;
            Player.Fired += _ => shots++;
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });

            yield return WaitTicks(120);

            // 10/s base, +15% = 11.5/s over 2 s.
            Assert.That(shots, Is.InRange(22, 24));
        }

        [UnityTest]
        public IEnumerator ArmorPlating_RaisesMaxHullAndLevelsUpOnDuplicates()
        {
            yield return LoadRun();
            var item = Item("equipment.armor-plating");

            Assert.AreEqual(EquipOutcome.Equipped, Player.Equip(item));
            Assert.AreEqual(120f, Player.Vitals.MaxHull, 1e-3f);
            Assert.AreEqual(120f, Player.Vitals.Hull, 1e-3f, "Gaining max hull also heals by that amount.");

            Assert.AreEqual(EquipOutcome.LeveledUp, Player.Equip(item));
            Assert.AreEqual(140f, Player.Vitals.MaxHull, 1e-3f);
            Assert.AreEqual(2, Player.Equipment.Find(item).Level);
        }

        [UnityTest]
        public IEnumerator FullCategory_NeedsASwap()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.armor-plating"));
            Player.Equip(Item("equipment.capacitor"));
            Player.Equip(Item("equipment.adrenal-thrusters"));
            var fourth = Item("equipment.salvage-magnet");

            Assert.AreEqual(EquipOutcome.NeedsSwap, Player.PreviewEquip(fourth));
            Player.ReplaceEquipment(0, fourth);

            Assert.IsNull(Player.Equipment.Find(Item("equipment.armor-plating")), "The replaced item is lost.");
            Assert.AreEqual(100f, Player.Vitals.MaxHull, 1e-3f, "Its stats go with it.");
            Assert.AreEqual(5f, Player.MagnetRadius, 1e-3f);
        }

        [UnityTest]
        public IEnumerator PiercingRounds_HitTheEnemyBehind()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.piercing-rounds"));
            var near = Spawn("enemy.spinner", new Vector2(0f, 6f));
            var far = Spawn("enemy.spinner", new Vector2(0f, 9f));
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });

            yield return WaitTicks(30);

            Assert.Less(near.Vitals.Hull, near.Vitals.MaxHull);
            Assert.Less(far.Vitals.Hull, far.Vitals.MaxHull, "Shots pass through the first Spinner.");
        }

        [UnityTest]
        public IEnumerator SplitShot_AddsAFanEveryFourthShot()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.split-shot"));
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });

            yield return WaitTicks(21); // shots on ticks 0, 6, 12 and 18

            Assert.AreEqual(4 + 2, World.Projectiles.CountForTeam(Team.Player), "The 4th shot fires two extra projectiles.");
        }

        [UnityTest]
        public IEnumerator CriticalOptics_DealsDoubleDamageHits()
        {
            yield return LoadRun();
            EquipTimes("equipment.critical-optics", 3); // 20% at level III
            Spawn("enemy.carrier", new Vector2(0f, 9f), eliteId: "elite.armored");
            var amounts = new List<float>();
            void Record(CombatEntity target, DamageInfo info, DamageResult result)
            {
                if (info.SourceId == Player.EntityId && info.Kind == DamageKind.Projectile)
                    amounts.Add(info.Amount);
            }

            CombatEvents.Damaged += Record;
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });
            yield return WaitTicks(240);
            CombatEvents.Damaged -= Record;

            Assert.That(amounts, Has.Some.EqualTo(16f).Within(0.01f), "Crits deal double the 8-damage bolt.");
            Assert.That(amounts, Has.Some.EqualTo(8f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Incendiary_KeepsBurningAfterTheShooting()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.incendiary"));
            // An Armored Spinner (120 HP) survives long enough to measure, even with the Stormbringer's lightning.
            var spinner = Spawn("enemy.spinner", new Vector2(0f, 7f), eliteId: "elite.armored");
            int stopAt = World.Tick + 8; // two shots
            Script(tick => new ShipCommand { Aim = Vector2.up, FirePrimary = tick < stopAt });

            yield return WaitTicks(40);
            Assert.IsTrue(spinner.IsBurning);
            float hull = spinner.Vitals.Hull;

            yield return WaitTicks(60);
            Assert.Less(spinner.Vitals.Hull, hull, "Burns keep dealing damage without new hits.");
        }

        [UnityTest]
        public IEnumerator Ricochet_BouncesToANearbyEnemy()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.ricochet"));
            Spawn("enemy.spinner", new Vector2(0f, 7f));
            var side = Spawn("enemy.spinner", new Vector2(4f, 7f));
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });

            yield return WaitTicks(40);

            Assert.Less(side.Vitals.Hull, side.Vitals.MaxHull, "Bounces hit the Spinner the ship never aimed at.");
        }

        [UnityTest]
        public IEnumerator KillRefund_ShortensTheSecondaryCooldown()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.kill-refund"));
            Script(Once(new ShipCommand { FireSecondary = true }));
            yield return WaitTicks(2);
            float before = Player.SecondaryCooldown.TimeUntilReady;

            var drone = Spawn("enemy.drone", new Vector2(0f, 12f));
            drone.ApplyDamage(new DamageInfo(1000f, Team.Player, Player.EntityId, drone.Position, Vector2.up));

            Assert.AreEqual(before - 0.3f, Player.SecondaryCooldown.TimeUntilReady, 0.02f);
        }

        [UnityTest]
        public IEnumerator TwinCharge_ShowsBothCharges_AndAnnouncesEachRecharge()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.twin-charge"));
            Assert.AreEqual(2, Player.SecondaryCooldown.MaxCharges);
            int recharged = 0;
            Player.SecondaryRecharged += _ => recharged++;

            Script(Once(new ShipCommand { FireSecondary = true }));
            yield return WaitTicks(2);
            Assert.AreEqual(1, Player.SecondaryCooldown.Charges, "One salvo spends one charge; the other stays ready.");
            string label = Pathfinder.Game.Presentation.CombatHud.SecondaryLabel(Player);
            StringAssert.Contains("1/2", label, "The HUD counts the charges (playtest 5).");
            StringAssert.Contains("+1 in", label, "...and shows when the next one returns.");
            Assert.AreEqual(0, recharged);

            yield return WaitUntilTicks(() => recharged > 0, Mathf.CeilToInt(Player.SecondaryCooldown.RechargeTime * 60f) + 10);
            Assert.AreEqual(1, recharged, "The returning charge is announced once (the ready chime).");
            Assert.AreEqual(2, Player.SecondaryCooldown.Charges);
            StringAssert.Contains("2/2", Pathfinder.Game.Presentation.CombatHud.SecondaryLabel(Player));
        }

        [UnityTest]
        public IEnumerator TwinCharge_HoldingTheButton_FiresOneSalvoPerPress()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.twin-charge"));
            bool held = true;
            Script(_ => new ShipCommand { FireSecondary = held });

            yield return WaitTicks(30); // a long click
            Assert.AreEqual(1, Player.SecondaryCooldown.Charges, "Holding the button fires once (playtest 5: a click spent both charges).");

            held = false;
            yield return WaitTicks(2);
            held = true;
            yield return WaitTicks(2);
            Assert.AreEqual(0, Player.SecondaryCooldown.Charges, "A second press fires the second charge.");
        }

        [UnityTest]
        public IEnumerator LastStand_SurvivesOneLethalHit()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.last-stand"));

            Player.ApplyDamage(new DamageInfo(10_000f, Team.Enemy, 0, Player.Position, Vector2.up));
            Assert.IsTrue(Player.IsAlive);
            Assert.AreEqual(1f, Player.Vitals.Hull, 1e-3f);

            yield return WaitTicks(60); // let the grace window pass
            Player.ApplyDamage(new DamageInfo(10_000f, Team.Enemy, 0, Player.Position, Vector2.up));
            Assert.IsFalse(Player.IsAlive, "Only once per level.");
        }

        [UnityTest]
        public IEnumerator PhaseDash_ExtendsIntangibilityAndRestoresShield()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.phase-dash"));
            Player.ApplyDamage(new DamageInfo(10f, Team.Enemy, 0, Player.Position, Vector2.up));
            Script(Once(new ShipCommand { Dash = true }));

            yield return WaitTicks(1);
            yield return WaitUntilTicks(() => !Player.Dash.IsDashing, 30);
            Assert.IsTrue(Player.IsIntangible, "Still phased just after the dash ends.");

            float shield = Player.Vitals.Shield;
            SpawnEnemyBullet(Player.Position, Vector2.right);
            yield return WaitTicks(2);
            Assert.AreEqual(shield + 2f, Player.Vitals.Shield, 0.01f, "Phasing through a bullet restores 2 shield.");
        }

        [UnityTest]
        public IEnumerator BlinkDash_TeleportsInOneTick()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.blink-dash"));
            float startX = Player.Position.x;
            Script(Once(new ShipCommand { Dash = true, Move = Vector2.right }));

            yield return WaitTicks(1);

            Assert.AreEqual(startX + 5f, Player.Position.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator PushbackDash_ShovesAndDamagesNearbyEnemies()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.pushback-dash"));
            var spinner = Spawn("enemy.spinner", new Vector2(6.5f, 0f));
            Script(Once(new ShipCommand { Dash = true, Move = Vector2.right }));

            yield return WaitTicks(12);
            float distance = Vector2.Distance(spinner.Position, Player.Position);
            yield return WaitTicks(10);

            Assert.Less(spinner.Vitals.Hull, spinner.Vitals.MaxHull);
            Assert.Greater(Vector2.Distance(spinner.Position, Player.Position), distance);
        }

        [UnityTest]
        public IEnumerator BulletDash_FiresARingAtTheDashEnd()
        {
            yield return LoadRun();
            Player.Equip(Item("equipment.bullet-dash"));
            Script(Once(new ShipCommand { Dash = true, Move = Vector2.right }));

            yield return WaitTicks(12);

            Assert.That(World.Projectiles.CountForTeam(Team.Player), Is.GreaterThanOrEqualTo(8));
        }

        [UnityTest]
        public IEnumerator Stormbringer_LightningCoreProcs()
        {
            yield return LoadRun();
            Assert.AreEqual("ship.stormbringer", Player.Definition.Id);
            Spawn("enemy.carrier", new Vector2(0f, 9f), eliteId: "elite.armored");
            int procs = 0;
            void Record(CombatEntity target, DamageInfo info, DamageResult result)
            {
                if (info.SourceId == Player.EntityId && info.Kind == DamageKind.Proc)
                    procs++;
            }

            CombatEvents.Damaged += Record;
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });
            yield return WaitTicks(300);
            CombatEvents.Damaged -= Record;

            Assert.Greater(procs, 0, "8% per hit over ~40 hits calls lightning.");
        }

        [UnityTest]
        public IEnumerator Titan_HasReinforcedHull()
        {
            yield return LoadRun();
            Player.Initialize(Ship("ship.titan"), Player.PrimaryWeapon, Player.SecondaryWeapon);

            Assert.AreEqual(150f, Player.Vitals.MaxHull);
            Player.ApplyDamage(new DamageInfo(100f, Team.Enemy, 0, Player.Position, Vector2.up));

            // 100 × 0.9 = 90 damage: 25 to the shield, 65 to the hull.
            Assert.AreEqual(85f, Player.Vitals.Hull, 0.01f);
        }
    }
}
