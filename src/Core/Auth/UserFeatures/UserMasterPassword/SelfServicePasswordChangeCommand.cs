using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class SelfServicePasswordChangeCommand(
    IUserService userService,
    IMasterPasswordService masterPasswordService,
    IdentityErrorDescriber identityErrorDescriber,
    IEventService eventService,
    IPushNotificationService pushService) : ISelfServicePasswordChangeCommand
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

        var result = await masterPasswordService.SaveUpdateExistingMasterPasswordAsync(user,
            new UpdateExistingPasswordData
            {
                MasterPasswordUnlock = unlockData,
                MasterPasswordAuthentication = authenticationData,
                MasterPasswordHint = masterPasswordHint
            });

        if (result.IsT1)
        {
            return IdentityResult.Failed(result.AsT1);
        }

        await eventService.LogUserEventAsync(user.Id, EventType.User_ChangedPassword);
        await pushService.PushLogOutAsync(user.Id, true);

        return IdentityResult.Success;
    }
}
