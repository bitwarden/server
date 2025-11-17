using Bit.Core.Enums;

namespace Bit.Core.KeyManagement.Utilities;

public static class EncryptionParsing
{
    /// <summary>
    /// Helper method to convert an encryption type string to an enum value.
    /// Accepts formats like "Header.iv|ct|mac" or "Header" COSE format.
    /// </summary>
    public static EncryptionType GetEncryptionType(string encString)
    {
        if (string.IsNullOrWhiteSpace(encString))
        {
            throw new ArgumentException("Encrypted string cannot be null or empty.", nameof(encString));
        }

        var parts = encString.Split('.');
        if (parts.Length == 1)
        {
            // No header detected; assume AES CBC variants based on number of pieces
            var splitParts = encString.Split('|');
            if (splitParts.Length == 3)
            {
                return EncryptionType.AesCbc128_HmacSha256_B64;
            }

            return EncryptionType.AesCbc256_B64;
        }

        // Try parse header as numeric, then as enum name, else fail
        if (byte.TryParse(parts[0], out var encryptionTypeNumber))
        {
            return (EncryptionType)encryptionTypeNumber;
        }

        if (Enum.TryParse(parts[0], out EncryptionType parsed))
        {
            return parsed;
        }

        throw new ArgumentException("Invalid encryption type header.", nameof(encString));
    }
}


