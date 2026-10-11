using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;

namespace Pathfinder.Tests.Run
{
    /// <summary>Checks every GDD §9.2 rule across many seeds.</summary>
    public class GalaxyMapGeneratorTests
    {
        const int Seeds = 500;

        static IEnumerable<GalaxyMap> Maps()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
                yield return GalaxyMapGenerator.Generate(new Pcg32(seed, 77));
        }

        [Test]
        public void Shape_SixPlayableRowsThenShopThenBoss()
        {
            foreach (var map in Maps())
            {
                Assert.AreEqual(8, map.RowCount);
                for (int r = 0; r < 6; r++)
                    Assert.That(map.Row(r).Count, Is.InRange(3, 4));
                Assert.AreEqual(1, map.Row(6).Count);
                Assert.AreEqual(MapNodeType.Shop, map.Shop.Type);
                Assert.AreEqual(1, map.Row(7).Count);
                Assert.AreEqual(MapNodeType.Boss, map.Boss.Type);
            }
        }

        [Test]
        public void EveryNode_IsReachableFromAnEntry_AndLeadsToTheShop()
        {
            foreach (var map in Maps())
            {
                foreach (var node in map.Nodes)
                {
                    if (node.Row > 0)
                        Assert.IsNotEmpty(node.Previous, $"{node} cannot be reached.");
                    if (node.Type != MapNodeType.Boss)
                        Assert.IsNotEmpty(node.Next, $"{node} is a dead end.");
                }
                Assert.AreEqual(map.Row(5).Count, map.Shop.Previous.Count, "Every last-row node leads to the Shop.");
            }
        }

        [Test]
        public void Edges_OnlyGoToTheNextRow_AndNeverCross()
        {
            foreach (var map in Maps())
            {
                for (int r = 0; r < map.RowCount - 1; r++)
                {
                    var edges = map.Row(r).SelectMany(n => n.Next.Select(next => (from: n, to: next))).ToList();
                    foreach (var (from, to) in edges)
                        Assert.AreEqual(from.Row + 1, to.Row);

                    foreach (var a in edges)
                    {
                        foreach (var b in edges)
                        {
                            bool crosses = (a.from.Lane < b.from.Lane && a.to.Lane > b.to.Lane) || (a.from.Lane > b.from.Lane && a.to.Lane < b.to.Lane);
                            Assert.IsFalse(crosses, $"{a.from}->{a.to} crosses {b.from}->{b.to}");
                        }
                    }
                }
            }
        }

        [Test]
        public void FirstRow_HasEquipment_AndNoDarkMatterOrMystery()
        {
            foreach (var map in Maps())
            {
                var entries = map.EntryNodes;
                Assert.That(entries.Any(n => n.Type == MapNodeType.Equipment));
                Assert.That(entries.All(n => n.Type != MapNodeType.DarkMatter && n.Type != MapNodeType.Mystery));
            }
        }

        [Test]
        public void Mystery_AtMostThree_Repair_AtMostOneAndOnlyInLaterRows()
        {
            int mapsWithRepair = 0;
            foreach (var map in Maps())
            {
                Assert.That(map.Nodes.Count(n => n.Type == MapNodeType.Mystery), Is.LessThanOrEqualTo(3));

                var repairs = map.Nodes.Where(n => n.Type == MapNodeType.Repair).ToList();
                Assert.That(repairs.Count, Is.LessThanOrEqualTo(1));
                Assert.That(repairs.All(n => n.Row >= 2 && n.Row <= 5), "Repair is never in the first two rows.");
                if (repairs.Count > 0)
                    mapsWithRepair++;
            }

            // A 60% chance across 500 maps: allow a generous margin.
            Assert.That(mapsWithRepair / (float)Seeds, Is.InRange(0.5f, 0.7f));
        }

        [Test]
        public void NoPath_HasThreeConsecutiveNodesOfTheSameType()
        {
            foreach (var map in Maps())
            {
                foreach (var node in map.Nodes)
                {
                    foreach (var parent in node.Previous)
                    {
                        foreach (var grandparent in parent.Previous)
                        {
                            bool triple = node.Type == parent.Type && parent.Type == grandparent.Type;
                            Assert.IsFalse(triple, $"{grandparent} -> {parent} -> {node}");
                        }
                    }
                }
            }
        }

        [Test]
        public void Bridges_AppearSometimes()
        {
            int bridged = 0, nodes = 0;
            foreach (var map in Maps())
            {
                for (int r = 0; r < 3; r++)
                {
                    foreach (var node in map.Row(r))
                    {
                        nodes++;
                        // The staircase alone never gives a node in an equal-width row more than one edge.
                        if (map.Row(r).Count == map.Row(r + 1).Count && node.Next.Count > 1)
                            bridged++;
                    }
                }
            }
            Assert.Greater(bridged, 0, "Side branches should let players switch paths sometimes.");
        }

        [Test]
        public void IsDeterministicForASeed()
        {
            var a = GalaxyMapGenerator.Generate(new Pcg32(42, 1));
            var b = GalaxyMapGenerator.Generate(new Pcg32(42, 1));

            CollectionAssert.AreEqual(a.Nodes.Select(n => (n.Row, n.Lane, n.Type)), b.Nodes.Select(n => (n.Row, n.Lane, n.Type)));
            CollectionAssert.AreEqual(a.Nodes.SelectMany(n => n.Next.Select(x => (n.Id, x.Id))), b.Nodes.SelectMany(n => n.Next.Select(x => (n.Id, x.Id))));
        }
    }
}
