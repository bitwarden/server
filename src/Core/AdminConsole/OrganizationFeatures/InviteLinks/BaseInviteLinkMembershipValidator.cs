using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Read-only eligibility checks shared by the invite link validators. Errors are returned unmapped so each
/// validator controls its response shape.
/// </summary>
public abstract class BaseInviteLinkMembershipValidator(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationUserValidationService organizationUserValidationService,
    IJoinOrganizationPolicyValidationService joinOrganizationPolicyValidationService)
{
    protected IJoinOrganizationPolicyValidationService JoinOrganizationPolicyValidationService { get; } =
        joinOrganizationPolicyValidationService;

    /// <summary>
    /// Loads the invite link and its organization and checks they can be used by the user.
    /// See <see cref="InviteLinkRules.ValidateEligibility"/>.
    /// </summary>
    protected async Task<CommandResult<(OrganizationInviteLink InviteLink, Organization Organization)>> ValidateInviteLinkAsync(
        Guid organizationId,
        Guid code,
        User user)
    {
        var existingInviteLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(organizationId);
        var existingOrganization = existingInviteLink is null
            ? null
            : await organizationRepository.GetByIdAsync(existingInviteLink.OrganizationId);

        var eligibilityError = InviteLinkRules.ValidateEligibility(
            existingInviteLink,
            code,
            existingOrganization,
            user);

        if (eligibilityError is not null)
        {
            return eligibilityError;
        }

        // ValidateEligibility guarantees the invite link and organization exist.
        return (existingInviteLink!, existingOrganization!);
    }

    /// <summary>
    /// Resolves the user's existing membership, preferring a user-linked membership and falling back to a pending
    /// email invitation for the same address.
    /// </summary>
    protected async Task<OrganizationUser?> ResolveExistingOrganizationUserAsync(Organization organization, User user)
    {
        var userLinkedOrganizationUser = await organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);

        if (userLinkedOrganizationUser is not null)
        {
            return userLinkedOrganizationUser;
        }

        return await organizationUserRepository.GetByOrganizationEmailAsync(organization.Id, user.Email);
    }

    /// <summary>
    /// Checks the free organization admin limit and the Automatic User Confirmation, Single Organization, and Require
    /// Two-Factor Authentication policies.
    /// </summary>
    /// <param name="policyEnforcer">The policy enforcer for the organization being joined.</param>
    protected async Task<CommandResult<InviteLinkPolicyValidationResult>> ValidateJoinPoliciesAsync(
        Organization organization,
        OrganizationUser? existingOrganizationUser,
        User user,
        IPreAccessPolicyEnforcer policyEnforcer)
    {
        var proposedRole = GetProposedRole(existingOrganizationUser);

        // The free organization admin count only includes confirmed memberships, so the joining membership
        // has not been counted yet.
        var freeAdminError = await organizationUserValidationService.ValidateFreeOrgAdminLimitAsync(
            user.Id,
            organization.PlanType,
            currentUserType: OrganizationUserType.User,
            newUserType: proposedRole);

        if (freeAdminError is not null)
        {
            return freeAdminError;
        }

        var allOrganizationMemberships = await organizationUserRepository.GetManyByUserAsync(user.Id);
        var autoConfirmResult = await JoinOrganizationPolicyValidationService.ValidateAutomaticUserConfirmationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer,
            allOrganizationMemberships);

        if (autoConfirmResult.IsError)
        {
            return autoConfirmResult.Error!;
        }

        var singleOrgError = await JoinOrganizationPolicyValidationService.ValidateSingleOrganizationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer,
            allOrganizationMemberships);

        if (singleOrgError is not null)
        {
            return singleOrgError;
        }

        var twoFactorError = await JoinOrganizationPolicyValidationService.ValidateTwoFactorAuthenticationPolicyAsync(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer);

        if (twoFactorError is not null)
        {
            return twoFactorError;
        }

        return new InviteLinkPolicyValidationResult(autoConfirmResult.AutoConfirmPolicyEnabled);
    }

    /// <summary>
    /// The role the user joins with: their existing membership's role, or <see cref="OrganizationUserType.User"/> if
    /// they are new.
    /// </summary>
    protected static OrganizationUserType GetProposedRole(OrganizationUser? existingOrganizationUser) =>
        existingOrganizationUser?.Type ?? OrganizationUserType.User;
}
