using NUnit.Framework;
using Pathfinder.Core.Combat;

namespace Pathfinder.Tests.Combat
{
    public class DashStateTests
    {
        const float Tick = 1f / 60f;

        // Base dash values from GDD §5.2.
        static DashState NewDash(int charges = 1) => new DashState(charges, duration: 0.15f, rechargeTime: 1.2f);

        static void Advance(DashState dash, float seconds)
        {
            for (int i = 0, ticks = (int)System.Math.Round(seconds / Tick); i < ticks; i++)
                dash.Tick(Tick);
        }

        [Test]
        public void Dash_LastsItsDuration()
        {
            var dash = NewDash();
            Assert.IsTrue(dash.TryStart());

            int activeTicks = 0;
            while (dash.IsDashing)
            {
                activeTicks++;
                dash.Tick(Tick);
            }

            Assert.AreEqual(9, activeTicks); // 0.15 s at 60 Hz
        }

        [Test]
        public void CannotDashWithoutCharges()
        {
            var dash = NewDash();
            dash.TryStart();
            Advance(dash, 0.2f);

            Assert.IsFalse(dash.TryStart());
        }

        [Test]
        public void CannotStartWhileDashing()
        {
            var dash = NewDash(charges: 2);
            dash.TryStart();

            Assert.IsFalse(dash.TryStart());
            Assert.AreEqual(1, dash.Charges);
        }

        [Test]
        public void ChargeRechargesAfterRechargeTime()
        {
            var dash = NewDash();
            dash.TryStart();

            Advance(dash, 1.1f);
            Assert.AreEqual(0, dash.Charges);

            Advance(dash, 0.15f);
            Assert.AreEqual(1, dash.Charges);
            Assert.AreEqual(0f, dash.RechargeProgress);
        }

        [Test]
        public void MultipleCharges_RechargeOneAtATime()
        {
            var dash = NewDash(charges: 2);
            dash.TryStart();
            Advance(dash, 0.2f);
            dash.TryStart();

            Advance(dash, 1.1f);
            Assert.AreEqual(1, dash.Charges);

            Advance(dash, 1.2f);
            Assert.AreEqual(2, dash.Charges);
        }
    }
}
