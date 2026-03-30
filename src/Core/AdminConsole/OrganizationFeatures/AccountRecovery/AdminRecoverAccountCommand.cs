using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
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

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;

public class AdminRecoverAccountCommand(IOrganizationRepository organizationRepository,
    IPolicyQuery policyQuery,
    IUserRepository userRepository,
    IMailService mailService,
    IEventService eventService,
    IPushNotificationService pushNotificationService,
    IUserService userService,
    TimeProvider timeProvider,
    IMasterPasswordService masterPasswordService) : IAdminRecoverAccountCommand
{
    public async Task<IdentityResult> RecoverAccountAsync(Guid orgId,
        OrganizationUser organizationUser, string newMasterPassword, string key)
    {
        // Org must be able to use reset password
        var org = await organizationRepository.GetByIdAsync(orgId);
        if (org == null || !org.UseResetPassword)
        {
            throw new BadRequestException("Organization does not allow password reset.");
        }

        // Enterprise policy must be enabled
        var resetPasswordPolicy = await policyQuery.RunAsync(orgId, PolicyType.ResetPassword);
        if (!resetPasswordPolicy.Enabled)
        {
            throw new BadRequestException("Organization does not have the password reset policy enabled.");
        }

        // Org User must be confirmed and have a ResetPasswordKey
        if (organizationUser == null ||
            organizationUser.Status != OrganizationUserStatusType.Confirmed ||
            organizationUser.OrganizationId != orgId ||
            !organizationUser.IsEnrolledInAccountRecovery() ||
            !organizationUser.UserId.HasValue)
        {
            throw new BadRequestException("Organization User not valid");
        }

        var user = await userService.GetUserByIdAsync(organizationUser.UserId.Value);
        if (user == null)
        {
            throw new NotFoundException();
        }

        if (user.UsesKeyConnector)
        {
            throw new BadRequestException("Cannot reset password of a user with Key Connector.");
        }

        var result = await userService.UpdatePasswordHash(user, newMasterPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        user.RevisionDate = user.AccountRevisionDate = timeProvider.GetUtcNow().UtcDateTime;
        user.LastPasswordChangeDate = user.RevisionDate;
        user.ForcePasswordReset = true;
        user.Key = key;

        await userRepository.ReplaceAsync(user);
        await mailService.SendAdminResetPasswordEmailAsync(user.Email, user.Name, org.DisplayName(), true, false);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_AdminResetPassword);
        await pushNotificationService.PushLogOutAsync(user.Id);

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> RecoverAccountAsync(
        Guid orgId,
        OrganizationUser organizationUser,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData)
    {
        // Org must be able to use reset password
        var org = await organizationRepository.GetByIdAsync(orgId);
        if (org == null || !org.UseResetPassword)
        {
            throw new BadRequestException("Organization does not allow password reset.");
        }

        // Enterprise policy must be enabled
        var resetPasswordPolicy = await policyQuery.RunAsync(orgId, PolicyType.ResetPassword);
        if (!resetPasswordPolicy.Enabled)
        {
            throw new BadRequestException("Organization does not have the password reset policy enabled.");
        }

        // Org User must be confirmed and have a ResetPasswordKey
        if (organizationUser == null ||
            organizationUser.Status != OrganizationUserStatusType.Confirmed ||
            organizationUser.OrganizationId != orgId ||
            !organizationUser.IsEnrolledInAccountRecovery() ||
            !organizationUser.UserId.HasValue)
        {
            throw new BadRequestException("Organization User not valid");
        }

        var user = await userService.GetUserByIdAsync(organizationUser.UserId.Value);
        if (user == null)
        {
            throw new NotFoundException();
        }

        if (user.UsesKeyConnector)
        {
            throw new BadRequestException("Cannot reset password of a user with Key Connector.");
        }

        IdentityResult mutationResult;

        // We can recover an account for users who both have a master password and
        // those who do not. TDE users can be recovered and will not have a password
        if (user.HasMasterPassword())
        {
            mutationResult = await masterPasswordService.UpdateExistingMasterPasswordAsync(
                user,
                new UpdateExistingPasswordData
                {
                    MasterPasswordUnlockData = unlockData,
                    MasterPasswordAuthenticationData = authenticationData,
                });
        }
        else
        {
            mutationResult = await masterPasswordService.SetInitialMasterPasswordAsync(
                user,
                new SetInitialPasswordData
                {
                    MasterPasswordUnlockData = unlockData,
                    MasterPasswordAuthenticationData = authenticationData,
                });
        }

        if (!mutationResult.Succeeded)
        {
            return mutationResult;
        }

        // Extra modifications for this particular scenario to the user object
        user.ForcePasswordReset = true;

        await userRepository.ReplaceAsync(user);

        await mailService.SendAdminResetPasswordEmailAsync(user.Email, user.Name, org.DisplayName());
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_AdminResetPassword);
        await pushNotificationService.PushLogOutAsync(user.Id);

        return IdentityResult.Success;
    }
}
