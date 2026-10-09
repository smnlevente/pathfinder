using NUnit.Framework;
using Pathfinder.Core.Random;

namespace Pathfinder.Tests.Random
{
    public class Pcg32Tests
    {
        [Test]
        public void MatchesReferenceImplementation()
        {
            // Output of the official pcg32-demo for pcg32_srandom_r(&rng, 42u, 54u).
            var rng = new Pcg32(42UL, 54UL);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };

            foreach (uint value in expected)
                Assert.AreEqual(value, rng.NextUInt());
        }

        [Test]
        public void SameSeedAndStream_ProduceSameSequence()
        {
            var a = new Pcg32(123UL, 7UL);
            var b = new Pcg32(123UL, 7UL);

            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void DifferentStreams_ProduceDifferentSequences()
        {
            var a = new Pcg32(123UL, 1UL);
            var b = new Pcg32(123UL, 2UL);

            int equal = 0;
            for (int i = 0; i < 100; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                    equal++;
            }
            Assert.Less(equal, 3);
        }

        [Test]
        public void RestoredState_ContinuesSameSequence()
        {
            var original = new Pcg32(99UL, 3UL);
            for (int i = 0; i < 17; i++)
                original.NextUInt();

            var restored = Pcg32.FromState(original.GetState());

            for (int i = 0; i < 100; i++)
                Assert.AreEqual(original.NextUInt(), restored.NextUInt());
        }

        [Test]
        public void FromState_RejectsEvenIncrement()
        {
            Assert.Throws<System.ArgumentException>(() =>
                Pcg32.FromState(new Pcg32State { State = 1UL, Increment = 2UL }));
        }
    }
}
