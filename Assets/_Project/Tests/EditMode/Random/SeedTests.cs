using NUnit.Framework;
using Pathfinder.Core.Random;

namespace Pathfinder.Tests.Random
{
    /// <summary>
    /// Determinism guarantees from GDD §14.3. The golden values come from an independent
    /// reference implementation. If one fails, a change has altered every seeded run and
    /// broken old seeds and saves. Update a golden value only if that is intended.
    /// </summary>
    public class SeedTests
    {
        [Test]
        public void Hash_MatchesGoldenValues()
        {
            Assert.AreEqual(0xcbf29ce484222325UL, SeedHash.Hash(""));
            Assert.AreEqual(0x9dccbf195b7d6649UL, SeedHash.Hash("map"));
        }

        [Test]
        public void DerivedStream_MatchesGoldenValues()
        {
            var levelSeed = new Seed(12345UL).Derive(SeedLabels.Level).Derive(1).Derive(3);
            Assert.AreEqual(0x5ee951f29bdd976bUL, levelSeed.Value);

            var waves = levelSeed.CreateRandom(SeedLabels.Waves);
            Assert.AreEqual(0x8110e1d0u, waves.NextUInt());
            Assert.AreEqual(0x7654efddu, waves.NextUInt());
            Assert.AreEqual(0x3f63fc5cu, waves.NextUInt());
        }

        [Test]
        public void Derive_IsOrderSensitive()
        {
            var root = new Seed(1UL);
            Assert.AreNotEqual(root.Derive(1).Derive(2), root.Derive(2).Derive(1));
            Assert.AreNotEqual(root.Derive("a").Derive("b"), root.Derive("b").Derive("a"));
        }

        [Test]
        public void Derive_DistinguishesLabelsAndIndices()
        {
            var root = new Seed(1UL);
            Assert.AreNotEqual(root.Derive(SeedLabels.Map), root.Derive(SeedLabels.Level));
            Assert.AreNotEqual(root.Derive(0), root.Derive(1));
            Assert.AreNotEqual(root.Derive(-1), root.Derive(1));
        }

        [Test]
        public void LevelContent_DoesNotDependOnGenerationOrder()
        {
            var run = new Seed(777UL);

            // Generate level (0, 2) first...
            uint first = LevelRoll(run, 0, 2);

            // ...then generate and consume other levels before generating it again.
            for (int node = 0; node < 5; node++)
            {
                var other = run.Derive(SeedLabels.Level).Derive(0).Derive(node).CreateRandom(SeedLabels.Loot);
                for (int i = 0; i < 100; i++)
                    other.NextUInt();
            }

            Assert.AreEqual(first, LevelRoll(run, 0, 2));
        }

        [Test]
        public void Streams_AreIndependent()
        {
            var level = new Seed(42UL).Derive(SeedLabels.Level).Derive(0).Derive(0);
            var loot = level.CreateRandom(SeedLabels.Loot);
            var cosmetic = level.CreateRandom(SeedLabels.Cosmetic);

            // Drawing cosmetic values must not change the loot sequence.
            uint expectedLoot = level.CreateRandom(SeedLabels.Loot).NextUInt();
            for (int i = 0; i < 50; i++)
                cosmetic.NextUInt();

            Assert.AreEqual(expectedLoot, loot.NextUInt());
        }

        [Test]
        public void StreamSeed_DiffersFromChildSeedWithSameLabel()
        {
            var level = new Seed(42UL);
            var viaStream = level.CreateRandom(SeedLabels.Waves);
            var viaChild = new Pcg32(level.Derive(SeedLabels.Waves).Value, SeedHash.Hash(SeedLabels.Waves));

            Assert.AreNotEqual(viaChild.NextUInt(), viaStream.NextUInt());
        }

        static uint LevelRoll(Seed run, int galaxy, int node) =>
            run.Derive(SeedLabels.Level).Derive(galaxy).Derive(node).CreateRandom(SeedLabels.Waves).NextUInt();
    }
}
