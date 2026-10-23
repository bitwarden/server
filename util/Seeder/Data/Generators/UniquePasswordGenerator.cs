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
    /// Whether <see cref="GenerateByIndex"/> has a shape for <paramref name="strength"/>: Fair, Strong and VeryStrong.
    /// </summary>
    internal static bool Supports(PasswordStrength strength) =>
        strength is PasswordStrength.Fair or PasswordStrength.Strong or PasswordStrength.VeryStrong;

    /// <summary>
    /// Builds the password of the login at <paramref name="index"/> in the shape of <paramref name="strength"/>.
    /// The same seed, index and strength always give the same password.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="strength"/> is VeryWeak or Weak.</exception>
    internal string GenerateByIndex(int index, PasswordStrength strength)
    {
        var state = ItemSeedMixer.StartStream(seed, index);

        return strength switch
        {
            // zxcvbn 4.4.2 scores this shape 2 for 73% of indices, 1 for 5% and 3 for 22%; the Fair pool scores 1-3
            PasswordStrength.Fair => $"{Capitalize(PasswordWords.All[(int)(ItemSeedMixer.NextValue(ref state) % (ulong)PasswordWords.All.Length)])}{ItemSeedMixer.NextValue(ref state) % 100_000}",
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
            chars[i] = Alphabets.UnambiguousPassword[(int)(ItemSeedMixer.NextValue(ref state) % (ulong)Alphabets.UnambiguousPassword.Length)];
        }
        return new string(chars);
    }
}
