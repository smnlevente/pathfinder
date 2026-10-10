using System;
using System.Collections.Generic;
using Pathfinder.Core.Random;

namespace Pathfinder.Core.Run
{
    /// <summary>
    /// The shared, serializable state of one run (GDD §3, §14.2): seed, galaxy, map position and run-wide currencies.
    /// Per-pilot state (hull, Scraps, loadout) lives on each pilot. Every random outcome derives from the seed
    /// and the node's position, never from the order nodes were played in (GDD §14.3).
    /// </summary>
    public sealed class RunState
    {
        public const int GalaxyCount = 3;

        readonly MapRules _rules;
        readonly HashSet<int> _visited = new HashSet<int>();
        bool _repairUsedThisGalaxy;

        public Seed Seed { get; }
        public int GalaxyIndex { get; private set; }
        public GalaxyMap Map { get; private set; }

        /// <summary>The node being played or last played. Null at the start of a galaxy.</summary>
        public MapNode CurrentNode { get; private set; }

        public IReadOnlyCollection<int> VisitedNodes => _visited;
        public bool HasVisited(MapNode node) => node != null && _visited.Contains(node.Id);
        public int LevelsCleared { get; private set; }

        /// <summary>Persistent currencies earned this run (GDD §11.1). They are banked when the run ends.</summary>
        public int DarkMatter { get; private set; }
        public int SingularityCores { get; private set; }

        public bool IsWon { get; private set; }
        public bool IsLost { get; private set; }
        public bool IsOver => IsWon || IsLost;

        public RunState(Seed seed, MapRules rules = null)
        {
            Seed = seed;
            _rules = rules ?? MapRules.Default;
            GenerateMap();
        }

        /// <summary>The nodes the player may choose next.</summary>
        public IReadOnlyList<MapNode> AvailableNodes => CurrentNode == null ? Map.EntryNodes : CurrentNode.Next;

        public bool CanEnter(MapNode node)
        {
            if (IsOver || node == null)
                return false;
            foreach (var available in AvailableNodes)
            {
                if (available == node)
                    return true;
            }
            return false;
        }

        public void Enter(MapNode node)
        {
            if (!CanEnter(node))
                throw new InvalidOperationException($"Cannot enter {node} from {(CurrentNode?.ToString() ?? "the galaxy start")}.");

            CurrentNode = node;
            _visited.Add(node.Id);
        }

        /// <summary>Seed for a level's content. It depends only on the galaxy and node.</summary>
        public Seed LevelSeed(MapNode node) => Seed.Derive(SeedLabels.Level).Derive(GalaxyIndex).Derive(node.Id);

        /// <summary>Decides what a Mystery node turns out to be. Deterministic per node.</summary>
        public MysteryOutcome ResolveMystery(MapNode node)
        {
            if (node.Type != MapNodeType.Mystery)
                throw new ArgumentException("Only Mystery nodes are resolved.", nameof(node));

            bool repairAllowed = !_repairUsedThisGalaxy && node.Row >= _rules.RepairMinRow;
            var outcome = MysteryRoll.Resolve(LevelSeed(node).CreateRandom("mystery"), repairAllowed);
            if (outcome.Type == MapNodeType.Repair)
                _repairUsedThisGalaxy = true;
            return outcome;
        }

        public void CompleteLevel() => LevelsCleared++;

        public void AddDarkMatter(int amount) => DarkMatter += Math.Max(0, amount);

        public void AddSingularityCores(int amount) => SingularityCores += Math.Max(0, amount);

        /// <summary>Called after the boss falls. Moves to the next galaxy, or wins the run after the last one.</summary>
        public void CompleteGalaxy()
        {
            if (CurrentNode == null || CurrentNode.Type != MapNodeType.Boss)
                throw new InvalidOperationException("A galaxy is completed by defeating its boss.");

            if (GalaxyIndex >= GalaxyCount - 1)
            {
                IsWon = true;
                return;
            }

            GalaxyIndex++;
            GenerateMap();
        }

        public void MarkLost() => IsLost = !IsWon;

        void GenerateMap()
        {
            Map = GalaxyMapGenerator.Generate(Seed.Derive(SeedLabels.Map).Derive(GalaxyIndex).CreateRandom(SeedLabels.Layout), _rules);
            CurrentNode = null;
            _visited.Clear();

            _repairUsedThisGalaxy = false;
            foreach (var node in Map.Nodes)
                _repairUsedThisGalaxy |= node.Type == MapNodeType.Repair;
        }
    }

    public readonly struct MysteryOutcome
    {
        public MapNodeType Type { get; }

        /// <summary>A harder combat variant with an elite wave and double rewards (GDD §10.5).</summary>
        public bool Ambush { get; }

        public MysteryOutcome(MapNodeType type, bool ambush)
        {
            Type = type;
            Ambush = ambush;
        }
    }

    /// <summary>Mystery resolution (GDD §10.5). Never a Shop or Boss, and Repair only where it's allowed.</summary>
    public static class MysteryRoll
    {
        public const float AmbushChance = 0.1f;

        public static MysteryOutcome Resolve(IRandom random, bool repairAllowed)
        {
            MapNodeType[] types = { MapNodeType.Scraps, MapNodeType.Equipment, MapNodeType.DarkMatter, MapNodeType.Repair };
            float[] weights = { 30f, 35f, 20f, repairAllowed ? 15f : 0f };
            var type = types[random.WeightedIndex(weights)];

            bool combat = type == MapNodeType.Scraps || type == MapNodeType.Equipment;
            return new MysteryOutcome(type, combat && random.Chance(AmbushChance));
        }
    }
}
