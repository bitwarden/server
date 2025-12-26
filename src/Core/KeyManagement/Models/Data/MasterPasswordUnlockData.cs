using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.KeyManagement.Models.Data;

public class MasterPasswordUnlockData
{
    public required KdfSettings Kdf { get; init; }
    public required string MasterKeyWrappedUserKey { get; init; }
    public required string Salt { get; init; }

    public void ValidateSaltUnchangedForUser(User user)
    {
        if (user.GetMasterPasswordSalt() != Salt)
        {
            throw new BadRequestException("Invalid master password salt.");
        }
    }

    public static void ThrowIfExistsAndNotMatchingAuthenticationData(
        MasterPasswordAuthenticationData? authenticationData,
        MasterPasswordUnlockData? unlockData)
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
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData)
    {
        var kdfMatches = unlockData.Kdf.Equals(authenticationData.Kdf);
        var saltMatches = unlockData.Salt == authenticationData.Salt;

        return kdfMatches && saltMatches;
    }
}
