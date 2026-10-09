using System.Collections;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Primary and secondary weapons (GDD §6).</summary>
    public class WeaponTests : SandboxTestBase
    {
        [UnityTest]
        public IEnumerator PlasmaCutter_ShredsCloseTargets()
        {
            yield return LoadRun();
            Player.EquipPrimary(PrimaryWeapon("primary.plasma-cutter"));
            var drone = Spawn("enemy.drone", new Vector2(0f, 3f));
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });

            yield return WaitUntilTicks(() => IsDead(drone), maxTicks: 40);

            Assert.IsTrue(IsDead(drone), "One or two close-range blasts should kill a Drone.");
        }

        [UnityTest]
        public IEnumerator PlasmaCutter_FallsShortAtLongRange()
        {
            yield return LoadRun();
            Player.EquipPrimary(PrimaryWeapon("primary.plasma-cutter"));
            var spinner = Spawn("enemy.spinner", new Vector2(0f, 12f));
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });

            yield return WaitTicks(60);

            Assert.AreEqual(spinner.Vitals.MaxHull, spinner.Vitals.Hull, "Pellets only reach 7 u.");
        }

        [UnityTest]
        public IEnumerator QuantumCharger_FullChargePiercesALine()
        {
            yield return LoadRun();
            Player.EquipPrimary(PrimaryWeapon("primary.quantum-charger"));
            bool release = false;
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = !release });

            yield return WaitTicks(90); // 1.5 s = full charge
            Assert.That(Player.Primary.Status.Charge, Is.EqualTo(1f).Within(0.02f));

            // Line up two Spinners (60 HP each) just before release, so they can't drift out of line.
            var near = Spawn("enemy.spinner", new Vector2(0f, 6f));
            var far = Spawn("enemy.spinner", new Vector2(0f, 9f));
            release = true;

            yield return WaitUntilTicks(() => IsDead(near) && IsDead(far), maxTicks: 40);

            Assert.IsTrue(IsDead(near) && IsDead(far), "A full 120-damage piercing shot should kill both.");
        }

        [UnityTest]
        public IEnumerator QuantumCharger_OverheatsWhenHeldTooLong()
        {
            yield return LoadRun();
            Player.EquipPrimary(PrimaryWeapon("primary.quantum-charger"));
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });

            bool overheated = false;
            int deadline = World.Tick + 240;
            while (World.Tick < deadline)
            {
                overheated |= Player.Primary.Status.Label == "OVERHEATED";
                Assert.AreEqual(0, World.Projectiles.CountForTeam(Team.Player), "An overheated charge must not fire.");
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(overheated);
        }

        [UnityTest]
        public IEnumerator SeekerSwarm_HomesOntoOffAxisTargets()
        {
            yield return LoadRun();
            Player.EquipSecondary(SecondaryWeapon("secondary.seeker-swarm"));
            var gunner = Spawn("enemy.gunner", new Vector2(9f, 0f)); // off to the side; the ship aims up
            Script(Once(new ShipCommand { Aim = Vector2.up, FireSecondary = true }, new ShipCommand { Aim = Vector2.up }));

            yield return WaitUntilTicks(() => IsDead(gunner), maxTicks: 150);

            Assert.IsTrue(IsDead(gunner), "Six 20-damage missiles should home in and kill a 35 HP Gunner.");
            Assert.IsFalse(Player.SecondaryCooldown.IsReady, "The swarm is on cooldown after firing.");
        }

        [UnityTest]
        public IEnumerator NovaPulse_ErasesBulletsDamagesAndKnocksBack()
        {
            yield return LoadRun();
            Player.EquipSecondary(SecondaryWeapon("secondary.nova-pulse"));
            for (int i = 0; i < 6; i++)
                SpawnEnemyBullet(Player.Position + Pathfinder.Game.Simulation.PlaneMath.FromHeading(i * 60f) * 2f, Vector2.right, speed: 0.1f);
            var drone = Spawn("enemy.drone", new Vector2(3f, 0f));
            var spinner = Spawn("enemy.spinner", new Vector2(-3.5f, 0f));
            float spinnerDistance = Vector2.Distance(spinner.Position, Player.Position);

            Script(Once(new ShipCommand { FireSecondary = true }));
            yield return WaitTicks(2);

            Assert.AreEqual(0, World.Projectiles.CountForTeam(Team.Enemy), "Nova Pulse erases enemy bullets in range.");
            Assert.IsTrue(IsDead(drone), "40 damage kills a 15 HP Drone.");
            Assert.Less(spinner.Vitals.Hull, spinner.Vitals.MaxHull);

            yield return WaitTicks(10);
            Assert.Greater(Vector2.Distance(spinner.Position, Player.Position), spinnerDistance + 0.5f, "Survivors are knocked back.");
        }
    }
}
