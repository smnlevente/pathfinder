using System.Collections;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Enemy archetypes and elite modifiers (GDD §7.3).</summary>
    public class EnemyTests : SandboxTestBase
    {
        [UnityTest]
        public IEnumerator Gunner_ShootsAtTheShip()
        {
            yield return LoadRun();
            Spawn("enemy.gunner", new Vector2(0f, 10f));

            yield return WaitUntilTicks(() => DamageTaken > 0f, maxTicks: 360);

            Assert.Greater(DamageTaken, 0f, "A stationary ship should be hit within 6 s.");
        }

        [UnityTest]
        public IEnumerator Spinner_FiresBulletRings()
        {
            yield return LoadRun();
            Spawn("enemy.spinner", new Vector2(0f, 9f));

            yield return WaitUntilTicks(() => World.Projectiles.CountForTeam(Team.Enemy) >= 10, maxTicks: 240);

            Assert.That(World.Projectiles.CountForTeam(Team.Enemy), Is.GreaterThanOrEqualTo(10), "A ring is 10 bullets.");
        }

        [UnityTest]
        public IEnumerator Lancer_TelegraphsThenChargesIntoTheShip()
        {
            yield return LoadRun();
            var lancer = Spawn("enemy.lancer", new Vector2(0f, 8f));
            var brain = lancer.GetComponent<LancerBrain>();

            bool woundUp = false, charged = false;
            float maxSpeed = 0f, lateWindUpSpeed = 0f;
            int windUpTicks = 0;
            int deadline = World.Tick + 300;
            while (World.Tick < deadline && DamageTaken <= 0f)
            {
                woundUp |= brain.Current == LancerBrain.State.WindUp;
                if (brain.Current == LancerBrain.State.WindUp && ++windUpTicks > 10)
                    lateWindUpSpeed = Mathf.Max(lateWindUpSpeed, lancer.Velocity.magnitude);
                charged |= woundUp && brain.Current == LancerBrain.State.Charge;
                maxSpeed = Mathf.Max(maxSpeed, lancer.Velocity.magnitude);
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(woundUp, "The Lancer must telegraph before charging.");
            Assert.Less(lateWindUpSpeed, 0.5f, "The Lancer holds still while winding up, so its line stays honest.");
            Assert.IsTrue(charged);
            Assert.Greater(maxSpeed, 15f, "Charges are far faster than normal movement.");
            Assert.That(DamageTaken, Is.GreaterThanOrEqualTo(19f), "A charge hits for double contact damage (20).");
        }

        [UnityTest]
        public IEnumerator Carrier_LaunchesDronesThatDropNothing()
        {
            yield return LoadRun();
            var carrier = Spawn("enemy.carrier", new Vector2(0f, 10f));

            yield return WaitUntilTicks(() => CombatRegistry.Enemies.Count >= 3, maxTicks: 360);
            Assert.That(CombatRegistry.Enemies.Count, Is.GreaterThanOrEqualTo(3), "The Carrier should launch Drones.");

            Enemy launched = null;
            foreach (var enemy in CombatRegistry.Enemies)
            {
                if (enemy != carrier)
                    launched = enemy;
            }
            Kill(launched);
            yield return WaitTicks(1);

            Assert.AreEqual(0, World.Pickups.Count, "Launched Drones must not drop Scraps.");
        }

        [UnityTest]
        public IEnumerator Carrier_TakesDoubleDamageFromTheRear()
        {
            yield return LoadRun();
            var carrier = Spawn("enemy.carrier", new Vector2(0f, 10f));
            yield return WaitTicks(1);

            Vector2 facing = PlaneMath.FromHeading(carrier.Heading);
            float before = carrier.Vitals.Hull;
            carrier.ApplyDamage(new DamageInfo(10f, Team.Player, 0, carrier.Position + facing * carrier.Radius, -facing));
            float frontLoss = before - carrier.Vitals.Hull;

            before = carrier.Vitals.Hull;
            carrier.ApplyDamage(new DamageInfo(10f, Team.Player, 0, carrier.Position - facing * carrier.Radius, facing));
            float rearLoss = before - carrier.Vitals.Hull;

            Assert.AreEqual(6f, frontLoss, 1e-3f, "The armoured front takes 60%.");
            Assert.AreEqual(20f, rearLoss, 1e-3f, "The weak rear takes 200%.");
        }

        [UnityTest]
        public IEnumerator Elites_ScaleStatsAndAlwaysDropTripleScraps()
        {
            yield return LoadRun();
            var normal = Spawn("enemy.drone", new Vector2(-8f, 8f));
            var armored = Spawn("enemy.drone", new Vector2(8f, 8f), eliteId: "elite.armored");
            var hasty = Spawn("enemy.gunner", new Vector2(0f, 12f), eliteId: "elite.hasty");

            Assert.AreEqual(normal.Vitals.MaxHull * 2f, armored.Vitals.MaxHull, 1e-3f);
            Assert.Less(armored.MoveSpeed, normal.MoveSpeed);
            Assert.Greater(armored.Radius, normal.Radius);
            Assert.AreEqual(1.5f, hasty.AttackRate, 1e-3f);

            Kill(armored);
            yield return WaitTicks(1);
            Assert.AreEqual(3, World.Pickups.TotalAmount, "An elite Drone always drops 3× its 1 Scrap.");
        }
    }
}
