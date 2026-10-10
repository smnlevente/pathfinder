using System;
using System.Collections.Generic;

namespace Pathfinder.Core.Run
{
    /// <summary>Level types on the galaxy map (GDD §10).</summary>
    public enum MapNodeType
    {
        Scraps,
        Equipment,
        DarkMatter,
        Repair,
        Mystery,
        Shop,
        Boss,
    }

    /// <summary>One level on a galaxy map. Rows are 0-based here; the GDD numbers them from 1.</summary>
    public sealed class MapNode
    {
        internal readonly List<MapNode> NextNodes = new List<MapNode>();
        internal readonly List<MapNode> PreviousNodes = new List<MapNode>();

        public int Id { get; }
        public int Row { get; }
        public int Lane { get; }
        public int LaneCount { get; }
        public MapNodeType Type { get; internal set; }

        /// <summary>Horizontal layout position from 0 to 1, with lanes spread evenly across the row.</summary>
        public float X => (Lane + 0.5f) / LaneCount;

        public IReadOnlyList<MapNode> Next => NextNodes;
        public IReadOnlyList<MapNode> Previous => PreviousNodes;

        internal MapNode(int id, int row, int lane, int laneCount, MapNodeType type)
        {
            Id = id;
            Row = row;
            Lane = lane;
            LaneCount = laneCount;
            Type = type;
        }

        public override string ToString() => $"#{Id} {Type} (row {Row + 1}, lane {Lane + 1}/{LaneCount})";
    }

    /// <summary>
    /// A galaxy's "reversed tree" (GDD §9): several entry nodes at the bottom converge through the playable rows into
    /// a single Shop and then the Boss.
    /// </summary>
    public sealed class GalaxyMap
    {
        readonly List<MapNode> _nodes = new List<MapNode>();
        readonly List<List<MapNode>> _rows = new List<List<MapNode>>();

        public IReadOnlyList<MapNode> Nodes => _nodes;
        public int RowCount => _rows.Count;
        public IReadOnlyList<MapNode> EntryNodes => _rows[0];
        public MapNode Shop => _rows[_rows.Count - 2][0];
        public MapNode Boss => _rows[_rows.Count - 1][0];

        public MapNode this[int id] => _nodes[id];

        public IReadOnlyList<MapNode> Row(int row) => _rows[row];

        public bool HasEdge(MapNode from, MapNode to) => from.NextNodes.Contains(to);

        internal List<MapNode> AddRow(int width, MapNodeType type)
        {
            var row = new List<MapNode>(width);
            int rowIndex = _rows.Count;
            for (int lane = 0; lane < width; lane++)
            {
                var node = new MapNode(_nodes.Count, rowIndex, lane, width, type);
                _nodes.Add(node);
                row.Add(node);
            }
            _rows.Add(row);
            return row;
        }

        internal static void Connect(MapNode from, MapNode to)
        {
            if (to.Row != from.Row + 1)
                throw new ArgumentException("Edges only go to the next row.");
            if (from.NextNodes.Contains(to))
                return;

            from.NextNodes.Add(to);
            to.PreviousNodes.Add(from);
        }
    }
}
