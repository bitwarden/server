using System.Security.Cryptography;
using System.Text;

namespace Bit.Seeder.Helpers;

/// <summary>
/// Provides stable, deterministic hash functions for seeding.
/// Unlike string.GetHashCode(), these are consistent across processes and runs.
/// </summary>
internal static class StableHash
{
    /// <summary>
    /// Converts a string to a stable 32-bit integer using SHA256.
    /// The same input always produces the same output, regardless of process or runtime.
    /// </summary>
    /// <param name="value">The string to hash.</param>
    /// <returns>A deterministic 32-bit integer derived from the input by taking the first 4 bytes as a stable int32.</returns>
    internal static int ToInt32(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt32(bytes, 0);
    }
}
