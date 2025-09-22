using System.Text;

namespace Bit.Core.Utilities;

public static class EnumerationProtectionHelpers
{
    /// <summary>
    /// Use this method to get a consistent int result based on the inputString that is in the range.
    /// The same inputString will always return the same index result based on range input.
    /// </summary>
    /// <param name="hmacKey">Key used to derive the HMAC hash. Use a different key for each usage for optimal security</param>
    /// <param name="inputString">The string to derive an index result</param>
    /// <param name="range">The range of possible index values</param>
    /// <returns>An int between 0 and range - 1</returns>
    public static int GetIndexForInputHash(byte[] hmacKey, string inputString, int range)
    {
        if (hmacKey == null || range <= 0 || hmacKey.Length == 0)
        {
            return 0;
        }
        else
        {
            // Compute the HMAC hash of the salt
            var hmacMessage = Encoding.UTF8.GetBytes(inputString.Trim().ToLowerInvariant());
            using var hmac = new System.Security.Cryptography.HMACSHA256(hmacKey);
            var hmacHash = hmac.ComputeHash(hmacMessage);
            // Convert the hash to a number
            var hashHex = BitConverter.ToString(hmacHash).Replace("-", string.Empty).ToLowerInvariant();
            var hashFirst8Bytes = hashHex[..16];
            var hashNumber = long.Parse(hashFirst8Bytes, System.Globalization.NumberStyles.HexNumber);
            // Find the default KDF value for this hash number
            var hashIndex = (int)(Math.Abs(hashNumber) % range);
            return hashIndex;
        }
    }
}
