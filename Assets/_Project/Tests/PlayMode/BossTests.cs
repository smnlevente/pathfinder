using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>The Quarry Maw (GDD §7.5), in the combat sandbox.</summary>
    public class BossTests : SandboxTestBase
    {
        Enemy _boss;
        QuarryMawBrain _brain;

        IEnumerator SpawnBoss()
        {
            yield return LoadRun();
            Player.GodMode = true;
            _boss = Spawn("boss.quarry-maw", new Vector2(0f, 12f));
            _brain = _boss.GetComponent<QuarryMawBrain>();
            yield return WaitTicks(2);
        }

        void Hit(CombatEntity part, float amount) =>
            part.ApplyDamage(new DamageInfo(amount, Team.Player, Player.EntityId, part.Position, Vector2.up));

        BossSegment WeakPoint => _brain.Segments.First(s => s.IsWeakPoint);

        IEnumerator WaitUntilVulnerable(int maxTicks = 600) =>
            WaitUntilTicks(() => !_brain.IsBurrowed && !_brain.IsRoaring, maxTicks);

        [UnityTest]
        public IEnumerator OnlyWeakPointsTakeDamage()
        {
            yield return SpawnBoss();
            Assert.AreEqual(12, _brain.Segments.Count);
            Assert.AreEqual(4, _brain.Segments.Count(s => s.IsWeakPoint), "Every third segment is a weak point.");
            Assert.AreEqual(5500f, _boss.Vitals.MaxHull);
            yield return WaitUntilVulnerable();

            var armoured = _brain.Segments.First(s => !s.IsWeakPoint);
            Assert.IsTrue(armoured.Armoured && _boss.Armoured && !WeakPoint.Armoured);

            float hull = _boss.Vitals.Hull;
            Hit(armoured, 100f);
            Hit(_boss, 100f);
            Assert.AreEqual(hull, _boss.Vitals.Hull, "The shell and the head deflect everything.");

            Hit(WeakPoint, 100f);
            Assert.AreEqual(hull - 100f, _boss.Vitals.Hull, 0.01f, "Weak points take full damage, into the shared pool.");
        }

        [UnityTest]
        public IEnumerator ArmourDeflectsProjectiles_AndSeekersFindWeakPoints()
        {
            yield return SpawnBoss();
            yield return WaitUntilVulnerable();
            // The first segment, shot square from the side: well away from any weak point along the body.
            var armoured = _brain.Segments[0];
            Vector2 axis = (_brain.Segments[1].Position - armoured.Position).normalized;
            Vector2 side = new Vector2(-axis.y, axis.x);

            float hull = _boss.Vitals.Hull;
            Vector2 from = armoured.Position - side * 3f;
            World.Projectiles.Spawn(new ProjectileSpawn(from, side, 40f, 50f, 0.2f, 10f, Team.Player, Player.EntityId, null, pierce: 3));
            yield return WaitTicks(10);
            Assert.AreEqual(hull, _boss.Vitals.Hull, "Even piercing shots stop at the armour.");

            Player.EquipSecondary(SecondaryWeapon("secondary.seeker-swarm"));
            Script(Once(new ShipCommand { Aim = Vector2.up, FireSecondary = true }));
            yield return WaitUntilTicks(() => _boss.Vitals.Hull < hull, 300);
            Assert.Less(_boss.Vitals.Hull, hull, "Seekers home in on the weak points, not the shell.");
        }

        [UnityTest]
        public IEnumerator Burrowed_IsInvulnerableAndUntouchable()
        {
            yield return SpawnBoss();
            yield return WaitUntilTicks(() => _brain.IsBurrowed, 400);
            Assert.IsTrue(_brain.IsBurrowed, "Phase 1 dives underground.");

            float hull = _boss.Vitals.Hull;
            Hit(WeakPoint, 500f);

            Assert.AreEqual(hull, _boss.Vitals.Hull, "Underground, it can't be hurt.");
            Assert.IsTrue(_boss.IsIntangible);
            Assert.IsTrue(_brain.Segments.All(s => s.IsIntangible));
        }

        [UnityTest]
        public IEnumerator Eruption_HitsTheShipAtTheMarkedSpot()
        {
            yield return SpawnBoss();
            Player.GodMode = false;

            yield return WaitUntilTicks(() => DamageTaken > 0f, 600);

            Assert.Greater(DamageTaken, 0f, "A stationary ship is caught by the eruption, its rocks or the aimed bursts.");
        }

        [UnityTest]
        public IEnumerator Phases_ChangeAtTwoThirdsAndOneThird()
        {
            yield return SpawnBoss();
            Assert.AreEqual(QuarryMawBrain.Phase.Burrow, _brain.CurrentPhase);

            yield return WaitUntilVulnerable();
            Hit(WeakPoint, 2100f); // 3400 / 5500 = 62%
            yield return WaitUntilTicks(() => _brain.CurrentPhase == QuarryMawBrain.Phase.Sweep, 600);
            Assert.AreEqual(QuarryMawBrain.Phase.Sweep, _brain.CurrentPhase);
            Assert.IsTrue(_brain.IsRoaring, "Each phase opens with a roar.");
            Assert.IsTrue(WeakPoint.Armoured, "Roaring, even the weak points are shut.");

            yield return WaitUntilVulnerable();
            Hit(WeakPoint, 1600f); // 1800 / 5500 = 33%
            yield return WaitUntilTicks(() => _brain.CurrentPhase == QuarryMawBrain.Phase.Enrage, 600);
            Assert.AreEqual(QuarryMawBrain.Phase.Enrage, _brain.CurrentPhase);

            yield return WaitUntilVulnerable();
            Assert.IsTrue(_brain.IsMawExposed);
            Assert.IsFalse(_boss.Armoured, "Enraged, the cracked maw can be hit.");
            yield return WaitUntilTicks(() => World.Projectiles.CountForTeam(Team.Enemy) >= 5, 300);
            Assert.That(World.Projectiles.CountForTeam(Team.Enemy), Is.GreaterThanOrEqualTo(5), "Enraged, it spews bullet streams.");
        }

        [UnityTest]
        public IEnumerator Sweep_TelegraphsALaneThroughTheShip_AndShedsRockWalls()
        {
            yield return SpawnBoss();
            yield return WaitUntilVulnerable();
            Hit(WeakPoint, 2100f);
            var telegraph = (IChargeTelegraph)_brain;

            yield return WaitUntilTicks(() => telegraph.ShowsCharge, 400);
            Assert.IsTrue(telegraph.ShowsCharge, "Each charge shows its lane first.");

            // The lane passes through the ship's position.
            Vector2 toShip = Player.Position - telegraph.ChargeStart;
            float offLane = Mathf.Abs(toShip.x * telegraph.ChargeDirection.y - toShip.y * telegraph.ChargeDirection.x);
            Assert.Less(offLane, 0.6f, "The charge is aimed through the ship.");
            Assert.Greater(telegraph.ChargeLength, 10f);

            yield return WaitUntilTicks(() => !telegraph.ShowsCharge, 120);
            int before = World.Projectiles.CountForTeam(Team.Enemy);
            yield return WaitTicks(90);
            Assert.Greater(World.Projectiles.CountForTeam(Team.Enemy), before + 10, "Charging, it sheds walls of rocks (a pair every 0.18 s).");
        }

        [UnityTest]
        public IEnumerator Defeat_DropsLootAndShattersTheBody()
        {
            yield return SpawnBoss();
            yield return WaitUntilVulnerable();

            Hit(WeakPoint, 100_000f); // the killing blow lands on a segment
            yield return null;
            yield return null;

            Assert.IsTrue(_boss == null || !_boss.IsAlive);
            Assert.AreEqual(0, CombatRegistry.OnTeam(Team.Enemy).Count, "The whole body goes with the head.");
            Assert.AreEqual(30, World.Pickups.TotalAmount, "The boss drops 30 Scraps.");
        }

        [UnityTest]
        public IEnumerator DefusedBullets_AreHarmlessAndFadeOut()
        {
            yield return LoadRun();
            for (int i = 0; i < 6; i++)
                SpawnEnemyBullet(Player.Position + new Vector2(-1.5f + i * 0.6f, 2f), Vector2.down, speed: 8f, damage: 10f);

            World.Projectiles.Defuse(Team.Enemy);
            yield return WaitTicks(45);

            Assert.AreEqual(0f, DamageTaken, "Defused bullets can't hurt.");
            Assert.AreEqual(0, World.Projectiles.CountForTeam(Team.Enemy), "They fade out within half a second.");
        }

        /// <summary>
        /// Balance check: a god-mode bot with the default loadout and perfect aim at whatever is exposed. Logs the
        /// time to kill; real fights take longer (dodging, misses) but pilots also carry equipment.
        /// </summary>
        [UnityTest]
        public IEnumerator Balance_TimeToKill()
        {
            yield return SpawnBoss();
            Player.PlaceForLevel(new Vector2(0f, -8f));

            Script(_ =>
            {
                CombatEntity best = null;
                float bestDistance = float.MaxValue;
                foreach (var enemy in CombatRegistry.OnTeam(Team.Enemy))
                {
                    float distance = (enemy.Position - Player.Position).sqrMagnitude;
                    if (enemy.IsAlive && !enemy.Armoured && !enemy.IsIntangible && distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = enemy;
                    }
                }
                Vector2 aim = best != null ? (best.Position - Player.Position).normalized : Vector2.up;
                return new ShipCommand { Aim = aim, FirePrimary = best != null, FireSecondary = best != null };
            });

            float timeScale = Time.timeScale;
            Time.timeScale = 5f;
            int start = World.Tick;
            yield return WaitUntilTicks(() => _boss == null || !_boss.IsAlive, 60 * 400);
            Time.timeScale = timeScale;

            float seconds = (World.Tick - start) / 60f;
            Debug.Log($"[Balance] Quarry Maw time to kill (perfect-aim bot, default loadout): {seconds:F0} s");
            Assert.IsTrue(_boss == null || !_boss.IsAlive, "The bot must be able to kill it.");
            Assert.That(seconds, Is.InRange(60f, 240f), "A boss fight should last minutes, not seconds or forever.");
        }
    }
}
