using NUnit.Framework;
using Pathfinder.Core.Combat;

namespace Pathfinder.Tests.Combat
{
    public class CooldownTests
    {
        const float Tick = 1f / 60f;

        static void Advance(Cooldown cooldown, float seconds)
        {
            for (int i = 0, ticks = (int)System.Math.Round(seconds / Tick); i < ticks; i++)
                cooldown.Tick(Tick);
        }

        [Test]
        public void StartsReady_AndSpendsCharges()
        {
            var cooldown = new Cooldown(maxCharges: 2, rechargeTime: 8f);

            Assert.IsTrue(cooldown.TryUse());
            Assert.IsTrue(cooldown.TryUse());
            Assert.IsFalse(cooldown.TryUse());
        }

        [Test]
        public void RechargesAfterRechargeTime()
        {
            var cooldown = new Cooldown(1, rechargeTime: 8f);
            cooldown.TryUse();

            Advance(cooldown, 7.9f);
            Assert.IsFalse(cooldown.IsReady);
            Assert.AreEqual(0.1f, cooldown.TimeUntilReady, 0.02f);

            Advance(cooldown, 0.1f);
            Assert.IsTrue(cooldown.IsReady);
            Assert.AreEqual(0f, cooldown.RechargeProgress);
        }

        [Test]
        public void RechargeProgress_TracksTheRechargingCharge()
        {
            var cooldown = new Cooldown(1, rechargeTime: 10f);
            cooldown.TryUse();
            Advance(cooldown, 5f);

            Assert.AreEqual(0.5f, cooldown.RechargeProgress, 0.01f);
        }
    }
}
