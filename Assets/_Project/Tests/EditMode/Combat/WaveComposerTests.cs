using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;

namespace Pathfinder.Tests.Combat
{
    public class WaveComposerTests
    {
        // Drone, Gunner, Spinner, Lancer, Carrier costs from GDD §7.3.
        static readonly WaveEntry[] Entries =
        {
            new WaveEntry(cost: 1, weight: 5f),
            new WaveEntry(cost: 2, weight: 3f),
            new WaveEntry(cost: 3, weight: 2f, minWave: 2),
            new WaveEntry(cost: 3, weight: 2f, minWave: 2),
            new WaveEntry(cost: 6, weight: 1f, minWave: 4),
        };

        static int Spent(System.Collections.Generic.List<WaveSpawnOrder> orders, float eliteMultiplier = 2f) =>
            orders.Sum(o => o.Elite ? (int)System.Math.Ceiling(Entries[o.EntryIndex].Cost * eliteMultiplier) : Entries[o.EntryIndex].Cost);

        [Test]
        public void NeverExceedsBudget_AndLeavesLessThanCheapestEntry()
        {
            for (ulong seed = 0; seed < 50; seed++)
            {
                var orders = WaveComposer.Compose(Entries, budget: 17, wave: 5, eliteChance: 0.2f, eliteCostMultiplier: 2f, new Pcg32(seed, 1));
                int spent = Spent(orders);

                Assert.That(spent, Is.LessThanOrEqualTo(17));
                Assert.That(17 - spent, Is.LessThan(1), "Leftover budget should not afford even a Drone.");
            }
        }

        [Test]
        public void RespectsMinimumWave()
        {
            for (ulong seed = 0; seed < 50; seed++)
            {
                var orders = WaveComposer.Compose(Entries, budget: 30, wave: 1, eliteChance: 0f, eliteCostMultiplier: 2f, new Pcg32(seed, 2));
                Assert.That(orders.All(o => o.EntryIndex <= 1), "Only Drones and Gunners are unlocked in wave 1.");
            }
        }

        [Test]
        public void EliteChanceOne_MakesEveryAffordablePickElite()
        {
            var orders = WaveComposer.Compose(new[] { new WaveEntry(1, 1f) }, budget: 10, wave: 1, eliteChance: 1f, eliteCostMultiplier: 2f, new Pcg32(3, 3));

            Assert.AreEqual(5, orders.Count);
            Assert.That(orders.All(o => o.Elite));
        }

        [Test]
        public void ZeroBudget_ProducesEmptyWave()
        {
            Assert.IsEmpty(WaveComposer.Compose(Entries, budget: 0, wave: 3, eliteChance: 0.5f, eliteCostMultiplier: 2f, new Pcg32(4, 4)));
        }

        [Test]
        public void IsDeterministicForASeed()
        {
            var a = WaveComposer.Compose(Entries, 25, 6, 0.3f, 2f, new Pcg32(99, 9));
            var b = WaveComposer.Compose(Entries, 25, 6, 0.3f, 2f, new Pcg32(99, 9));

            CollectionAssert.AreEqual(a.Select(o => (o.EntryIndex, o.Elite)), b.Select(o => (o.EntryIndex, o.Elite)));
        }

        [Test]
        public void IsCappedToProtectTheArena()
        {
            var orders = WaveComposer.Compose(new[] { new WaveEntry(1, 1f) }, budget: 10_000, wave: 1, eliteChance: 0f, eliteCostMultiplier: 2f, new Pcg32(5, 5));
            Assert.AreEqual(WaveComposer.MaxSpawnsPerWave, orders.Count);
        }
    }
}
