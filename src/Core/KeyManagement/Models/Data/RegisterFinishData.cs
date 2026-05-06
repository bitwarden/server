namespace Bit.Core.KeyManagement.Models.Data;

public class RegisterFinishData
{
    public required UserAccountKeysData UserAccountKeysData { get; set; }
    public required KdfSettings Kdf { get; init; }
    public required string MasterKeyWrappedUserKey { get; init; }

    public required string MasterPasswordAuthenticationHash { get; init; }
    public string? Salt { get; init; }

    public bool IsV2Encryption()
    {
        return UserAccountKeysData.IsV2Encryption();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not RegisterFinishData other)
        {
            return false;
        }

        return UserAccountKeysData.Equals(other.UserAccountKeysData) &&
               Kdf.Equals(other.Kdf) &&
               MasterKeyWrappedUserKey == other.MasterKeyWrappedUserKey &&
               MasterPasswordAuthenticationHash == other.MasterPasswordAuthenticationHash &&
               Salt == other.Salt &&
               IsV2Encryption() == other.IsV2Encryption();
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(UserAccountKeysData, Kdf, MasterKeyWrappedUserKey, MasterPasswordAuthenticationHash, Salt);
    }
}
