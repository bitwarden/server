using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
    IMasterPasswordHasher masterPasswordHasher,
    TimeProvider timeProvider) : IAdminRecoverAccountCommand
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

        var (result, serverSideHash) = await masterPasswordHasher.ValidateAndHashPasswordAsync(user, newMasterPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        // TODO: Once this endpoint receives salt from the client, pass the client-supplied salt
        // instead of user.GetMasterPasswordSalt() to enable real salt verification.
        // user.UpdateMasterPasswordCrypto(serverSideHash!, key, requestModel.MasterPasswordUnlockData.Salt);
        user.UpdateMasterPasswordCrypto(serverSideHash!, key, user.GetMasterPasswordSalt());

        user.RevisionDate = user.AccountRevisionDate = timeProvider.GetUtcNow().UtcDateTime;
        user.LastPasswordChangeDate = user.RevisionDate;
        user.ForcePasswordReset = true;
        await userRepository.ReplaceAsync(user);
        await mailService.SendAdminResetPasswordEmailAsync(user.Email, user.Name, org.DisplayName(), true, false);
        await eventService.LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_AdminResetPassword);
        await pushNotificationService.PushLogOutAsync(user.Id);

        return IdentityResult.Success;
    }
}
