using Bit.Core.Auth.UserFeatures.TempPassword.Interfaces;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.TempPassword;

public class UpdateTempPasswordCommand(
    IMasterPasswordService masterPasswordService,
    IUserRepository userRepository,
    IMailService mailService,
    IEventService eventService,
    IPushNotificationService pushService) : IUpdateTempPasswordCommand
{
    public async Task<IdentityResult> UpdateTempPasswordAsync(
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        string? masterPasswordHint)
    {
        if (!user.ForcePasswordReset)
        {
            throw new BadRequestException("User does not have a temporary password to update.");
        }

        var result = await masterPasswordService.OnlyMutateUserUpdateExistingMasterPasswordAsync(user, new UpdateExistingPasswordData
        {
            MasterPasswordUnlock = unlockData,
            MasterPasswordAuthentication = authenticationData,
            MasterPasswordHint = masterPasswordHint,
        });
        if (!result.Succeeded)
        {
            return result;
        }

        user.ForcePasswordReset = false;

        await userRepository.ReplaceAsync(user);
        await mailService.SendUpdatedTempPasswordEmailAsync(user.Email, user.Name ?? string.Empty);
        await eventService.LogUserEventAsync(user.Id, EventType.User_UpdatedTempPassword);
        await pushService.PushLogOutAsync(user.Id);

        return IdentityResult.Success;
    }
}
