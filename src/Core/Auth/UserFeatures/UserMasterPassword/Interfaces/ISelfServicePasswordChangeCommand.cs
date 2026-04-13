using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

public interface ISelfServicePasswordChangeCommand
{
    Task<IdentityResult> ChangePasswordAsync(
        User user,
        string masterPasswordHash,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        string? masterPasswordHint);
}
