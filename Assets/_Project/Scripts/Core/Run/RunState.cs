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
        readonly HashSet<string> _seenEvents = new HashSet<string>();
        bool _repairUsedThisGalaxy;

        public Seed Seed { get; }

        /// <summary>The pilot's chosen ship and weapons, as content IDs (GDD §5.4). Empty means the scene defaults.</summary>
        public PilotLoadout Loadout { get; set; }

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

        /// <summary>Mystery events already met this run, by content ID. An event isn't repeated while others remain.</summary>
        public bool HasSeenEvent(string eventId) => _seenEvents.Contains(eventId);

        public void MarkEventSeen(string eventId) => _seenEvents.Add(eventId);

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

        // ---------------------------------------------------------------- saving (M7)

        /// <summary>Everything needed to rebuild the run where it stands. The map isn't stored: it regenerates from the seed.</summary>
        public RunStateSnapshot Snapshot()
        {
            var visited = new int[_visited.Count];
            _visited.CopyTo(visited);
            Array.Sort(visited);
            var seen = new string[_seenEvents.Count];
            _seenEvents.CopyTo(seen);
            Array.Sort(seen, StringComparer.Ordinal);
            return new RunStateSnapshot
            {
                Seed = Seed,
                Loadout = Loadout,
                GalaxyIndex = GalaxyIndex,
                CurrentNodeId = CurrentNode != null ? CurrentNode.Id : -1,
                Visited = visited,
                LevelsCleared = LevelsCleared,
                DarkMatter = DarkMatter,
                SingularityCores = SingularityCores,
                SeenEvents = seen,
                RepairUsed = _repairUsedThisGalaxy,
            };
        }

        /// <summary>Rebuilds a run from a snapshot. Anything that doesn't fit the regenerated map (a stale node) is dropped.</summary>
        public static RunState Restore(RunStateSnapshot snapshot, MapRules rules = null)
        {
            var run = new RunState(snapshot.Seed, rules) { Loadout = snapshot.Loadout };
            int galaxy = Math.Max(0, Math.Min(snapshot.GalaxyIndex, GalaxyCount - 1));
            if (galaxy > 0)
            {
                run.GalaxyIndex = galaxy;
                run.GenerateMap();
            }

            foreach (int id in snapshot.Visited ?? Array.Empty<int>())
            {
                if (id >= 0 && id < run.Map.Nodes.Count)
                    run._visited.Add(id);
            }
            int current = snapshot.CurrentNodeId;
            run.CurrentNode = current >= 0 && current < run.Map.Nodes.Count ? run.Map[current] : null;
            if (run.CurrentNode != null)
                run._visited.Add(current);
            run.LevelsCleared = Math.Max(0, snapshot.LevelsCleared);
            run.DarkMatter = Math.Max(0, snapshot.DarkMatter);
            run.SingularityCores = Math.Max(0, snapshot.SingularityCores);
            foreach (string id in snapshot.SeenEvents ?? Array.Empty<string>())
                run._seenEvents.Add(id);
            run._repairUsedThisGalaxy |= snapshot.RepairUsed;
            return run;
        }

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

    /// <summary>A run's state as plain values, for the save file (M7). See <see cref="RunState.Snapshot"/>.</summary>
    public sealed class RunStateSnapshot
    {
        public Seed Seed;
        public PilotLoadout Loadout;
        public int GalaxyIndex;
        public int CurrentNodeId = -1;
        public int[] Visited = Array.Empty<int>();
        public int LevelsCleared;
        public int DarkMatter;
        public int SingularityCores;
        public string[] SeenEvents = Array.Empty<string>();
        public bool RepairUsed;
    }

    /// <summary>A pilot's starting ship, primary and secondary, stored as stable content IDs for saves and network messages.</summary>
    public readonly struct PilotLoadout
    {
        public string ShipId { get; }
        public string PrimaryId { get; }
        public string SecondaryId { get; }

        public bool IsEmpty => string.IsNullOrEmpty(ShipId);

        public PilotLoadout(string shipId, string primaryId, string secondaryId)
        {
            ShipId = shipId;
            PrimaryId = primaryId;
            SecondaryId = secondaryId;
        }
    }

    public readonly struct MysteryOutcome
    {
        /// <summary>The level type it turned into. <see cref="MapNodeType.Mystery"/> for an event.</summary>
        public MapNodeType Type { get; }

        /// <summary>A harder combat variant with an elite wave and double rewards (GDD §10.5).</summary>
        public bool Ambush { get; }

        /// <summary>A narrative event: a choice with upsides and downsides instead of a level (GDD §10.5).</summary>
        public bool IsEvent => Type == MapNodeType.Mystery;

        public MysteryOutcome(MapNodeType type, bool ambush)
        {
            Type = type;
            Ambush = ambush;
        }
    }

    /// <summary>
    /// Mystery resolution (GDD §10.5). Usually a narrative event; otherwise a level, never a Shop or Boss, and Repair only
    /// where it's allowed.
    /// </summary>
    public static class MysteryRoll
    {
        public const float EventChance = 0.7f;
        public const float AmbushChance = 0.1f;

        public static MysteryOutcome Resolve(IRandom random, bool repairAllowed)
        {
            if (random.Chance(EventChance))
                return new MysteryOutcome(MapNodeType.Mystery, ambush: false);

            MapNodeType[] types = { MapNodeType.Scraps, MapNodeType.Equipment, MapNodeType.DarkMatter, MapNodeType.Repair };
            float[] weights = { 30f, 35f, 20f, repairAllowed ? 15f : 0f };
            var type = types[random.WeightedIndex(weights)];

            bool combat = type == MapNodeType.Scraps || type == MapNodeType.Equipment;
            return new MysteryOutcome(type, combat && random.Chance(AmbushChance));
        }
    }
}
