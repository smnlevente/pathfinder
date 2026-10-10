using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
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

        IEnumerator WaitUntilVulnerable(int maxTicks = 600) =>
            WaitUntilTicks(() => !_brain.IsBurrowed && !_brain.IsRoaring, maxTicks);

        [UnityTest]
        public IEnumerator Body_SegmentsShareHealth_WeakPointsTakeDouble()
        {
            yield return SpawnBoss();
            Assert.AreEqual(10, _brain.Segments.Count);
            Assert.AreEqual(2000f, _boss.Vitals.MaxHull);

            var armoured = _brain.Segments.First(s => !s.IsWeakPoint);
            var weak = _brain.Segments.First(s => s.IsWeakPoint);

            float hull = _boss.Vitals.Hull;
            Hit(armoured, 100f);
            Assert.AreEqual(hull - 50f, _boss.Vitals.Hull, 0.01f, "Armoured segments take half.");

            hull = _boss.Vitals.Hull;
            Hit(weak, 100f);
            Assert.AreEqual(hull - 200f, _boss.Vitals.Hull, 0.01f, "Weak points take double.");
        }

        [UnityTest]
        public IEnumerator Burrowed_IsInvulnerableAndUntouchable()
        {
            yield return SpawnBoss();
            yield return WaitUntilTicks(() => _brain.IsBurrowed, 400);
            Assert.IsTrue(_brain.IsBurrowed, "Phase 1 dives underground.");

            float hull = _boss.Vitals.Hull;
            Hit(_boss, 500f);
            Hit(_brain.Segments[0], 500f);

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

            Assert.Greater(DamageTaken, 0f, "A stationary ship is caught by the eruption or its rock shower.");
        }

        [UnityTest]
        public IEnumerator Phases_ChangeAtTwoThirdsAndOneThird()
        {
            yield return SpawnBoss();
            Assert.AreEqual(QuarryMawBrain.Phase.Burrow, _brain.CurrentPhase);

            yield return WaitUntilVulnerable();
            Hit(_boss, 750f); // 1250 / 2000 = 62.5%
            yield return WaitUntilTicks(() => _brain.CurrentPhase == QuarryMawBrain.Phase.Sweep, 600);
            Assert.AreEqual(QuarryMawBrain.Phase.Sweep, _brain.CurrentPhase);
            Assert.IsTrue(_brain.IsRoaring, "Each phase opens with a roar.");

            yield return WaitUntilVulnerable();
            Hit(_boss, 700f); // 550 / 2000 = 27.5%
            yield return WaitUntilTicks(() => _brain.CurrentPhase == QuarryMawBrain.Phase.Enrage, 600);
            Assert.AreEqual(QuarryMawBrain.Phase.Enrage, _brain.CurrentPhase);

            yield return WaitUntilVulnerable();
            yield return WaitUntilTicks(() => World.Projectiles.CountForTeam(Team.Enemy) >= 5, 300);
            Assert.That(World.Projectiles.CountForTeam(Team.Enemy), Is.GreaterThanOrEqualTo(5), "Enraged, it spews bullet streams.");
        }

        [UnityTest]
        public IEnumerator Defeat_DropsLootAndShattersTheBody()
        {
            yield return SpawnBoss();
            yield return WaitUntilVulnerable();

            Hit(_brain.Segments.First(s => s.IsWeakPoint), 5000f); // the killing blow lands on a segment
            yield return null;
            yield return null;

            Assert.IsTrue(_boss == null || !_boss.IsAlive);
            Assert.AreEqual(0, CombatRegistry.OnTeam(Team.Enemy).Count, "The whole body goes with the head.");
            Assert.AreEqual(30, World.Pickups.TotalAmount, "The boss drops 30 Scraps.");
        }
    }
}
