using System;
using System.Collections.Generic;
using Pathfinder.Core.Random;

namespace Pathfinder.Core.Combat
{
    /// <summary>One purchasable enemy type for the wave director (GDD §7.2).</summary>
    public readonly struct WaveEntry
    {
        public int Cost { get; }
        public float Weight { get; }

        /// <summary>First wave (1-based) in which this enemy may appear.</summary>
        public int MinWave { get; }

        public WaveEntry(int cost, float weight, int minWave = 1)
        {
            if (cost <= 0)
                throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be positive.");
            Cost = cost;
            Weight = weight;
            MinWave = minWave;
        }
    }

    public readonly struct WaveSpawnOrder
    {
        public int EntryIndex { get; }
        public bool Elite { get; }

        public WaveSpawnOrder(int entryIndex, bool elite)
        {
            EntryIndex = entryIndex;
            Elite = elite;
        }
    }

    /// <summary>
    /// Spends a wave's budget on a random mix of affordable enemies (GDD §7.2). Each pick is weighted among
    /// the entries that are unlocked and still affordable. A pick may become an elite, which costs more.
    /// </summary>
    public static class WaveComposer
    {
        /// <summary>Safety cap so a huge budget can't flood the arena.</summary>
        public const int MaxSpawnsPerWave = 40;

        public static List<WaveSpawnOrder> Compose(IReadOnlyList<WaveEntry> entries, int budget, int wave, float eliteChance,
            float eliteCostMultiplier, IRandom random)
        {
            var orders = new List<WaveSpawnOrder>();
            var weights = new float[entries.Count];
            int remaining = budget;

            while (orders.Count < MaxSpawnsPerWave)
            {
                bool anyAffordable = false;
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    bool available = entry.MinWave <= wave && entry.Cost <= remaining && entry.Weight > 0f;
                    weights[i] = available ? entry.Weight : 0f;
                    anyAffordable |= available;
                }

                if (!anyAffordable)
                    break;

                int index = random.WeightedIndex(weights);
                int cost = entries[index].Cost;
                int eliteCost = (int)Math.Ceiling(cost * eliteCostMultiplier);

                bool elite = eliteChance > 0f && eliteCost <= remaining && random.Chance(eliteChance);
                remaining -= elite ? eliteCost : cost;
                orders.Add(new WaveSpawnOrder(index, elite));
            }

            return orders;
        }
    }
}
