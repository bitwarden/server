#nullable enable

using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.Kdf;

/// <summary>
/// </summary>
public interface IChangeKdfCommand
{
    public Task<IdentityResult> ChangeKdfAsync(User user, string masterPasswordAuthenticationHash, string newMasterPasswordAuthenticationHash,
        string masterKeyEncryptedUserKey, KdfSettings kdf, MasterPasswordAuthenticationData authenticationData, MasterPasswordUnlockData unlockData);
}
