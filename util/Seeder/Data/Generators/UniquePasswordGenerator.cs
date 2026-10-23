using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Deterministic per-item passwords in the style of the Fair, Strong and VeryStrong tiers, so those logins don't share
/// a password. Breach checks run once per distinct password, so a vault built only from the shared pools understates
/// them. VeryWeak and Weak have no generator: zxcvbn scores no unique password as low as the pooled ones.
/// </summary>
internal sealed class UniquePasswordGenerator(int seed)
{
    private readonly int _seed = seed;

    private static readonly string[] _words =
    [
        "dragon", "monkey", "sunshine", "football", "shadow", "master", "flower", "summer", "winter", "spring",
        "autumn", "coffee", "tiger", "falcon", "rocket", "pirate", "garden", "silver", "golden", "hunter",
        "pepper", "cookie", "banana", "orange", "purple", "yellow", "thunder", "breeze", "harbor", "meadow",
        "castle", "forest", "island", "planet", "comet", "anchor", "violet", "maple", "cedar", "willow",
        "otter", "jaguar", "panda", "rabbit", "turtle", "dolphin", "eagle", "lion"
    ];

    private const string StrongChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*?";

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
        var start = ((ulong)(uint)_seed << 32) | (uint)index;
        var state = Next(ref start);

        return strength switch
        {
            // zxcvbn 4.4.2 scores this shape 2 for 73% of indices, 1 for 5% and 3 for 22%; the Fair pool scores 1-3
            PasswordStrength.Fair => $"{Capitalize(_words[(int)(Next(ref state) % (ulong)_words.Length)])}{Next(ref state) % 100_000}",
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
            chars[i] = StrongChars[(int)(Next(ref state) % (ulong)StrongChars.Length)];
        }
        return new string(chars);
    }

    // SplitMix64: cheap, deterministic, well-spread
    private static ulong Next(ref ulong state)
    {
        var z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
