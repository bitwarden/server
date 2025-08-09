using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;

public class AdminRecoverAccountCommand(IOrganizationRepository organizationRepository,
    IPolicyRepository policyRepository,
    IOrganizationUserRepository organizationUserRepository,
    IUserRepository userRepository,
    IMailService mailService,
    IEventService eventService,
    IPushNotificationService pushNotificationService,
    IUserService userService) : IAdminRecoverAccountCommand
{
    public async Task<IdentityResult> AdminResetPasswordAsync(OrganizationUserType callingUserType, Guid orgId, Guid id, string newMasterPassword, string key)
    {
        // Org must be able to use reset password
        var org = await organizationRepository.GetByIdAsync(orgId);
        if (org == null || !org.UseResetPassword)
        {
            throw new BadRequestException("Organization does not allow password reset.");
        }

        // Enterprise policy must be enabled
        var resetPasswordPolicy =
            await policyRepository.GetByOrganizationIdTypeAsync(orgId, PolicyType.ResetPassword);
        if (resetPasswordPolicy == null || !resetPasswordPolicy.Enabled)
        {
            throw new BadRequestException("Organization does not have the password reset policy enabled.");
        }

        // Org User must be confirmed and have a ResetPasswordKey
        var orgUser = await organizationUserRepository.GetByIdAsync(id);
        if (orgUser == null || orgUser.Status != OrganizationUserStatusType.Confirmed ||
            orgUser.OrganizationId != orgId || string.IsNullOrEmpty(orgUser.ResetPasswordKey) ||
            !orgUser.UserId.HasValue)
        {
            throw new BadRequestException("Organization User not valid");
        }

        // Calling User must be of higher/equal user type to reset user's password
        var canAdjustPassword = false;
        switch (callingUserType)
        {
            case OrganizationUserType.Owner:
                canAdjustPassword = true;
                break;
            case OrganizationUserType.Admin:
                canAdjustPassword = orgUser.Type != OrganizationUserType.Owner;
                break;
            case OrganizationUserType.Custom:
                canAdjustPassword = orgUser.Type != OrganizationUserType.Owner &&
                    orgUser.Type != OrganizationUserType.Admin;
                break;
        }

        if (!canAdjustPassword)
        {
            throw new BadRequestException("Calling user does not have permission to reset this user's master password");
        }

        var user = await userService.GetUserByIdAsync(orgUser.UserId.Value);
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

        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        user.LastPasswordChangeDate = user.RevisionDate;
        user.ForcePasswordReset = true;
        user.Key = key;

        await userRepository.ReplaceAsync(user);
        await mailService.SendAdminResetPasswordEmailAsync(user.Email, user.Name, org.DisplayName());
        await eventService.LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_AdminResetPassword);
        await pushNotificationService.PushLogOutAsync(user.Id);

        return IdentityResult.Success;
    }
}
