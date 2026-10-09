using System;

namespace Pathfinder.Core.Random
{
    /// <summary>
    /// Stable 64-bit hashing used to derive seeds. The output is part of the save and replay
    /// format: changing anything here changes every generated run, and golden-value tests will fail.
    /// </summary>
    public static class SeedHash
    {
        const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        const ulong FnvOffset = 14695981039346656037UL;
        const ulong FnvPrime = 1099511628211UL;

        /// <summary>SplitMix64 finalizer: a strong bijective bit mixer.</summary>
        public static ulong Mix(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Order-sensitive combination: <c>Combine(a, b) != Combine(b, a)</c> in general.</summary>
        public static ulong Combine(ulong seed, ulong value) => Mix(seed ^ Mix(unchecked(value + GoldenGamma)));

        /// <summary>
        /// FNV-1a 64 of the string's UTF-16LE bytes. Use this instead of <see cref="string.GetHashCode"/>,
        /// which is not stable across runtimes or processes.
        /// </summary>
        public static ulong Hash(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            ulong hash = FnvOffset;
            unchecked
            {
                foreach (char c in text)
                {
                    hash = (hash ^ (byte)c) * FnvPrime;
                    hash = (hash ^ (byte)(c >> 8)) * FnvPrime;
                }
            }
            return hash;
        }
    }
}
