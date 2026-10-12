using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

/// <inheritdoc />
public class JoinOrganizationPolicyValidationService(
    IPreAccessEnforcerQuery preAccessEnforcerQuery,
    IOrganizationUserRepository organizationUserRepository,
    IPolicyRequirementQuery policyRequirementQuery,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IProviderUserRepository providerUserRepository)
    : IJoinOrganizationPolicyValidationService
{
    public async Task<Error?> ValidateCanJoinAsync(
        Organization organization, User user, OrganizationUserType proposedRole)
    {
        var allOrganizationMemberships = await organizationUserRepository.GetManyByUserAsync(user.Id);
        var policyEnforcer = await preAccessEnforcerQuery.RunAsync(organization.Id);

        var singleOrgError = await ValidateSingleOrganizationPolicyAsync(
            organization, user, proposedRole, policyEnforcer, allOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return singleOrgError;
        }

        var twoFactorError = await ValidateTwoFactorAuthenticationPolicyAsync(user, proposedRole, policyEnforcer);
        if (twoFactorError is not null)
        {
            return twoFactorError;
        }

        return await ValidateAutomaticUserConfirmationPolicyAsync(
            organization, user, proposedRole, policyEnforcer, allOrganizationMemberships);
    }

    /// <summary>
    /// The Single Organization policy: another organization the user belongs to may forbid joining this one, and
    /// this organization may forbid belonging to any other.
    /// </summary>
    private async Task<Error?> ValidateSingleOrganizationPolicyAsync(Organization organization, User user,
        OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer,
        ICollection<OrganizationUser> allOrganizationMemberships)
    {
        // Other organizations. This also checks this organization when the requirement framework can resolve the
        // user's membership in it; that is consistent with the check below.
        var singleOrgRequirement = await policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(user.Id);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(organization.Id, allOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return singleOrgError;
        }

        // This organization
        if (IsMemberOfAnotherOrganization(allOrganizationMemberships, organization.Id)
            && policyEnforcer.Evaluate(PolicyType.SingleOrg, user.Id, proposedRole).IsEnforced)
        {
            return new UserIsAMemberOfAnotherOrganization();
        }

        return null;
    }

    /// <summary>
    /// The Require Two-Factor Authentication policy of this organization.
    /// </summary>
    private async Task<Error?> ValidateTwoFactorAuthenticationPolicyAsync(
        User user, OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer)
    {
        if (policyEnforcer.Evaluate(PolicyType.TwoFactorAuthentication, user.Id, proposedRole).IsEnforced
            && !await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user))
        {
            return new TwoFactorRequiredForMembership();
        }

        return null;
    }

    /// <summary>
    /// The Automatic User Confirmation policy: another organization the user belongs to may forbid joining this one,
    /// and when this organization enforces it, the user cannot be a provider user or belong to any other
    /// organization (no role exemptions).
    /// </summary>
    private async Task<Error?> ValidateAutomaticUserConfirmationPolicyAsync(Organization organization, User user,
        OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer,
        ICollection<OrganizationUser> allOrganizationMemberships)
    {
        // Other organizations
        var autoConfirmRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id);
        if (autoConfirmRequirement.IsEnabledForOrganizationsOtherThan(organization.Id))
        {
            return new OtherOrganizationDoesNotAllowOtherMembership(user.Email);
        }

        // This organization
        if (!policyEnforcer.Evaluate(PolicyType.AutomaticUserConfirmation, user.Id, proposedRole).IsEnforced)
        {
            return null;
        }

        if ((await providerUserRepository.GetManyByUserAsync(user.Id)).Count != 0)
        {
            return new ProviderUsersCannotJoin();
        }

        if (IsMemberOfAnotherOrganization(allOrganizationMemberships, organization.Id))
        {
            return new UserCannotBelongToAnotherOrganization(user.Email);
        }

        return null;
    }

    private static bool IsMemberOfAnotherOrganization(
        ICollection<OrganizationUser> allOrganizationMemberships, Guid organizationId) =>
        allOrganizationMemberships.Any(ou => ou.OrganizationId != organizationId);
}
