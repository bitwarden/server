using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.UserKey.Models.Data;

public class PasswordChangeAndRotateUserAccountKeysData
{
    // Authentication for this request
    public required string OldMasterKeyAuthenticationHash { get; set; }

    public required MasterPasswordAuthenticationData MasterPasswordAuthenticationData { get; set; }
    public required MasterPasswordUnlockData MasterPasswordUnlockData { get; set; }
    public string? MasterPasswordHint { get; set; }

    public required BaseRotateUserAccountKeysData BaseData { get; set; }

    public void ValidateForUser(User user)
    {
        try
        {
            MasterPasswordAuthenticationData.ValidateSaltUnchangedForUser(user);
            MasterPasswordAuthenticationData.Kdf.ValidateUnchangedForUser(user);
            MasterPasswordUnlockData.ValidateSaltUnchangedForUser(user);
            MasterPasswordUnlockData.Kdf.ValidateUnchangedForUser(user);
        }
        catch
        {
            throw new InvalidOperationException("The provided master password unlock data is not valid for this user.");
        }
    }
}
