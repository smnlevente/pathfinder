using System;
using System.Security.Cryptography;
using System.Text;

namespace Pathfinder.Core.Random
{
    /// <summary>
    /// Creates run seeds and converts them to and from short shareable codes,
    /// e.g. <c>7-K3QP-9ZD2-XW4M</c>.
    /// </summary>
    public static class RunSeed
    {
        // Crockford Base32: no I, L, O or U, so codes are easy to read aloud and type.
        const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        /// <summary>Number of symbols in a code: 4 bits in the first symbol, then 12 × 5 bits.</summary>
        public const int CodeLength = 13;

        /// <summary>
        /// A fresh, unpredictable seed for a new run. This is the only place a cryptographic RNG is
        /// used. Everything after this point is deterministic.
        /// </summary>
        public static Seed Generate()
        {
            var bytes = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return new Seed(BitConverter.ToUInt64(bytes, 0));
        }

        public static string ToCode(Seed seed)
        {
            ulong value = seed.Value;
            var code = new StringBuilder(CodeLength + 3);
            code.Append(Alphabet[(int)(value >> 60)]);
            for (int i = 0; i < CodeLength - 1; i++)
            {
                if (i % 4 == 0)
                    code.Append('-');
                code.Append(Alphabet[(int)((value >> (55 - 5 * i)) & 31UL)]);
            }
            return code.ToString();
        }

        /// <summary>
        /// Parses a code produced by <see cref="ToCode"/>. Accepts lowercase, ignores spaces and
        /// hyphens, and reads the easily confused I/L as 1 and O as 0.
        /// </summary>
        public static bool TryParse(string code, out Seed seed)
        {
            seed = default;
            if (code == null)
                return false;

            ulong value = 0;
            int symbols = 0;
            foreach (char raw in code)
            {
                if (raw == '-' || raw == ' ')
                    continue;

                int digit = DecodeSymbol(raw);
                if (digit < 0 || symbols >= CodeLength)
                    return false;

                // The first symbol carries only the top 4 bits of the 64-bit value.
                if (symbols == 0 && digit > 15)
                    return false;

                value = (value << (symbols == 0 ? 0 : 5)) | (uint)digit;
                symbols++;
            }

            if (symbols != CodeLength)
                return false;

            seed = new Seed(value);
            return true;
        }

        static int DecodeSymbol(char c)
        {
            c = char.ToUpperInvariant(c);
            switch (c)
            {
                case 'I':
                case 'L':
                    return 1;
                case 'O':
                    return 0;
                default:
                    return Alphabet.IndexOf(c);
            }
        }
    }
}
