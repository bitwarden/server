using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class SelfServicePasswordChangeCommand(
    IUserService userService,
    IMasterPasswordService masterPasswordService,
    IdentityErrorDescriber identityErrorDescriber) : ISelfServicePasswordChangeCommand
{
    public async Task<IdentityResult> ChangePasswordAsync(
        User user,
        string masterPasswordHash,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        string? masterPasswordHint)
    {
        if (!await userService.CheckPasswordAsync(user, masterPasswordHash))
        {
            return IdentityResult.Failed(identityErrorDescriber.PasswordMismatch());
        }

        return await masterPasswordService.SaveUpdateExistingMasterPasswordAsync(user, new UpdateExistingPasswordData
        {
            MasterPasswordUnlock = unlockData,
            MasterPasswordAuthentication = authenticationData,
            MasterPasswordHint = masterPasswordHint
        });
    }
}
