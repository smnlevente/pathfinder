using NUnit.Framework;
using Pathfinder.Core.Meta;

namespace Pathfinder.Tests.Meta
{
    public class MetaStateTests
    {
        static MetaState WithCurrency(int darkMatter, int cores)
        {
            var state = new MetaState();
            state.BankRun(darkMatter, cores, scrapsCollected: 0, won: false, galaxyReached: 1);
            return state;
        }

        [Test]
        public void BankRun_KeepsCurrencies_AndConvertsTenPercentOfScraps()
        {
            var state = new MetaState();

            var rewards = state.BankRun(darkMatter: 40, singularityCores: 1, scrapsCollected: 255, won: false, galaxyReached: 2);

            Assert.AreEqual(25, rewards.ConvertedScraps);
            Assert.AreEqual(65, rewards.TotalDarkMatter);
            Assert.AreEqual(65, state.DarkMatter);
            Assert.AreEqual(1, state.SingularityCores);
            Assert.AreEqual(1, state.RunsPlayed);
            Assert.AreEqual(0, state.RunsWon);
            Assert.AreEqual(2, state.BestGalaxy);
        }

        [Test]
        public void PurchaseTier_SpendsAndStopsAtMax()
        {
            var state = WithCurrency(100, 0);

            Assert.IsTrue(state.TryPurchaseTier("lab.hull", 2, MetaCurrency.DarkMatter, 30));
            Assert.IsTrue(state.TryPurchaseTier("lab.hull", 2, MetaCurrency.DarkMatter, 60));
            Assert.IsFalse(state.TryPurchaseTier("lab.hull", 2, MetaCurrency.DarkMatter, 1), "Maxed.");
            Assert.AreEqual(2, state.Tier("lab.hull"));
            Assert.AreEqual(10, state.DarkMatter);
        }

        [Test]
        public void Purchase_FailsWithoutEnoughCurrency()
        {
            var state = WithCurrency(20, 1);

            Assert.IsFalse(state.TryPurchaseTier("lab.hull", 5, MetaCurrency.DarkMatter, 30));
            Assert.IsFalse(state.TryPurchaseTier("core.dash", 1, MetaCurrency.SingularityCores, 2));
            Assert.AreEqual(20, state.DarkMatter);
            Assert.AreEqual(1, state.SingularityCores);
        }

        [Test]
        public void Unlock_IsPermanentAndOneTime()
        {
            var state = WithCurrency(300, 0);

            Assert.IsTrue(state.TryUnlock("primary.quantum-charger", MetaCurrency.DarkMatter, 150));
            Assert.IsTrue(state.IsUnlocked("primary.quantum-charger"));
            Assert.IsFalse(state.TryUnlock("primary.quantum-charger", MetaCurrency.DarkMatter, 150));
            Assert.AreEqual(150, state.DarkMatter);
        }

        [Test]
        public void SaveRoundTrip_PreservesEverything()
        {
            var state = WithCurrency(500, 3);
            state.TryPurchaseTier("lab.hull", 5, MetaCurrency.DarkMatter, 30);
            state.TryPurchaseTier("core.dash", 1, MetaCurrency.SingularityCores, 2);
            state.TryUnlock("primary.quantum-charger", MetaCurrency.DarkMatter, 150);
            state.BankRun(0, 0, 0, won: true, galaxyReached: 3);

            var copy = MetaState.FromSave(state.ToSave());

            Assert.AreEqual(state.DarkMatter, copy.DarkMatter);
            Assert.AreEqual(state.SingularityCores, copy.SingularityCores);
            Assert.AreEqual(1, copy.Tier("lab.hull"));
            Assert.AreEqual(1, copy.Tier("core.dash"));
            Assert.IsTrue(copy.IsUnlocked("primary.quantum-charger"));
            Assert.AreEqual(2, copy.RunsPlayed);
            Assert.AreEqual(1, copy.RunsWon);
            Assert.AreEqual(3, copy.BestGalaxy);
        }

        [Test]
        public void FromSave_HandlesMissingOrCorruptData()
        {
            Assert.AreEqual(0, MetaState.FromSave(null).DarkMatter);

            var save = new MetaSave { DarkMatter = -50 };
            save.UpgradeIds.Add("orphan");
            Assert.AreEqual(0, MetaState.FromSave(save).DarkMatter, "Negative balances are clamped.");
            Assert.AreEqual(0, MetaState.FromSave(save).Tier("orphan"), "Mismatched lists are ignored safely.");
        }
    }
}
