using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Random;

namespace Pathfinder.Tests.Random
{
    public class RandomExtensionsTests
    {
        static IRandom NewRandom() => new Pcg32(2026UL, 10UL);

        [Test]
        public void RangeInt_StaysInBoundsAndCoversAllValues()
        {
            var rng = NewRandom();
            var seen = new HashSet<int>();

            for (int i = 0; i < 2000; i++)
            {
                int value = rng.Range(-3, 4);
                Assert.That(value, Is.InRange(-3, 3));
                seen.Add(value);
            }
            Assert.AreEqual(7, seen.Count);
        }

        [Test]
        public void RangeInt_HandlesFullIntSpan()
        {
            var rng = NewRandom();
            for (int i = 0; i < 1000; i++)
                Assert.Less(rng.Range(int.MinValue, int.MaxValue), int.MaxValue);
        }

        [Test]
        public void RangeInt_SingleValueRange_ReturnsMin()
        {
            Assert.AreEqual(5, NewRandom().Range(5, 6));
        }

        [Test]
        public void RangeInt_EmptyRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NewRandom().Range(3, 3));
        }

        [Test]
        public void NextFloat_IsInUnitInterval()
        {
            var rng = NewRandom();
            for (int i = 0; i < 10000; i++)
                Assert.That(rng.NextFloat(), Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
        }

        [Test]
        public void NextDouble_IsInUnitInterval()
        {
            var rng = NewRandom();
            for (int i = 0; i < 10000; i++)
                Assert.That(rng.NextDouble(), Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
        }

        [Test]
        public void Chance_RespectsCertainOutcomes()
        {
            var rng = NewRandom();
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(rng.Chance(0f));
                Assert.IsTrue(rng.Chance(1f));
            }
        }

        [Test]
        public void WeightedIndex_NeverPicksNonPositiveWeights()
        {
            var rng = NewRandom();
            float[] weights = { 0f, 3f, -1f, 1f, 0f };
            var counts = new int[weights.Length];

            for (int i = 0; i < 4000; i++)
                counts[rng.WeightedIndex(weights)]++;

            Assert.AreEqual(0, counts[0]);
            Assert.AreEqual(0, counts[2]);
            Assert.AreEqual(0, counts[4]);
            // Expected ratio 3:1. A wide tolerance keeps the test robust to the seed.
            Assert.That(counts[1] / (double)counts[3], Is.InRange(2.4, 3.7));
        }

        [Test]
        public void WeightedIndex_AllZero_Throws()
        {
            Assert.Throws<ArgumentException>(() => NewRandom().WeightedIndex(new[] { 0f, 0f }));
        }

        [Test]
        public void Shuffle_IsDeterministicPermutation()
        {
            var a = Enumerable.Range(0, 50).ToList();
            var b = Enumerable.Range(0, 50).ToList();

            new Pcg32(5UL, 5UL).Shuffle(a);
            new Pcg32(5UL, 5UL).Shuffle(b);

            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 50), a);
            CollectionAssert.AreNotEqual(Enumerable.Range(0, 50), a);
        }
    }
}
