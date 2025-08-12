using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
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
    IUserService userService,
    IProviderUserRepository providerUserRepository,
    ICurrentContext currentContext) : IAdminRecoverAccountCommand
{
    public async Task<IdentityResult> RecoverAccountAsync(OrganizationUserType callingUserType, Guid orgId,
        Guid organizationUserId, string newMasterPassword, string key)
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
        var orgUser = await organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.Status != OrganizationUserStatusType.Confirmed ||
            orgUser.OrganizationId != orgId || string.IsNullOrEmpty(orgUser.ResetPasswordKey) ||
            !orgUser.UserId.HasValue)
        {
            throw new BadRequestException("Organization User not valid");
        }

        // Calling User must be of higher/equal user type to reset user's password
        // TODO: move this out of the command and into an authorization handler
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

        // Check if the target user is a providerUser for this organization - if so, the Calling User must also be
        // part of the provider
        // TODO: move this out of the command and into an authorization handler
        await CheckProviderPermissionsAsync(orgId, orgUser);

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

    private async Task CheckProviderPermissionsAsync(Guid orgId, OrganizationUser targetOrganizationUser)
    {
        // Get all ProviderUsers for this organization. If the organization doesn't have a provider, then
        // there will be no ProviderUsers so this logic will work either way.
        var providerUsers = await providerUserRepository.GetManyByOrganizationAsync(orgId);

        // Check if the target user is a providerUser (in any status, just to be safe)
        var targetUserIsProvider = providerUsers.Any(pu => pu.UserId == targetOrganizationUser.UserId!.Value);

        // If the target user is a provider, the calling user must also be a provider for this organization
        if (targetUserIsProvider)
        {
            var callingUserIsProvider = currentContext.UserId.HasValue && providerUsers.Any(pu =>
                pu.UserId == currentContext.UserId.Value && pu.Status == ProviderUserStatusType.Confirmed);

            if (!callingUserIsProvider)
            {
                throw new BadRequestException("Calling user does not have permission to reset this user's master password");
            }
        }
    }
}
