using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Models.Api.Request;

public class MasterPasswordUnlockDataRequestModel
{
    public required KdfRequestModel Kdf { get; init; }
    [EncryptedString] public required string MasterKeyWrappedUserKey { get; init; }
    [StringLength(256)] public required string Salt { get; init; }

    public MasterPasswordUnlockData ToData()
    {
        return new MasterPasswordUnlockData
        {
            Kdf = Kdf.ToData(),
            MasterKeyWrappedUserKey = MasterKeyWrappedUserKey,
            Salt = Salt
        };
    }

    public static void ThrowIfExistsAndNotMatchingAuthenticationData(
        MasterPasswordAuthenticationDataRequestModel? authenticationData,
        MasterPasswordUnlockDataRequestModel? unlockData)
    {
        if (unlockData != null && authenticationData != null)
        {
            var matches = MatchesAuthenticationData(
                unlockData,
                authenticationData);

            if (!matches)
            {
                throw new Exception("KDF settings and salt must match between authentication and unlock data.");
            }
        }
    }

    private static bool MatchesAuthenticationData(
        MasterPasswordUnlockDataRequestModel unlockData,
        MasterPasswordAuthenticationDataRequestModel authenticationData)
    {
        var kdfMatches = unlockData.Kdf.Equals(authenticationData.Kdf);
        var saltMatches = unlockData.Salt == authenticationData.Salt;

        return kdfMatches && saltMatches;
    }
}
