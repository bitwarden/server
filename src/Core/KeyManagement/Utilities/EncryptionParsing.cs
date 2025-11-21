using Bit.Core.Enums;

namespace Bit.Core.KeyManagement.Utilities;

public static class EncryptionParsing
{
    /// <summary>
    /// Helper method to convert an encryption type string to an enum value.
    /// </summary>
    public static EncryptionType GetEncryptionType(string encString)
    {
        var parts = encString.Split('.');
        if (parts.Length == 1)
        {
            throw new ArgumentException("Invalid encryption type string.");
        }
        if (byte.TryParse(parts[0], out var encryptionTypeNumber))
        {
            if (Enum.IsDefined(typeof(EncryptionType), encryptionTypeNumber))
            {
                return (EncryptionType)encryptionTypeNumber;
            }
        }
        throw new ArgumentException("Invalid encryption type string.");
    }
}
