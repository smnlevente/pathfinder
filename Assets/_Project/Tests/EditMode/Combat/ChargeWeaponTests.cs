using NUnit.Framework;
using Pathfinder.Core.Combat;

namespace Pathfinder.Tests.Combat
{
    public class ChargeWeaponTests
    {
        const float Tick = 1f / 60f;

        // Quantum Charger values from GDD §6.1.
        static ChargeWeapon NewCharger() => new ChargeWeapon(minChargeTime: 0.3f, fullChargeTime: 1.5f, overheatTime: 2f, lockoutTime: 1f);

        static bool Hold(ChargeWeapon weapon, float seconds)
        {
            bool fired = false;
            for (int i = 0, ticks = (int)System.Math.Round(seconds / Tick); i < ticks; i++)
                fired |= weapon.Tick(Tick, triggerHeld: true, out _);
            return fired;
        }

        static bool Release(ChargeWeapon weapon, out float power) => weapon.Tick(Tick, triggerHeld: false, out power);

        [Test]
        public void ReleaseBeforeMinimumCharge_DoesNotFire()
        {
            var weapon = NewCharger();
            Hold(weapon, 0.2f);

            Assert.IsFalse(Release(weapon, out _));
            Assert.IsFalse(weapon.IsCharging);
        }

        [Test]
        public void ReleaseAtMinimumCharge_FiresAtZeroPower()
        {
            var weapon = NewCharger();
            Hold(weapon, 0.3f);

            Assert.IsTrue(Release(weapon, out float power));
            Assert.AreEqual(0f, power, 0.02f);
        }

        [Test]
        public void ReleaseAtFullCharge_FiresAtFullPower()
        {
            var weapon = NewCharger();
            Hold(weapon, 1.5f);

            Assert.IsTrue(Release(weapon, out float power));
            Assert.AreEqual(1f, power, 1e-4f);
        }

        [Test]
        public void PowerIsLinearBetweenMinimumAndFull()
        {
            var weapon = NewCharger();
            Hold(weapon, 0.9f);

            Release(weapon, out float power);
            Assert.AreEqual(0.5f, power, 0.02f);
        }

        [Test]
        public void HoldingTooLong_OverheatsWithoutFiring()
        {
            var weapon = NewCharger();

            Assert.IsFalse(Hold(weapon, 3.6f));
            Assert.IsTrue(weapon.IsLockedOut);
            Assert.IsFalse(Release(weapon, out _));
        }

        [Test]
        public void HeatFraction_RisesOnlyAfterFullCharge()
        {
            var weapon = NewCharger();
            Hold(weapon, 1.5f);
            Assert.AreEqual(0f, weapon.HeatFraction, 0.01f);

            Hold(weapon, 1f);
            Assert.AreEqual(0.5f, weapon.HeatFraction, 0.02f);
        }

        [Test]
        public void AfterLockout_TriggerMustBeReleasedBeforeCharging()
        {
            var weapon = NewCharger();
            Hold(weapon, 3.5f + 1.1f); // overheat, then keep holding through the whole lockout

            Assert.IsFalse(weapon.IsLockedOut);
            Assert.IsFalse(weapon.IsCharging, "Still holding from before the lockout, so no new charge.");

            Release(weapon, out _);
            Hold(weapon, 0.5f);
            Assert.IsTrue(weapon.IsCharging);
            Assert.IsTrue(Release(weapon, out _));
        }
    }
}
