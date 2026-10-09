using NUnit.Framework;
using Pathfinder.Core.Combat;

namespace Pathfinder.Tests.Combat
{
    public class VitalsTests
    {
        static Vitals NewShip() => new Vitals(maxHull: 100f, maxShield: 25f)
        {
            ShieldRegenRate = 10f,
            ShieldRegenDelay = 3f,
            HitInvulnerability = 0.5f,
        };

        [Test]
        public void Damage_HitsShieldBeforeHull()
        {
            var vitals = NewShip();

            var result = vitals.ApplyDamage(30f);

            Assert.AreEqual(25f, result.ShieldDamage);
            Assert.AreEqual(5f, result.HullDamage);
            Assert.AreEqual(0f, vitals.Shield);
            Assert.AreEqual(95f, vitals.Hull);
        }

        [Test]
        public void ShieldOnlyHit_DoesNotGrantInvulnerability()
        {
            var vitals = NewShip();

            vitals.ApplyDamage(10f);

            Assert.IsFalse(vitals.IsInvulnerable);
            Assert.AreEqual(10f, vitals.ApplyDamage(10f).ShieldDamage);
        }

        [Test]
        public void HullHit_GrantsInvulnerabilityThatExpires()
        {
            var vitals = new Vitals(100f) { HitInvulnerability = 0.5f };

            vitals.ApplyDamage(10f);
            Assert.IsTrue(vitals.ApplyDamage(10f).Blocked);
            Assert.AreEqual(90f, vitals.Hull);

            vitals.Tick(0.5f);
            Assert.IsFalse(vitals.ApplyDamage(10f).Blocked);
            Assert.AreEqual(80f, vitals.Hull);
        }

        [Test]
        public void ExternalInvulnerability_BlocksDamage()
        {
            var vitals = NewShip();
            vitals.ExternalInvulnerability = true;

            var result = vitals.ApplyDamage(50f);

            Assert.IsTrue(result.Blocked);
            Assert.AreEqual(25f, vitals.Shield);
            Assert.AreEqual(100f, vitals.Hull);
        }

        [Test]
        public void Shield_RegeneratesOnlyAfterDelay()
        {
            var vitals = NewShip();
            vitals.ApplyDamage(20f);

            vitals.Tick(2.9f);
            Assert.AreEqual(5f, vitals.Shield, 1e-4);

            vitals.Tick(0.1f);  // delay reached; regen starts on this tick
            vitals.Tick(1f);
            Assert.That(vitals.Shield, Is.GreaterThan(5f));

            vitals.Tick(10f);
            Assert.AreEqual(25f, vitals.Shield);
        }

        [Test]
        public void DamageDuringDelay_RestartsTheDelay()
        {
            var vitals = NewShip();
            vitals.ApplyDamage(10f);
            vitals.Tick(2.5f);
            vitals.ApplyDamage(5f);
            vitals.Tick(2.5f);

            Assert.AreEqual(10f, vitals.Shield, 1e-4);
        }

        [Test]
        public void Hull_DoesNotRegenerate()
        {
            var vitals = new Vitals(100f, 0f);
            vitals.ApplyDamage(40f);

            vitals.Tick(60f);

            Assert.AreEqual(60f, vitals.Hull);
        }

        [Test]
        public void LethalDamage_KillsAndClampsAtZero()
        {
            var vitals = NewShip();

            var result = vitals.ApplyDamage(500f);

            Assert.IsTrue(result.Killed);
            Assert.IsTrue(vitals.IsDead);
            Assert.AreEqual(0f, vitals.Hull);
            Assert.AreEqual(100f, result.HullDamage);
        }

        [Test]
        public void DeadShip_IgnoresDamageAndHealing()
        {
            var vitals = new Vitals(10f);
            vitals.ApplyDamage(10f);

            Assert.AreEqual(0f, vitals.ApplyDamage(5f).TotalDamage);
            Assert.AreEqual(0f, vitals.Heal(5f));
        }

        [Test]
        public void DamageTakenMultiplier_ScalesIncomingDamage()
        {
            var vitals = new Vitals(100f) { DamageTakenMultiplier = 1.25f };

            vitals.ApplyDamage(20f);

            Assert.AreEqual(75f, vitals.Hull);
        }

        [Test]
        public void Heal_IsCappedAtMaxHull()
        {
            var vitals = new Vitals(100f);
            vitals.ApplyDamage(10f);

            Assert.AreEqual(10f, vitals.Heal(50f));
            Assert.AreEqual(100f, vitals.Hull);
        }
    }
}
