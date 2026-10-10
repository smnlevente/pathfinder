using System;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;

namespace Pathfinder.Tests.Run
{
    public class RunStateTests
    {
        static RunState NewRun(ulong seed = 7) => new RunState(new Seed(seed));

        /// <summary>Walks a path to the boss, always taking the first available node.</summary>
        static void WalkToBoss(RunState run)
        {
            while (run.CurrentNode == null || run.CurrentNode.Type != MapNodeType.Boss)
                run.Enter(run.AvailableNodes[0]);
        }

        [Test]
        public void StartsAtTheGalaxyBase()
        {
            var run = NewRun();

            Assert.IsNull(run.CurrentNode);
            Assert.AreEqual(0, run.GalaxyIndex);
            CollectionAssert.AreEqual(run.Map.EntryNodes, run.AvailableNodes);
        }

        [Test]
        public void Enter_OnlyAllowsConnectedNodes()
        {
            var run = NewRun();
            var entry = run.Map.EntryNodes[0];
            run.Enter(entry);

            CollectionAssert.AreEqual(entry.Next, run.AvailableNodes);
            Assert.Throws<InvalidOperationException>(() => run.Enter(run.Map.Boss));
            Assert.Throws<InvalidOperationException>(() => run.Enter(entry));
        }

        [Test]
        public void CompletingAGalaxy_RequiresTheBoss_AndBuildsANewMap()
        {
            var run = NewRun();
            run.Enter(run.AvailableNodes[0]);
            Assert.Throws<InvalidOperationException>(() => run.CompleteGalaxy());

            var firstMap = run.Map;
            WalkToBoss(run);
            run.CompleteGalaxy();

            Assert.AreEqual(1, run.GalaxyIndex);
            Assert.AreNotSame(firstMap, run.Map);
            Assert.IsNull(run.CurrentNode);
            Assert.IsEmpty(run.VisitedNodes);
        }

        [Test]
        public void ThirdBoss_WinsTheRun()
        {
            var run = NewRun();
            for (int galaxy = 0; galaxy < RunState.GalaxyCount; galaxy++)
            {
                WalkToBoss(run);
                run.CompleteGalaxy();
            }

            Assert.IsTrue(run.IsWon);
            Assert.IsTrue(run.IsOver);
            Assert.AreEqual(RunState.GalaxyCount - 1, run.GalaxyIndex);
            Assert.IsEmpty(run.AvailableNodes.Where(run.CanEnter));
        }

        [Test]
        public void SameSeed_GivesSameMaps_DifferentGalaxiesDiffer()
        {
            var a = NewRun(99);
            var b = NewRun(99);
            CollectionAssert.AreEqual(a.Map.Nodes.Select(n => n.Type), b.Map.Nodes.Select(n => n.Type));

            var firstTypes = a.Map.Nodes.Select(n => (n.Row, n.Lane, n.Type)).ToList();
            WalkToBoss(a);
            a.CompleteGalaxy();
            CollectionAssert.AreNotEqual(firstTypes, a.Map.Nodes.Select(n => (n.Row, n.Lane, n.Type)).ToList());
        }

        [Test]
        public void LevelSeed_DependsOnlyOnGalaxyAndNode()
        {
            var a = NewRun(5);
            var b = NewRun(5);
            var node = a.Map.Row(2)[0];

            // Play different routes first, then compare the seed of the same node.
            a.Enter(a.Map.EntryNodes[0]);
            b.Enter(b.Map.EntryNodes[b.Map.EntryNodes.Count - 1]);

            Assert.AreEqual(a.LevelSeed(node), b.LevelSeed(b.Map[node.Id]));
        }

        [Test]
        public void Mystery_NeverResolvesToShopOrBoss_AndRespectsRepairRules()
        {
            for (ulong seed = 0; seed < 300; seed++)
            {
                var random = new Pcg32(seed, 3);
                var outcome = MysteryRoll.Resolve(random, repairAllowed: false);
                CollectionAssert.Contains(new[] { MapNodeType.Scraps, MapNodeType.Equipment, MapNodeType.DarkMatter }, outcome.Type);
                if (outcome.Ambush)
                    CollectionAssert.Contains(new[] { MapNodeType.Scraps, MapNodeType.Equipment }, outcome.Type, "Ambushes are combat levels.");
            }

            bool sawRepair = Enumerable.Range(0, 300).Any(i => MysteryRoll.Resolve(new Pcg32((ulong)i, 4), repairAllowed: true).Type == MapNodeType.Repair);
            Assert.IsTrue(sawRepair);
        }

        [Test]
        public void Currencies_Accumulate()
        {
            var run = NewRun();
            run.AddDarkMatter(12);
            run.AddDarkMatter(-5);
            run.AddSingularityCores(1);

            Assert.AreEqual(12, run.DarkMatter);
            Assert.AreEqual(1, run.SingularityCores);
        }
    }
}
