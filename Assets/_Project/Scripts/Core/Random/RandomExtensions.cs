using System;
using System.Collections.Generic;

namespace Pathfinder.Core.Random
{
    /// <summary>
    /// All derived random values. Everything is built on <see cref="IRandom.NextUInt"/> with
    /// integer or exactly-representable float math, so results are identical across platforms.
    /// </summary>
    public static class RandomExtensions
    {
        const float FloatUnit = 1f / (1 << 24);              // 24 bits = float mantissa
        const double DoubleUnit = 1.0 / (1UL << 53);         // 53 bits = double mantissa

        public static ulong NextULong(this IRandom random)
        {
            ulong high = random.NextUInt();
            return (high << 32) | random.NextUInt();
        }

        /// <summary>Uniform value in [0, <paramref name="bound"/>) without modulo bias.</summary>
        public static uint NextUInt(this IRandom random, uint bound)
        {
            if (bound == 0)
                throw new ArgumentOutOfRangeException(nameof(bound), "Bound must be greater than zero.");

            // Reject the low values that would make "r % bound" favour small results
            // (same approach as the reference pcg32_boundedrand_r).
            uint threshold = unchecked(0u - bound) % bound;
            while (true)
            {
                uint r = random.NextUInt();
                if (r >= threshold)
                    return r % bound;
            }
        }

        /// <summary>Uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
        public static int Range(this IRandom random, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), $"Empty range [{minInclusive}, {maxExclusive}).");

            uint span = (uint)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)random.NextUInt(span));
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public static float NextFloat(this IRandom random) => (random.NextUInt() >> 8) * FloatUnit;

        /// <summary>Uniform double in [0, 1).</summary>
        public static double NextDouble(this IRandom random) => (random.NextULong() >> 11) * DoubleUnit;

        /// <summary>Uniform float between the bounds. Float rounding can rarely return exactly <paramref name="max"/>.</summary>
        public static float Range(this IRandom random, float min, float max) => min + (max - min) * random.NextFloat();

        /// <summary>True with the given probability. Values ≤ 0 never succeed and values ≥ 1 always succeed.</summary>
        public static bool Chance(this IRandom random, float probability) => random.NextFloat() < probability;

        public static T Pick<T>(this IRandom random, IReadOnlyList<T> items)
        {
            if (items.Count == 0)
                throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
            return items[random.Range(0, items.Count)];
        }

        /// <summary>
        /// Index chosen with probability proportional to its weight. Weights ≤ 0 are never chosen.
        /// </summary>
        public static int WeightedIndex(this IRandom random, IReadOnlyList<float> weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f)
                    total += weights[i];
            }

            if (total <= 0)
                throw new ArgumentException("At least one weight must be positive.", nameof(weights));

            double roll = random.NextDouble() * total;
            int lastPositive = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (!(weights[i] > 0f))
                    continue;

                lastPositive = i;
                roll -= weights[i];
                if (roll < 0)
                    return i;
            }

            // Only reachable through floating-point rounding on the final subtraction.
            return lastPositive;
        }

        public static T WeightedPick<T>(this IRandom random, IReadOnlyList<T> items, Func<T, float> weightOf)
        {
            var weights = new float[items.Count];
            for (int i = 0; i < items.Count; i++)
                weights[i] = weightOf(items[i]);
            return items[random.WeightedIndex(weights)];
        }

        /// <summary>In-place Fisher–Yates shuffle.</summary>
        public static void Shuffle<T>(this IRandom random, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
