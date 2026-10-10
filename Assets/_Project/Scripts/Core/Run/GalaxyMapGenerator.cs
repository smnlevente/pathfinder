using System;
using System.Collections.Generic;
using Pathfinder.Core.Random;

namespace Pathfinder.Core.Run
{
    /// <summary>Tunable generation rules (GDD §9.2). Row indices are 0-based.</summary>
    public sealed class MapRules
    {
        public int PlayableRows = 4;
        public int MinWidth = 2;
        public int MaxWidth = 4;

        /// <summary>Chance per node of an extra diagonal edge to a neighbouring lane, which lets the player switch paths.</summary>
        public float BridgeChance = 0.15f;

        public float EquipmentWeight = 35f;
        public float ScrapsWeight = 30f;
        public float DarkMatterWeight = 20f;
        public float MysteryWeight = 15f;
        public int MaxMystery = 2;

        public float RepairChance = 0.6f;

        /// <summary>Repair may appear only from this row on (GDD: never in the first two rows).</summary>
        public int RepairMinRow = 2;

        public static MapRules Default => new MapRules();
    }

    /// <summary>
    /// Builds a galaxy map that satisfies the GDD §9.2 rules:
    /// <list type="bullet">
    /// <item>2–4 nodes per playable row, then a single Shop and a single Boss.</item>
    /// <item>Edges link lane neighbours and never cross. Occasional bridges let the player switch paths.</item>
    /// <item>Every node is reachable from an entry node, and every node leads to the Shop.</item>
    /// <item>Row 1 has at least one Equipment node and never Dark Matter or Mystery.</item>
    /// <item>At most 2 Mystery nodes and at most 1 Repair node (60% chance), with Repair only in rows 3–4.</item>
    /// <item>No path contains three consecutive nodes of the same type.</item>
    /// </list>
    /// </summary>
    public static class GalaxyMapGenerator
    {
        static readonly MapNodeType[] WeightedTypes = { MapNodeType.Equipment, MapNodeType.Scraps, MapNodeType.DarkMatter, MapNodeType.Mystery };

        public static GalaxyMap Generate(IRandom random, MapRules rules = null)
        {
            rules ??= MapRules.Default;
            if (rules.PlayableRows < 1 || rules.MinWidth < 1 || rules.MaxWidth < rules.MinWidth)
                throw new ArgumentException("Invalid map rules.", nameof(rules));

            var map = new GalaxyMap();
            var rows = new List<List<MapNode>>();
            for (int r = 0; r < rules.PlayableRows; r++)
                rows.Add(map.AddRow(random.Range(rules.MinWidth, rules.MaxWidth + 1), MapNodeType.Scraps));
            rows.Add(map.AddRow(1, MapNodeType.Shop));
            rows.Add(map.AddRow(1, MapNodeType.Boss));

            for (int r = 0; r < rows.Count - 1; r++)
            {
                ConnectStaircase(rows[r], rows[r + 1]);
                if (r < rules.PlayableRows - 1)
                    AddBridges(rows[r], rows[r + 1], random, rules.BridgeChance);
            }

            AssignTypes(rows, random, rules);
            return map;
        }

        /// <summary>
        /// Links two rows with a monotone "staircase" sweep from left to right. Every node gets at least one edge in each
        /// direction, and because both lane indices only ever increase, no two edges cross.
        /// </summary>
        static void ConnectStaircase(List<MapNode> from, List<MapNode> to)
        {
            int i = 0, k = 0;
            GalaxyMap.Connect(from[0], to[0]);
            while (i < from.Count - 1 || k < to.Count - 1)
            {
                if (i == from.Count - 1)
                {
                    k++;
                }
                else if (k == to.Count - 1)
                {
                    i++;
                }
                else
                {
                    float nextFrom = from[i + 1].X, nextTo = to[k + 1].X;
                    if (Math.Abs(nextFrom - nextTo) < 0.05f)
                    {
                        i++;
                        k++;
                    }
                    else if (nextFrom < nextTo)
                    {
                        i++;
                    }
                    else
                    {
                        k++;
                    }
                }
                GalaxyMap.Connect(from[i], to[k]);
            }
        }

        static void AddBridges(List<MapNode> from, List<MapNode> to, IRandom random, float chance)
        {
            foreach (var node in from)
            {
                if (!random.Chance(chance))
                    continue;

                // Extend this node's reach by one lane to the left or right, if that doesn't cross an existing edge.
                int lowest = int.MaxValue, highest = int.MinValue;
                foreach (var next in node.Next)
                {
                    lowest = Math.Min(lowest, next.Lane);
                    highest = Math.Max(highest, next.Lane);
                }

                bool preferLeft = random.Chance(0.5f);
                foreach (int lane in preferLeft ? new[] { lowest - 1, highest + 1 } : new[] { highest + 1, lowest - 1 })
                {
                    if (lane < 0 || lane >= to.Count || Crosses(from, node.Lane, lane))
                        continue;
                    GalaxyMap.Connect(node, to[lane]);
                    break;
                }
            }
        }

        static bool Crosses(List<MapNode> from, int fromLane, int toLane)
        {
            foreach (var other in from)
            {
                foreach (var next in other.Next)
                {
                    if ((other.Lane < fromLane && next.Lane > toLane) || (other.Lane > fromLane && next.Lane < toLane))
                        return true;
                }
            }
            return false;
        }

        static void AssignTypes(List<List<MapNode>> rows, IRandom random, MapRules rules)
        {
            var weights = new float[WeightedTypes.Length];
            int mysteries = 0;

            for (int r = 0; r < rules.PlayableRows; r++)
            {
                foreach (var node in rows[r])
                {
                    for (int t = 0; t < WeightedTypes.Length; t++)
                    {
                        var type = WeightedTypes[t];
                        bool allowedInRow = r > 0 || (type != MapNodeType.DarkMatter && type != MapNodeType.Mystery);
                        bool underCap = type != MapNodeType.Mystery || mysteries < rules.MaxMystery;
                        weights[t] = allowedInRow && underCap && !WouldTriple(node, type) ? Weight(rules, type) : 0f;
                    }

                    node.Type = HasPositive(weights) ? WeightedTypes[random.WeightedIndex(weights)] : MapNodeType.Scraps;
                    if (node.Type == MapNodeType.Mystery)
                        mysteries++;
                }

                // Every run must be able to pick up equipment early.
                if (r == 0 && !rows[0].Exists(n => n.Type == MapNodeType.Equipment))
                    rows[0][random.Range(0, rows[0].Count)].Type = MapNodeType.Equipment;
            }

            // A single Repair node replaces a later node. Being unique, it can never form a triple.
            if (random.Chance(rules.RepairChance))
            {
                var candidates = new List<MapNode>();
                for (int r = Math.Min(rules.RepairMinRow, rules.PlayableRows); r < rules.PlayableRows; r++)
                    candidates.AddRange(rows[r]);
                if (candidates.Count > 0)
                    random.Pick(candidates).Type = MapNodeType.Repair;
            }
        }

        /// <summary>True if giving <paramref name="node"/> this type would complete a run of three along some path.</summary>
        static bool WouldTriple(MapNode node, MapNodeType type)
        {
            foreach (var parent in node.Previous)
            {
                if (parent.Type != type)
                    continue;
                foreach (var grandparent in parent.Previous)
                {
                    if (grandparent.Type == type)
                        return true;
                }
            }
            return false;
        }

        static float Weight(MapRules rules, MapNodeType type) => type switch
        {
            MapNodeType.Equipment => rules.EquipmentWeight,
            MapNodeType.Scraps => rules.ScrapsWeight,
            MapNodeType.DarkMatter => rules.DarkMatterWeight,
            MapNodeType.Mystery => rules.MysteryWeight,
            _ => 0f,
        };

        static bool HasPositive(float[] weights)
        {
            foreach (float weight in weights)
            {
                if (weight > 0f)
                    return true;
            }
            return false;
        }
    }
}
