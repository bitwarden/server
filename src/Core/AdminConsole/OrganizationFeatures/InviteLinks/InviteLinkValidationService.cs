using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <inheritdoc />
public class InviteLinkValidationService(
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationUserValidationService organizationUserValidationService,
    IJoinOrganizationPolicyValidationService joinOrganizationPolicyValidationService)
    : IInviteLinkValidationService
{
    public async Task<JoinRequirementsValidationResult> ValidateJoinRequirementsAsync(
        Organization organization,
        User user,
        OrganizationUserType proposedRole,
        IPreAccessPolicyEnforcer policyEnforcer)
    {
        // The free organization admin count only includes confirmed memberships, so the joining membership
        // has not been counted yet.
        var freeAdminError = await organizationUserValidationService.ValidateFreeOrgAdminLimitAsync(
            user.Id,
            organization.PlanType,
            currentUserType: OrganizationUserType.User,
            newUserType: proposedRole);

        if (freeAdminError is not null)
        {
            return new JoinRequirementsValidationResult(freeAdminError, AutoConfirmPolicyEnabled: false);
        }

        var allOrganizationMemberships = await organizationUserRepository.GetManyByUserAsync(user.Id);

        var autoConfirmResult = await joinOrganizationPolicyValidationService.ValidateAutomaticUserConfirmationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer,
            allOrganizationMemberships);

        if (autoConfirmResult.IsError)
        {
            return new JoinRequirementsValidationResult(autoConfirmResult.Error, autoConfirmResult.AutoConfirmPolicyEnabled);
        }

        var singleOrgError = await joinOrganizationPolicyValidationService.ValidateSingleOrganizationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer,
            allOrganizationMemberships);

        if (singleOrgError is not null)
        {
            return new JoinRequirementsValidationResult(singleOrgError, autoConfirmResult.AutoConfirmPolicyEnabled);
        }

        var twoFactorError = await joinOrganizationPolicyValidationService.ValidateTwoFactorAuthenticationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer);

        return new JoinRequirementsValidationResult(twoFactorError, autoConfirmResult.AutoConfirmPolicyEnabled);
    }
}
