using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;

public class AdminRecoverAccountCommand(
    IAdminRecoverAccountValidator validator,
    IOrganizationRepository organizationRepository,
    IUserRepository userRepository,
    IMailService mailService,
    IEventService eventService,
    IPushNotificationService pushNotificationService,
    IUserService userService,
    IResetUserTwoFactorCommand resetUserTwoFactorCommand,
    IPolicyRequirementQuery policyRequirementQuery,
    IRevokeNonCompliantOrganizationUserCommand revokeNonCompliantOrganizationUserCommand,
    TimeProvider timeProvider) : IAdminRecoverAccountCommand
{
    public async Task<CommandResult> RecoverAccountAsync(RecoverAccountRequest request)
    {
        // Validate
        var validationResult = await validator.ValidateAsync(request);
        if (validationResult.IsError)
        {
            return validationResult.AsError;
        }

        var org = await organizationRepository.GetByIdAsync(request.OrgId);
        if (org == null)
        {
            return new OrganizationNotFoundError();
        }

        var user = await userRepository.GetByIdAsync(request.OrganizationUser.UserId!.Value);
        if (user == null)
        {
            return new UserNotFoundError();
        }

        // Password reset
        if (request.ResetMasterPassword)
        {
            var result = await userService.UpdatePasswordHash(user, request.NewMasterPasswordHash!);
            if (!result.Succeeded)
            {
                var errorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
                return new PasswordUpdateFailedError(errorMessage);
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            user.RevisionDate = user.AccountRevisionDate = now;
            user.LastPasswordChangeDate = now;
            user.ForcePasswordReset = true;
            user.Key = request.Key;

            await userRepository.ReplaceAsync(user);
        }

        // 2FA reset
        if (request.ResetTwoFactor)
        {
            await resetUserTwoFactorCommand.ResetAsync(user);
        }

        // Email notification
        await mailService.SendAdminResetPasswordEmailAsync(
            user.Email, user.Name, org.DisplayName(),
            request.ResetMasterPassword, request.ResetTwoFactor);

        // Event logging
        if (request.ResetMasterPassword)
        {
            await eventService.LogOrganizationUserEventAsync(
                request.OrganizationUser, EventType.OrganizationUser_AdminResetPassword);
        }

        if (request.ResetTwoFactor)
        {
            await eventService.LogOrganizationUserEventAsync(
                request.OrganizationUser, EventType.OrganizationUser_AdminResetTwoFactor);
        }

        // Push logout
        await pushNotificationService.PushLogOutAsync(user.Id);

        // Policy compliance — revoke user from orgs with RequireTwoFactor policy
        if (request.ResetTwoFactor)
        {
            await CheckPoliciesOnTwoFactorRemovalAsync(user);
        }

        return new None();
    }

    private async Task CheckPoliciesOnTwoFactorRemovalAsync(User user)
    {
        var requirement = await policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(user.Id);
        if (!requirement.OrganizationsRequiringTwoFactor.Any())
        {
            return;
        }

        var organizationIds = requirement.OrganizationsRequiringTwoFactor.Select(o => o.OrganizationId).ToList();
        var organizations = await organizationRepository.GetManyByIdsAsync(organizationIds);
        var organizationLookup = organizations.ToDictionary(org => org.Id);

        var revokeOrgUserTasks = requirement.OrganizationsRequiringTwoFactor
            .Where(o => organizationLookup.ContainsKey(o.OrganizationId))
            .Select(async o =>
            {
                var organization = organizationLookup[o.OrganizationId];
                await revokeNonCompliantOrganizationUserCommand.RevokeNonCompliantOrganizationUsersAsync(
                    new RevokeOrganizationUsersRequest(
                        o.OrganizationId,
                        [new OrganizationUserUserDetails { Id = o.OrganizationUserId, OrganizationId = o.OrganizationId }],
                        new SystemUser(EventSystemUser.TwoFactorDisabled),
                        RevocationReason.TwoFactorPolicyNonCompliance));
                await mailService.SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(organization.DisplayName(), user.Email);
            }).ToArray();

        await Task.WhenAll(revokeOrgUserTasks);
    }
}
