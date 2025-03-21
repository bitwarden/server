using System.Security.Cryptography;

namespace Bit.Core.Auth.Utilities;

public static class GuidUtilities
{
    public static bool TryParseBytes(ReadOnlySpan<byte> bytes, out Guid guid)
    {
        try
        {
            guid = new Guid(bytes);
            return true;
        }
        catch
        {
            guid = Guid.Empty;
            return false;
        }
    }

    /// <summary>
    /// Creates a genuinely random GUID as the login session IDs for opaque need to be cryptographically secure.
    /// </summary>
    public static Guid MakeCryptoGuid()
    {
        // Get 16 cryptographically random bytes
        var data = RandomNumberGenerator.GetBytes(16);

        // Mark it as a version 4 GUID
        data[7] = (byte)((data[7] | (byte)0x40) & (byte)0x4f);
        data[8] = (byte)((data[8] | (byte)0x80) & (byte)0xbf);

        return new Guid(data);
    }
}

