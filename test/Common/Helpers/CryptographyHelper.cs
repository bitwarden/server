using System.Security.Cryptography;
using System.Text;

namespace Bit.Test.Common.Helpers;

public class CryptographyHelper
{
    /// <summary>
    /// Returns a hex-encoded, SHA256 hash for the given string
    /// </summary>
    public static string HashAndEncode(string text)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var hashEncoded = Convert.ToHexString(hashBytes).ToUpperInvariant();
        return hashEncoded;
    }
}
