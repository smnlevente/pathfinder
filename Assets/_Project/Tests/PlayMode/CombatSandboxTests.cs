using System.Collections;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Core ship feel and run flow (M1).</summary>
    public class CombatSandboxTests : SandboxTestBase
    {
        [UnityTest]
        public IEnumerator Ship_ReachesTopSpeedAlmostInstantly()
        {
            yield return LoadRun();
            float startX = Player.Position.x;
            Script(_ => new ShipCommand { Move = Vector2.right });

            yield return WaitTicks(60);

            // 8 u/s for 1 s, minus ~0.3 u lost to the 0.08 s acceleration ramp.
            Assert.That(Player.Position.x - startX, Is.InRange(7.4f, 8.0f));
        }

        [UnityTest]
        public IEnumerator Ship_StaysInsideTheArena()
        {
            yield return LoadRun();
            Script(_ => new ShipCommand { Move = new Vector2(-1f, -1f) });

            yield return WaitTicks(400);

            Vector2 limit = World.ArenaSize * 0.5f - Vector2.one * Player.Radius;
            Assert.AreEqual(-limit.x, Player.Position.x, 1e-3f);
            Assert.AreEqual(-limit.y, Player.Position.y, 1e-3f);
        }

        [UnityTest]
        public IEnumerator PhotonRepeater_DestroysAnApproachingDrone()
        {
            yield return LoadRun();
            var drone = Spawn("enemy.drone", new Vector2(0f, 8f));
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });

            yield return WaitUntilTicks(() => IsDead(drone), maxTicks: 180);

            Assert.IsTrue(IsDead(drone), "Drone survived 3 s of fire.");
            Assert.AreEqual(Player.Vitals.MaxShield, Player.Vitals.Shield, "The drone should die before it reaches the ship.");
            var magazine = ((AutomaticPrimary)Player.Primary).Magazine;
            Assert.Less(magazine.Ammo, magazine.MagazineSize);
        }

        [UnityTest]
        public IEnumerator Dash_GrantsIFrames_ThenDamageResumes()
        {
            yield return LoadRun();
            Script(Once(new ShipCommand { Dash = true }));

            yield return WaitTicks(1);
            Assert.IsTrue(Player.IsIntangible, "Ship should be dashing.");

            SpawnEnemyBullet(Player.Position, Vector2.right);
            yield return WaitTicks(2);
            Assert.AreEqual(Player.Vitals.MaxShield, Player.Vitals.Shield, "Bullets must pass through a dashing ship.");

            yield return WaitUntilTicks(() => !Player.IsIntangible, maxTicks: 30);
            SpawnEnemyBullet(Player.Position, Vector2.right);
            yield return WaitTicks(2);
            Assert.Less(Player.Vitals.Shield, Player.Vitals.MaxShield, "After the dash the ship takes damage again.");
        }

        [UnityTest]
        public IEnumerator Dash_CoversItsFullDistance()
        {
            yield return LoadRun();
            Vector2 start = Player.Position;
            Script(Once(new ShipCommand { Dash = true, Move = Vector2.right }, new ShipCommand { Move = Vector2.right }));

            yield return WaitTicks(1);
            yield return WaitUntilTicks(() => !Player.IsIntangible, maxTicks: 30);

            // 5 u dash. A tick of normal movement after it may add up to ~0.15 u.
            Assert.That(Player.Position.x - start.x, Is.InRange(4.9f, 5.3f));
        }

        [UnityTest]
        public IEnumerator LethalDamage_EndsTheRun()
        {
            yield return LoadRun();
            bool raised = false;
            World.AllPlayersDown += () => raised = true;

            Player.ApplyDamage(new DamageInfo(10_000f, Team.Enemy, 0, Player.Position, Vector2.up));
            yield return WaitTicks(1);

            Assert.IsFalse(Player.IsAlive);
            Assert.IsTrue(raised);
            Assert.IsTrue(Object.FindAnyObjectByType<RunFlow>().IsGameOver);
        }

        [UnityTest]
        public IEnumerator GodMode_TogglesImmediately()
        {
            yield return LoadRun();
            Player.GodMode = true;
            Assert.IsTrue(Player.Vitals.IsInvulnerable, "No tick needed to turn it on.");

            yield return WaitTicks(2);
            Player.GodMode = false;
            Player.ApplyDamage(new DamageInfo(10f, Team.Enemy, 0, Player.Position, Vector2.up));

            Assert.Less(Player.Vitals.Hull + Player.Vitals.Shield, Player.Vitals.MaxHull + Player.Vitals.MaxShield, "Nor to turn it off.");
        }
    }
}
