using System;
using System.Collections.Generic;
using Pathfinder.Core.Random;

namespace Pathfinder.Core.Equipment
{
    /// <summary>An item that can be offered, described by its rarity and relative weight.</summary>
    public readonly struct LootCandidate
    {
        public Rarity Rarity { get; }
        public float Weight { get; }

        /// <summary>Excluded candidates are never offered, e.g. items already at their maximum level.</summary>
        public bool Excluded { get; }

        public LootCandidate(Rarity rarity, float weight = 1f, bool excluded = false)
        {
            Rarity = rarity;
            Weight = weight;
            Excluded = excluded;
        }
    }

    /// <summary>
    /// Rolls equipment offers for caches, boss rewards and the Shop (GDD §10.2, §10.6). Each slot first rolls a
    /// rarity from the galaxy's odds (70/25/5 in galaxy 1, improving later), then picks an item of that rarity. No
    /// item is offered twice in the same roll.
    /// </summary>
    public static class EquipmentRoller
    {
        static readonly float[][] OddsByGalaxy =
        {
            new[] { 0.70f, 0.25f, 0.05f },
            new[] { 0.55f, 0.33f, 0.12f },
            new[] { 0.40f, 0.40f, 0.20f },
        };

        public static IReadOnlyList<float> RarityOdds(int galaxyIndex) => OddsByGalaxy[Math.Max(0, Math.Min(galaxyIndex, OddsByGalaxy.Length - 1))];

        /// <summary>Returns indices into <paramref name="candidates"/>. There may be fewer than requested if the pool runs dry.</summary>
        public static List<int> Roll(IReadOnlyList<LootCandidate> candidates, int count, int galaxyIndex, Rarity minimumRarity, IRandom random)
        {
            var picked = new List<int>(count);
            var odds = RarityOdds(galaxyIndex);
            var rarityWeights = new float[3];
            var itemWeights = new float[candidates.Count];

            for (int slot = 0; slot < count; slot++)
            {
                for (int r = 0; r < 3; r++)
                    rarityWeights[r] = r >= (int)minimumRarity ? odds[r] : 0f;

                var rarity = (Rarity)random.WeightedIndex(rarityWeights);
                int index = PickOfRarity(candidates, picked, rarity, minimumRarity, itemWeights, random);
                if (index < 0)
                    break;
                picked.Add(index);
            }
            return picked;
        }

        /// <summary>Picks among the rolled rarity, falling back to the nearest rarity that still has items.</summary>
        static int PickOfRarity(IReadOnlyList<LootCandidate> candidates, List<int> picked, Rarity rarity, Rarity minimumRarity,
            float[] weights, IRandom random)
        {
            // Try the rolled rarity, then lower ones (down to the minimum), then higher ones.
            var order = new List<Rarity> { rarity };
            for (int r = (int)rarity - 1; r >= (int)minimumRarity; r--)
                order.Add((Rarity)r);
            for (int r = (int)rarity + 1; r <= (int)Rarity.Epic; r++)
                order.Add((Rarity)r);

            foreach (var tier in order)
            {
                bool any = false;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    bool available = candidate.Rarity == tier && !candidate.Excluded && candidate.Weight > 0f && !picked.Contains(i);
                    weights[i] = available ? candidate.Weight : 0f;
                    any |= available;
                }
                if (any)
                    return random.WeightedIndex(weights);
            }
            return -1;
        }
    }
}
