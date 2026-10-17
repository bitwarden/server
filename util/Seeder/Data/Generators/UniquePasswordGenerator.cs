using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Deterministic per-item passwords in the style of each strength tier, so most logins don't share a password.
/// Breach checks run once per distinct password, so a vault built only from the shared pools understates them.
/// </summary>
internal static class UniquePasswordGenerator
{
    private static readonly string[] _words =
    [
        "dragon", "monkey", "sunshine", "football", "shadow", "master", "flower", "summer", "winter", "spring",
        "autumn", "coffee", "tiger", "falcon", "rocket", "pirate", "garden", "silver", "golden", "hunter",
        "pepper", "cookie", "banana", "orange", "purple", "yellow", "thunder", "breeze", "harbor", "meadow",
        "castle", "forest", "island", "planet", "comet", "anchor", "violet", "maple", "cedar", "willow",
        "otter", "jaguar", "panda", "rabbit", "turtle", "dolphin", "eagle", "lion"
    ];

    private const string Symbols = "!@#$%&*?";

    private const string StrongChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*?";

    internal static string Generate(int index, PasswordStrength strength)
    {
        var state = (ulong)(uint)index * 0x9E3779B97F4A7C15UL + 0xD1B54A32D192ED03UL;
        var word = _words[(int)(Next(ref state) % (ulong)_words.Length)];
        var number = (int)(Next(ref state) % 100_000);

        return strength switch
        {
            PasswordStrength.VeryWeak => $"{word}{number}",
            PasswordStrength.Weak => $"{Capitalize(word)}{number}",
            PasswordStrength.Fair => $"{Capitalize(word)}{Capitalize(_words[(int)(Next(ref state) % (ulong)_words.Length)])}{number}{Symbols[(int)(Next(ref state) % (ulong)Symbols.Length)]}",
            PasswordStrength.Strong => Random(ref state, 16),
            PasswordStrength.VeryStrong => Random(ref state, 22),
            _ => throw new ArgumentOutOfRangeException(nameof(strength), strength, null)
        };
    }

    private static string Capitalize(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    private static string Random(ref ulong state, int length)
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
