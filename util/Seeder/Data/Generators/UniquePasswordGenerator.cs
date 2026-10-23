using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;

namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Deterministic per-item passwords in the style of the Fair, Strong and VeryStrong tiers, so those logins don't share
/// a password. Breach checks run once per distinct password, so a vault built only from the shared pools understates
/// them. VeryWeak and Weak have no generator: zxcvbn scores no unique password as low as the pooled ones.
/// </summary>
internal sealed class UniquePasswordGenerator(int seed)
{
    /// <summary>
    /// Builds the password of the login at <paramref name="index"/> in the shape of <paramref name="strength"/>.
    /// The same seed, index and strength always give the same password.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="strength"/> is VeryWeak or Weak.</exception>
    internal string GenerateByIndex(int index, PasswordStrength strength)
    {
        // SplitMix64 rather than the siblings' Randomizer(_seed + index): System.Random reseeded with consecutive
        // values gives correlated first draws. Mixing the (seed, index) pair first keeps neighbouring indices from
        // sharing one stream offset by a step
        var start = ((ulong)(uint)seed << 32) | (uint)index;
        var state = Next(ref start);

        return strength switch
        {
            // zxcvbn 4.4.2 scores this shape 2 for 73% of indices, 1 for 5% and 3 for 22%; the Fair pool scores 1-3
            PasswordStrength.Fair => $"{Capitalize(PasswordWords.All[(int)(Next(ref state) % (ulong)PasswordWords.All.Length)])}{Next(ref state) % 100_000}",
            PasswordStrength.Strong => RandomChars(ref state, 16),
            PasswordStrength.VeryStrong => RandomChars(ref state, 22),
            _ => throw new ArgumentOutOfRangeException(nameof(strength), strength, null)
        };
    }

    private static string Capitalize(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    private static string RandomChars(ref ulong state, int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabets.UnambiguousPassword[(int)(Next(ref state) % (ulong)Alphabets.UnambiguousPassword.Length)];
        }
        return new string(chars);
    }

    private static ulong Next(ref ulong state)
    {
        var z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
