namespace Bit.Seeder.Data.Static;

/// <summary>
/// The character sets that generated secrets and passwords are drawn from.
/// </summary>
internal static class Alphabets
{
    /// <summary>
    /// RFC 4648 base32, the alphabet of TOTP secrets.
    /// </summary>
    internal const string Base32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// Letters, digits and symbols for random passwords, without the look-alikes I, O, l, 0 and 1.
    /// </summary>
    internal const string UnambiguousPassword = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*?";
}
