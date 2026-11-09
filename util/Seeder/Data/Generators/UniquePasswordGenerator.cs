using Bit.Seeder.Data.Enums;
using Bit.Seeder.Data.Static;

namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Deterministic per-item passwords in the style of the Fair, Strong and VeryStrong tiers, so those logins are
/// practically unique rather than sharing a pooled password. Breach checks run once per distinct password, so a vault
/// built only from the shared pools understates them. Strong and VeryStrong are half random characters and half
/// passphrases, as the pools are. VeryWeak and Weak have no generator: zxcvbn scores no unique password as low as the
/// pooled ones.
/// </summary>
internal sealed class UniquePasswordGenerator(int seed)
{
    private const string _passphraseSeparators = "-.#@$";
    private const int _strongWords = 3;
    private const int _veryStrongMinWords = 4;
    private const int _veryStrongWordRange = 2;

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
            // zxcvbn 4.4.2 scores this shape 1 for 4% of indices, 2 for 72% and 3 for 24%; the Fair pool scores 1-3
            PasswordStrength.Fair => $"{Capitalize(NextWord(ref state, PasswordWords.Common))}{ItemSeedMixer.NextValue(ref state) % 100_000}",
            PasswordStrength.Strong => IsPassphrase(ref state)
                ? Passphrase(ref state, _strongWords)
                : RandomChars(ref state, 16),
            PasswordStrength.VeryStrong => IsPassphrase(ref state)
                ? Passphrase(ref state, _veryStrongMinWords + (int)(ItemSeedMixer.NextValue(ref state) % _veryStrongWordRange))
                : RandomChars(ref state, 22),
            _ => throw new ArgumentOutOfRangeException(nameof(strength), strength, null)
        };
    }

    private static string Capitalize(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    private static bool IsPassphrase(ref ulong state) => ItemSeedMixer.NextValue(ref state) % 2 == 0;

    private static string NextWord(ref ulong state, string[] words) =>
        words[(int)(ItemSeedMixer.NextValue(ref state) % (ulong)words.Length)];

    private static string Passphrase(ref ulong state, int wordCount)
    {
        var separator = _passphraseSeparators[(int)(ItemSeedMixer.NextValue(ref state) % (ulong)_passphraseSeparators.Length)];
        var words = new string[wordCount];
        for (var i = 0; i < wordCount; i++)
        {
            words[i] = NextWord(ref state, PasswordWords.All);
        }
        return string.Join(separator, words);
    }

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
