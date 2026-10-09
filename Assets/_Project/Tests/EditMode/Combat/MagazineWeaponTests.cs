using NUnit.Framework;
using Pathfinder.Core.Combat;

namespace Pathfinder.Tests.Combat
{
    public class MagazineWeaponTests
    {
        const float Tick = 1f / 60f;

        // Photon Repeater values from GDD §6.1.
        static MagazineWeapon NewPhotonRepeater() => new MagazineWeapon(magazineSize: 30, shotsPerSecond: 10f, reloadTime: 1f);

        static int HoldTrigger(MagazineWeapon weapon, float seconds)
        {
            int shots = 0;
            for (int i = 0, ticks = (int)System.Math.Round(seconds / Tick); i < ticks; i++)
                shots += weapon.Tick(Tick, triggerHeld: true, reloadRequested: false);
            return shots;
        }

        [Test]
        public void FiresImmediatelyOnTriggerPull()
        {
            Assert.AreEqual(1, NewPhotonRepeater().Tick(Tick, triggerHeld: true, reloadRequested: false));
        }

        [Test]
        public void RespectsFireRate()
        {
            var weapon = NewPhotonRepeater();

            // 10 shots/s held for 1 s: the first shot is instant, so 10 shots fit in the window.
            Assert.AreEqual(10, HoldTrigger(weapon, 1f));
        }

        [Test]
        public void HighFireRate_FiresSeveralShotsPerTick()
        {
            var weapon = new MagazineWeapon(100, shotsPerSecond: 180f, reloadTime: 1f);

            // Ideal is 90. Shots due mid-tick fire at the next tick, so the first tick lags by up to a couple.
            Assert.That(HoldTrigger(weapon, 0.5f), Is.InRange(87, 90));
        }

        [Test]
        public void EmptyMagazine_AutoReloadsThenFiresAgain()
        {
            var weapon = NewPhotonRepeater();

            Assert.AreEqual(30, HoldTrigger(weapon, 3f));
            Assert.AreEqual(0, weapon.Ammo);
            Assert.IsTrue(weapon.IsReloading);

            // Still reloading 0.9 s later: nothing fires.
            Assert.AreEqual(0, HoldTrigger(weapon, 0.9f));

            // The reload finishes and firing resumes with a full magazine.
            Assert.That(HoldTrigger(weapon, 0.25f), Is.GreaterThan(0));
            Assert.IsFalse(weapon.IsReloading);
        }

        [Test]
        public void ManualReload_OnlyWhenNotFull()
        {
            var weapon = NewPhotonRepeater();

            weapon.Tick(Tick, triggerHeld: false, reloadRequested: true);
            Assert.IsFalse(weapon.IsReloading);

            weapon.Tick(Tick, triggerHeld: true, reloadRequested: false);
            weapon.Tick(Tick, triggerHeld: false, reloadRequested: true);
            Assert.IsTrue(weapon.IsReloading);
        }

        [Test]
        public void ReleasingTrigger_DoesNotBankShots()
        {
            var weapon = NewPhotonRepeater();
            weapon.Tick(Tick, triggerHeld: true, reloadRequested: false);

            for (int i = 0; i < 120; i++)
                weapon.Tick(Tick, triggerHeld: false, reloadRequested: false);

            Assert.AreEqual(1, weapon.Tick(Tick, triggerHeld: true, reloadRequested: false));
        }

        [Test]
        public void ReloadProgress_GoesFromZeroToOne()
        {
            var weapon = NewPhotonRepeater();
            weapon.Tick(Tick, true, false);
            weapon.Tick(Tick, false, true);

            Assert.That(weapon.ReloadProgress, Is.InRange(0f, 0.05f));
            HoldTrigger(weapon, 0.5f);
            Assert.That(weapon.ReloadProgress, Is.InRange(0.45f, 0.55f));
        }
    }
}
