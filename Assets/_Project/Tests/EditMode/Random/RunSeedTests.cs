using NUnit.Framework;
using Pathfinder.Core.Random;

namespace Pathfinder.Tests.Random
{
    public class RunSeedTests
    {
        [TestCase(0UL)]
        [TestCase(1UL)]
        [TestCase(0x0123456789ABCDEFUL)]
        [TestCase(ulong.MaxValue)]
        public void Code_RoundTrips(ulong value)
        {
            var seed = new Seed(value);
            string code = RunSeed.ToCode(seed);

            Assert.IsTrue(RunSeed.TryParse(code, out var parsed), code);
            Assert.AreEqual(seed, parsed);
        }

        [Test]
        public void Code_RoundTripsGeneratedSeeds()
        {
            for (int i = 0; i < 200; i++)
            {
                var seed = RunSeed.Generate();
                Assert.IsTrue(RunSeed.TryParse(RunSeed.ToCode(seed), out var parsed));
                Assert.AreEqual(seed, parsed);
            }
        }

        [Test]
        public void Code_HasExpectedShape()
        {
            Assert.AreEqual("0-0000-0000-0000", RunSeed.ToCode(new Seed(0UL)));
            Assert.AreEqual("F-ZZZZ-ZZZZ-ZZZZ", RunSeed.ToCode(new Seed(ulong.MaxValue)));
        }

        [Test]
        public void TryParse_IsForgivingAboutFormatting()
        {
            var seed = new Seed(0x0123456789ABCDEFUL);
            string code = RunSeed.ToCode(seed);
            string sloppy = code.ToLowerInvariant().Replace("-", " ").Replace('1', 'l').Replace('0', 'o');

            Assert.IsTrue(RunSeed.TryParse(sloppy, out var parsed), sloppy);
            Assert.AreEqual(seed, parsed);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0-0000-0000-000")]      // too short
        [TestCase("0-0000-0000-00000")]    // too long
        [TestCase("G-0000-0000-0000")]     // first symbol only holds 4 bits
        [TestCase("0-0000-0000-000U")]     // U is not in the alphabet
        public void TryParse_RejectsInvalidCodes(string code)
        {
            Assert.IsFalse(RunSeed.TryParse(code, out _));
        }

        [Test]
        public void Generate_ProducesDistinctSeeds()
        {
            Assert.AreNotEqual(RunSeed.Generate(), RunSeed.Generate());
        }
    }
}
