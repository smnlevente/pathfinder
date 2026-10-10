using System.Collections;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>The galaxy 2 enemies: Scorcher, Arc Drone and Crusher (GDD §7.3).</summary>
    public class GalaxyTwoEnemyTests : SandboxTestBase
    {
        [UnityTest]
        public IEnumerator Scorcher_MarksItsConeThenBurnsTheShip_Quietly()
        {
            yield return LoadRun();
            var scorcher = Spawn("enemy.scorcher", new Vector2(0f, 4f));
            var brain = scorcher.GetComponent<ScorcherBrain>();
            var area = (IAreaTelegraph)brain;

            int shotEvents = 0;
            void OnFired(Team team, Vector2 position) => shotEvents += team == Team.Enemy ? 1 : 0;
            CombatEvents.ProjectileFired += OnFired;
            try
            {
                yield return WaitUntilTicks(() => brain.Current == ScorcherBrain.State.WindUp, 240);
                Assert.AreEqual(ScorcherBrain.State.WindUp, brain.Current, "Close up, it winds up.");
                Assert.IsTrue(area.ShowsArea && area.AreaArc < 90f, "The wind-up marks a cone on the floor.");

                yield return WaitUntilTicks(() => DamageTaken > 0f, 120);
                Assert.Greater(DamageTaken, 0f, "A ship standing in the cone burns.");
                Assert.That(World.Projectiles.CountForTeam(Team.Enemy), Is.GreaterThanOrEqualTo(3), "It sprays a stream of flames.");
                Assert.AreEqual(0, shotEvents, "Flames don't make the bullet sound; the Scorcher plays its own roar.");
            }
            finally
            {
                CombatEvents.ProjectileFired -= OnFired;
            }
        }

        [UnityTest]
        public IEnumerator ArcDrone_LocksALine_ThenZapsAlongIt()
        {
            yield return LoadRun();
            var drone = Spawn("enemy.arc-drone", new Vector2(0f, 7f));
            var brain = drone.GetComponent<ArcDroneBrain>();
            var beam = (IBeamTelegraph)brain;

            yield return WaitUntilTicks(() => brain.Current == ArcDroneBrain.State.Charge, 300);
            Assert.IsTrue(beam.ShowsBeam, "It shows the line before it fires.");
            Vector2 toShip = (Player.Position - beam.BeamStart).normalized;
            Assert.Greater(Vector2.Dot(toShip, (beam.BeamEnd - beam.BeamStart).normalized), 0.98f, "The line runs through the ship.");

            yield return WaitUntilTicks(() => DamageTaken > 0f, 60);
            Assert.AreEqual(14f, DamageTaken, 2f, "A ship that stays on the line takes the bolt.");
        }

        [UnityTest]
        public IEnumerator ArcDrone_MissesAShipThatStepsOffTheLine()
        {
            yield return LoadRun();
            var drone = Spawn("enemy.arc-drone", new Vector2(0f, 7f));
            var brain = drone.GetComponent<ArcDroneBrain>();

            yield return WaitUntilTicks(() => brain.Current == ArcDroneBrain.State.Charge, 300);
            Vector2 side = new Vector2(-brain.LockedDirection.y, brain.LockedDirection.x);
            Player.PlaceForLevel(Player.Position + side * 3f);
            yield return WaitUntilTicks(() => brain.Current == ArcDroneBrain.State.Hover, 60);

            Assert.AreEqual(ArcDroneBrain.State.Hover, brain.Current, "It fired.");
            Assert.AreEqual(0f, DamageTaken, "The bolt follows the locked line, not the ship.");
        }

        [UnityTest]
        public IEnumerator Crusher_SlamsHardUpClose()
        {
            yield return LoadRun();
            var crusher = Spawn("enemy.crusher", new Vector2(0f, 2.3f));
            var brain = crusher.GetComponent<CrusherBrain>();

            yield return WaitUntilTicks(() => brain.Current == CrusherBrain.State.WindUp, 120);
            Assert.IsTrue(((IAreaTelegraph)brain).ShowsArea, "It marks its reach before it slams.");
            Assert.AreEqual(0f, DamageTaken, "Nothing lands during the wind-up.");

            yield return WaitUntilTicks(() => DamageTaken > 0f, 60);
            Assert.Greater(DamageTaken, 30f, "The slam hits very hard.");
            Assert.AreEqual(CrusherBrain.State.Recover, brain.Current, "Then it needs a moment.");
        }

        /// <summary>Each new enemy mid-attack, for visual review: the flame cone, the charging line and bolt, the slam ring.</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureGalaxyTwoEnemies()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun();
            Player.GodMode = true;

            var scorcher = Spawn("enemy.scorcher", new Vector2(3f, 3f)).GetComponent<ScorcherBrain>();
            yield return WaitUntilTicks(() => scorcher.Current == ScorcherBrain.State.WindUp, 300);
            yield return WaitTicks(25);
            yield return null;
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "enemy_scorcher_windup.png"));
            yield return WaitUntilTicks(() => scorcher.Current == ScorcherBrain.State.Spray, 60);
            yield return WaitTicks(40);
            yield return null;
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "enemy_scorcher_spray.png"));
            Kill(scorcher.GetComponent<Enemy>());

            var drone = Spawn("enemy.arc-drone", new Vector2(-5f, 5f)).GetComponent<ArcDroneBrain>();
            yield return WaitUntilTicks(() => drone.Current == ArcDroneBrain.State.Charge, 300);
            yield return WaitTicks(30);
            yield return null;
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "enemy_arcdrone_charge.png"));
            yield return WaitUntilTicks(() => drone.Current == ArcDroneBrain.State.Hover, 60);
            yield return null;
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "enemy_arcdrone_zap.png"));
            Kill(drone.GetComponent<Enemy>());

            var crusher = Spawn("enemy.crusher", new Vector2(1.5f, 2f)).GetComponent<CrusherBrain>();
            yield return WaitUntilTicks(() => crusher.Current == CrusherBrain.State.WindUp, 120);
            yield return WaitTicks(22);
            yield return null;
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "enemy_crusher_windup.png"));
        }

        [UnityTest]
        public IEnumerator Crusher_CanBeOutrun_AndShrugsOffKnockback()
        {
            yield return LoadRun();
            // Just inside its reach: a pilot who moves away the moment the ring appears gets clear of the lunge.
            var crusher = Spawn("enemy.crusher", new Vector2(0f, 3.1f));
            var brain = crusher.GetComponent<CrusherBrain>();
            Script(_ => new ShipCommand { Move = brain.Current == CrusherBrain.State.WindUp ? Vector2.down : Vector2.zero });

            yield return WaitUntilTicks(() => brain.Current == CrusherBrain.State.Recover, 200);
            Assert.AreEqual(CrusherBrain.State.Recover, brain.Current, "It slammed.");
            Assert.AreEqual(0f, DamageTaken, "A pilot who moves away at once escapes the slam.");

            float x = crusher.Position.x;
            crusher.ApplyKnockback(Vector2.right * 20f);
            yield return WaitTicks(20);
            Assert.Less(Mathf.Abs(crusher.Position.x - x), 0.6f, "Knockback barely moves it (a Drone would slide 2.5 u).");
        }
    }
}
