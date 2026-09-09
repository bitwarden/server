namespace Bit.Core.KeyManagement.Models.Data;

/// <summary>
/// A key id is a hex-encoded string unique to a key. A symmetric key, a public-encryption-keypair
/// and a signature-keypair all have unique key ids.
/// </summary>
public class KeyId
{
    private string HexEncodedKeyId { get; }

    KeyId(string hexEncodedKeyId)
    {
        HexEncodedKeyId = hexEncodedKeyId;
    }

    /// <summary>
    /// Creates a KeyId from a hex-encoded string. This MUST be 32 characters long (16 bytes).
    /// Null passes through as null, since a key id is optional in most places it is carried.
    /// </summary>
    public static KeyId? FromHexEncodedString(string? hexEncodedKeyId)
    {
        if (hexEncodedKeyId is null)
        {
            return null;
        }

        if (hexEncodedKeyId.Length != 32)
        {
            throw new ArgumentException("Key id must be 32 characters long.", nameof(hexEncodedKeyId));
        }

        foreach (var character in hexEncodedKeyId)
        {
            var isLowercaseHex = character is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isLowercaseHex)
            {
                throw new ArgumentException("Key id must be a lowercase hex-encoded string.", nameof(hexEncodedKeyId));
            }
        }

        return new KeyId(hexEncodedKeyId);
    }

    public override string ToString() => HexEncodedKeyId;

    public override bool Equals(object? obj)
    {
        if (obj is not KeyId other)
        {
            return false;
        }

        return HexEncodedKeyId.Equals(other.HexEncodedKeyId);
    }

    public override int GetHashCode()
    {
        return HexEncodedKeyId.GetHashCode();
    }
}
