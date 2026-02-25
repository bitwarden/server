using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;

public class AdminRecoverAccountCommand(
    IOrganizationRepository organizationRepository,
    IPolicyQuery policyQuery,
    IUserRepository userRepository,
    IMailService mailService,
    IEventService eventService,
    IPushNotificationService pushNotificationService,
    IUserService userService,
    TimeProvider timeProvider) : IAdminRecoverAccountCommand
{
    public async Task<IdentityResult> RecoverAccountAsync(Guid orgId,
        OrganizationUser organizationUser, MasterPasswordAuthenticationData authenticationData,
        MasterPasswordUnlockData unlockData)
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
            string.IsNullOrEmpty(organizationUser.ResetPasswordKey) ||
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

        // Validate submitted KDF settings match the user's stored KDF settings
        try
        {
            unlockData.Kdf.ValidateUnchangedForUser(user);
        }
        catch (ArgumentException)
        {
            throw new BadRequestException("Invalid KDF settings.");
        }

        // Validate submitted salt matches the user's stored salt
        // After PM-21925: Uncomment below block when MasterPasswordSalt exists on User.
        // When the user has no stored salt, persist it from the request data.
        // When the user already has a stored salt, validate it matches the request data.
        // if (string.IsNullOrEmpty(user.MasterPasswordSalt))
        // {
        //     user.MasterPasswordSalt = authenticationData.Salt;
        // }
        // else
        // {
        //     unlockData.ValidateSaltUnchangedForUser(user);
        //     authenticationData.ValidateSaltUnchangedForUser(user);
        // }
        unlockData.ValidateSaltUnchangedForUser(user);
        authenticationData.ValidateSaltUnchangedForUser(user);

        var result = await userService.UpdatePasswordHash(user, authenticationData.MasterPasswordAuthenticationHash);
        if (!result.Succeeded)
        {
            return result;
        }

        user.RevisionDate = user.AccountRevisionDate = timeProvider.GetUtcNow().UtcDateTime;
        user.LastPasswordChangeDate = user.RevisionDate;
        user.ForcePasswordReset = true;
        user.Key = unlockData.MasterKeyWrappedUserKey;

        await userRepository.ReplaceAsync(user);
        await mailService.SendAdminResetPasswordEmailAsync(user.Email, user.Name, org.DisplayName());
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_AdminResetPassword);
        await pushNotificationService.PushLogOutAsync(user.Id);

        return IdentityResult.Success;
    }
}
