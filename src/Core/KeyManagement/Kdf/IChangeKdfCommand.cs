#nullable enable

using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.Kdf;

/// <summary>
/// Command to change the Key Derivation Function (KDF) settings for a user. This includes 
/// changing the masterpassword authentication hash, and the masterkey encrypted userkey.
/// The salt must not change during the KDF change.
/// </summary>
public interface IChangeKdfCommand
{
    public Task<IdentityResult> ChangeKdfAsync(User user, string masterPasswordAuthenticationHash, string newMasterPasswordAuthenticationHash,
        string masterKeyEncryptedUserKey, KdfSettings kdf, MasterPasswordAuthenticationData authenticationData, MasterPasswordUnlockData unlockData);
}
