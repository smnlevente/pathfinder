using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>The Swarm's creatures in the Veil Nebula (M7): Spore Drifter, Stinger and Spitter.</summary>
    public class SwarmEnemyTests : SandboxTestBase
    {
        [UnityTest]
        public IEnumerator SporeDrifter_BurstsIntoACloud_ThatSlowsAndBurns_ThenFades()
        {
            yield return LoadRun();
            var drifter = Spawn("enemy.spore-drifter", new Vector2(0f, 1.5f));
            float radius = drifter.GetComponent<SporeDrifterBrain>().CloudRadius;
            yield return WaitTicks(1);
            Vector2 at = drifter.Position;
            Kill(drifter);

            Assert.AreEqual(1, World.Clouds.Count, "It bursts into a cloud when it dies.");
            Assert.Less(Vector2.Distance(World.Clouds.Positions.First(), at), radius, "The cloud is where it died.");
            float before = DamageTaken;
            yield return WaitTicks(40);
            Assert.IsTrue(Player.IsSlowed, "A ship in the cloud is slowed.");
            Assert.Greater(DamageTaken, before, "And burns.");

            var despawned = Spawn("enemy.spore-drifter", new Vector2(8f, 0f));
            yield return WaitTicks(1);
            despawned.Despawn();
            Assert.AreEqual(1, World.Clouds.Count, "A level ending (a despawn) leaves no cloud.");

            yield return WaitUntilTicks(() => World.Clouds.Count == 0, 360);
            Assert.AreEqual(0, World.Clouds.Count, "The cloud fades after a few seconds.");
        }

        [UnityTest]
        public IEnumerator SporeDrifter_CloudsGoWhenTheLevelIsCleared()
        {
            yield return LoadRun();
            var drifter = Spawn("enemy.spore-drifter", new Vector2(6f, 0f));
            yield return WaitTicks(1);
            Kill(drifter);
            Assert.AreEqual(1, World.Clouds.Count);
            World.ClearLevel(Player);
            Assert.AreEqual(0, World.Clouds.Count);
        }

        [UnityTest]
        public IEnumerator Stinger_CirclesTheShip_ThenDashesThroughIt()
        {
            yield return LoadRun();
            var stinger = Spawn("enemy.stinger", new Vector2(0f, 6f));
            var brain = stinger.GetComponent<StingerBrain>();
            var charge = (IChargeTelegraph)brain;
            float last = PlaneMath.HeadingDegrees(stinger.Position - Player.Position), swept = 0f;
            bool WindingUp()
            {
                float angle = PlaneMath.HeadingDegrees(stinger.Position - Player.Position);
                swept += Mathf.Abs(Mathf.DeltaAngle(last, angle));
                last = angle;
                return brain.Current == StingerBrain.State.WindUp;
            }

            yield return WaitUntilTicks(WindingUp, 360);
            Assert.AreEqual(StingerBrain.State.WindUp, brain.Current, "It winds up a dash.");
            Assert.Greater(swept, 45f, "First it circles the ship.");
            Assert.IsTrue(charge.ShowsCharge, "The dash is shown as a lane.");
            Vector2 toShip = (Player.Position - charge.ChargeStart).normalized;
            Assert.Greater(Vector2.Dot(toShip, charge.ChargeDirection), 0.98f, "The lane runs through the ship.");
            Assert.Greater(charge.ChargeLength, Vector2.Distance(charge.ChargeStart, Player.Position), "And past it.");
            Assert.AreEqual(0f, DamageTaken, "Nothing lands during the wind-up.");

            yield return WaitUntilTicks(() => DamageTaken > 0f, 90);
            Assert.Greater(DamageTaken, 9f, "A ship that stays on the lane is stung, harder than a touch.");
            yield return WaitUntilTicks(() => brain.Current == StingerBrain.State.Recover, 30);
            Assert.AreEqual(StingerBrain.State.Recover, brain.Current, "Then it needs a moment.");
        }

        [UnityTest]
        public IEnumerator Stinger_MissesAShipThatStepsOffTheLane()
        {
            yield return LoadRun();
            var stinger = Spawn("enemy.stinger", new Vector2(0f, 6f));
            var brain = stinger.GetComponent<StingerBrain>();

            yield return WaitUntilTicks(() => brain.Current == StingerBrain.State.WindUp, 360);
            Vector2 side = new Vector2(-brain.ChargeDirection.y, brain.ChargeDirection.x);
            Player.PlaceForLevel(Player.Position + side * 3f);
            yield return WaitUntilTicks(() => brain.Current == StingerBrain.State.Recover, 90);

            Assert.AreEqual(StingerBrain.State.Recover, brain.Current, "It dashed.");
            Assert.AreEqual(0f, DamageTaken, "The dash follows the lane, not the ship.");
        }

        [UnityTest]
        public IEnumerator Spitter_LobsAGlob_ThatHurtsUnderIt_AndBurstsIntoThorns()
        {
            yield return LoadRun();
            var spitter = Spawn("enemy.spitter", new Vector2(0f, 9f));
            var brain = spitter.GetComponent<SpitterBrain>();

            yield return WaitUntilTicks(() => brain.GlobsInFlight > 0, 420);
            Assert.IsTrue(brain.TryGetIncomingGlob(out var target, out _), "It lobs a glob.");
            Assert.Less(Vector2.Distance(target, Player.Position), 0.5f, "Aimed where the ship is.");
            Assert.AreEqual(0, World.Projectiles.CountForTeam(Team.Enemy), "Nothing is flying at the ship yet: the glob is a landing mark.");

            yield return WaitUntilTicks(() => brain.GlobsInFlight == 0, 90);
            Assert.AreEqual(12f, DamageTaken, 0.5f, "A ship still under it when it lands is hit, by the glob only.");
            Assert.That(World.Projectiles.CountForTeam(Team.Enemy), Is.GreaterThanOrEqualTo(5), "It bursts into a ring of thorns.");
        }

        [UnityTest]
        public IEnumerator Spitter_MissesAShipThatMovesAway()
        {
            yield return LoadRun();
            var spitter = Spawn("enemy.spitter", new Vector2(0f, 9f));
            var brain = spitter.GetComponent<SpitterBrain>();

            yield return WaitUntilTicks(() => brain.GlobsInFlight > 0, 420);
            Player.PlaceForLevel(Player.Position + new Vector2(12f, -2f)); // beyond the thorns' reach
            yield return WaitUntilTicks(() => brain.GlobsInFlight == 0, 90);
            yield return WaitTicks(90); // the thorns fly out and away

            Assert.AreEqual(0f, DamageTaken, "A ship that moves off the mark is missed by the glob and clear of the thorns.");
        }

        /// <summary>Each Swarm creature and its attack, for visual review.</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureSwarmEnemies()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun();
            Player.GodMode = true;

            var drifters = new[] { Spawn("enemy.spore-drifter", new Vector2(-6f, 4f)), Spawn("enemy.spore-drifter", new Vector2(-3f, -4f)) };
            var stinger = Spawn("enemy.stinger", new Vector2(6f, 4f)).GetComponent<StingerBrain>();
            var spitter = Spawn("enemy.spitter", new Vector2(2f, 9f)).GetComponent<SpitterBrain>();
            yield return WaitTicks(20);
            Kill(drifters[1]);
            yield return FreezeWhen(() => stinger.Current == StingerBrain.State.WindUp && ((IChargeTelegraph)stinger).ChargeProgress > 0.5f, 400);
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "enemy_swarm_stinger.png"));
            GameClock.Resume(this);

            yield return FreezeWhen(() => spitter.TryGetIncomingGlob(out _, out float progress) && progress > 0.6f, 420);
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "enemy_swarm_spitter.png"));
            GameClock.Resume(this);

            int landed = -1;
            yield return FreezeWhen(() =>
            {
                if (landed < 0 && spitter.GlobsInFlight == 0)
                    landed = World.Tick;
                return landed >= 0 && World.Tick - landed >= 10;
            }, 120);
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "enemy_swarm_thorns.png"));
            GameClock.Resume(this);
        }

        /// <summary>
        /// Waits for a moment in the simulation and pauses the game clock right on it, then lets a frame render, so a
        /// screenshot shows that moment however fast the test clock runs. Resume the clock after the capture.
        /// </summary>
        IEnumerator FreezeWhen(System.Func<bool> condition, int maxTicks)
        {
            yield return WaitUntilTicks(() =>
            {
                if (!condition())
                    return false;
                GameClock.Pause(this);
                return true;
            }, maxTicks);
            yield return null;
        }
    }
}
