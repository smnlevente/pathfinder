using System;

namespace Pathfinder.Core.Random
{
    /// <summary>
    /// A node in the seed tree. Derive children from context, then create generators at the leaves:
    /// <code>
    /// var waves = runSeed.Derive(SeedLabels.Level).Derive(galaxyIndex).Derive(nodeId)
    ///                    .CreateRandom(SeedLabels.Waves);
    /// </code>
    /// A child depends only on its parent and its own label or index. A level's content is therefore
    /// fixed by its position in the run, no matter which other levels were generated first.
    /// </summary>
    public readonly struct Seed : IEquatable<Seed>
    {
        // Keeps generator seeds apart from child seed values derived with the same label.
        const ulong StreamDomain = 0x53545245414D5321UL; // "STREAMS!"

        public ulong Value { get; }

        public Seed(ulong value) => Value = value;

        public Seed Derive(string label) => new Seed(SeedHash.Combine(Value, SeedHash.Hash(label)));

        public Seed Derive(int index) => new Seed(SeedHash.Combine(Value, unchecked((ulong)(long)index)));

        /// <summary>Creates an independent generator for one consumer, e.g. <see cref="SeedLabels.Loot"/>.</summary>
        public Pcg32 CreateRandom(string stream)
        {
            ulong streamId = SeedHash.Hash(stream);
            return new Pcg32(SeedHash.Combine(Value ^ StreamDomain, streamId), streamId);
        }

        public bool Equals(Seed other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Seed other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("X16");

        public static bool operator ==(Seed left, Seed right) => left.Equals(right);
        public static bool operator !=(Seed left, Seed right) => !left.Equals(right);
    }

    /// <summary>
    /// Well-known labels for <see cref="Seed.Derive(string)"/> and <see cref="Seed.CreateRandom"/>.
    /// Renaming one changes every seeded run.
    /// </summary>
    public static class SeedLabels
    {
        public const string Map = "map";
        public const string Level = "level";
        public const string Player = "player";
        public const string Enemy = "enemy";

        public const string Layout = "layout";
        public const string Waves = "waves";
        public const string Loot = "loot";
        public const string Weapon = "weapon";
        public const string Ai = "ai";

        /// <summary>Visual-only randomness. Clients may draw from it freely without risking desync.</summary>
        public const string Cosmetic = "cosmetic";
    }
}
