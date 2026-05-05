using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

internal static class MasterPasswordTestData
{
    internal static MasterPasswordUnlockData CreateUnlockData(
        KdfSettings kdfSettings, string salt, string masterKeyWrappedUserKey) =>
        new()
        {
            Salt = salt,
            MasterKeyWrappedUserKey = masterKeyWrappedUserKey,
            Kdf = kdfSettings
        };

    internal static MasterPasswordAuthenticationData CreateAuthenticationData(
        KdfSettings kdfSettings, string salt, string masterPasswordAuthenticationHash) =>
        new()
        {
            Salt = salt,
            MasterPasswordAuthenticationHash = masterPasswordAuthenticationHash,
            Kdf = kdfSettings
        };
}
