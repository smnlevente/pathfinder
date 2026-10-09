using System;

namespace Pathfinder.Core.Random
{
    /// <summary>
    /// PCG32 (PCG-XSH-RR 64/32) by Melissa O'Neill, see https://www.pcg-random.org.
    /// Small, fast, statistically strong, and fully specified, so it produces identical
    /// sequences on Mono, IL2CPP and .NET. Each (seed, stream) pair is an independent sequence.
    /// </summary>
    /// <remarks>Not cryptographically secure, by design: gameplay randomness must be reproducible.</remarks>
    public sealed class Pcg32 : IRandom
    {
        const ulong Multiplier = 6364136223846793005UL;

        ulong _state;
        readonly ulong _increment;

        /// <summary>Equivalent to the reference <c>pcg32_srandom_r(initstate: seed, initseq: stream)</c>.</summary>
        public Pcg32(ulong seed, ulong stream)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        Pcg32(Pcg32State state)
        {
            _state = state.State;
            _increment = state.Increment;
        }

        /// <summary>Restores a generator captured with <see cref="GetState"/>, e.g. from a mid-run save.</summary>
        public static Pcg32 FromState(Pcg32State state)
        {
            if ((state.Increment & 1UL) == 0UL)
                throw new ArgumentException("PCG32 increment must be odd; the state is corrupt.", nameof(state));
            return new Pcg32(state);
        }

        public Pcg32State GetState() => new Pcg32State { State = _state, Increment = _increment };

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
        }
    }

    /// <summary>Serializable snapshot of a <see cref="Pcg32"/> generator.</summary>
    [Serializable]
    public struct Pcg32State : IEquatable<Pcg32State>
    {
        public ulong State;
        public ulong Increment;

        public bool Equals(Pcg32State other) => State == other.State && Increment == other.Increment;
        public override bool Equals(object obj) => obj is Pcg32State other && Equals(other);
        public override int GetHashCode() => unchecked((State.GetHashCode() * 397) ^ Increment.GetHashCode());
    }
}
