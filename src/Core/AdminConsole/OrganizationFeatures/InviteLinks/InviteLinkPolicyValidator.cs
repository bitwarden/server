using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IInviteLinkPolicyValidator"/>.
/// </summary>
public class InviteLinkPolicyValidator(
    IPreAccessEnforcerQuery preAccessEnforcerQuery,
    IOrganizationUserRepository organizationUserRepository,
    IPolicyRequirementQuery policyRequirementQuery,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IProviderUserRepository providerUserRepository)
    : IInviteLinkPolicyValidator
{
    public async Task<ValidationResult<InviteLinkPolicyValidationRequest>> ValidateAsync(
        InviteLinkPolicyValidationRequest request)
    {
        var allOrganizationMemberships = await organizationUserRepository.GetManyByUserAsync(request.User.Id);
        var policyEnforcer = await preAccessEnforcerQuery.RunAsync(request.Organization.Id);

        var singleOrgError = await ValidateSingleOrganizationPolicyAsync(request, policyEnforcer, allOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return Invalid(request, singleOrgError);
        }

        var twoFactorError = await ValidateTwoFactorAuthenticationPolicyAsync(request, policyEnforcer);
        if (twoFactorError is not null)
        {
            return Invalid(request, twoFactorError);
        }

        var autoConfirmError = await ValidateAutomaticUserConfirmationPolicyAsync(
            request, policyEnforcer, allOrganizationMemberships);
        if (autoConfirmError is not null)
        {
            return Invalid(request, autoConfirmError);
        }

        return Valid(request);
    }

    /// <summary>
    /// The Single Organization policy: another organization the user belongs to may forbid joining this one, and
    /// this organization may forbid belonging to any other.
    /// </summary>
    private async Task<Error?> ValidateSingleOrganizationPolicyAsync(InviteLinkPolicyValidationRequest request,
        IPreAccessPolicyEnforcer policyEnforcer, ICollection<OrganizationUser> allOrganizationMemberships)
    {
        var user = request.User;
        var organizationId = request.Organization.Id;

        // Other organizations. This also checks this organization when the requirement framework can resolve the
        // user's membership in it; that is consistent with the check below.
        var singleOrgRequirement = await policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(user.Id);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(organizationId, allOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return singleOrgError;
        }

        // This organization
        if (IsMemberOfAnotherOrganization(allOrganizationMemberships, organizationId)
            && policyEnforcer.Evaluate(PolicyType.SingleOrg, user.Id, GetProposedRole(request)).IsEnforced)
        {
            return new UserIsAMemberOfAnotherOrganization();
        }

        return null;
    }

    /// <summary>
    /// The Require Two-Factor Authentication policy of this organization.
    /// </summary>
    private async Task<Error?> ValidateTwoFactorAuthenticationPolicyAsync(
        InviteLinkPolicyValidationRequest request, IPreAccessPolicyEnforcer policyEnforcer)
    {
        var user = request.User;

        if (policyEnforcer.Evaluate(PolicyType.TwoFactorAuthentication, user.Id, GetProposedRole(request)).IsEnforced
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
    private async Task<Error?> ValidateAutomaticUserConfirmationPolicyAsync(InviteLinkPolicyValidationRequest request,
        IPreAccessPolicyEnforcer policyEnforcer, ICollection<OrganizationUser> allOrganizationMemberships)
    {
        var user = request.User;

        // Other organizations
        var autoConfirmRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id);
        if (autoConfirmRequirement.IsEnabledForOrganizationsOtherThan(request.Organization.Id))
        {
            return new OtherOrganizationDoesNotAllowOtherMembership(user.Email);
        }

        // This organization
        if (!policyEnforcer.Evaluate(PolicyType.AutomaticUserConfirmation, user.Id, GetProposedRole(request)).IsEnforced)
        {
            return null;
        }

        if ((await providerUserRepository.GetManyByUserAsync(user.Id)).Count != 0)
        {
            return new ProviderUsersCannotAcceptInviteLink();
        }

        if (IsMemberOfAnotherOrganization(allOrganizationMemberships, request.Organization.Id))
        {
            return new UserCannotBelongToAnotherOrganization(user.Email);
        }

        return null;
    }

    /// <summary>
    /// The role the user will hold in the organization: their existing membership's role, or
    /// <see cref="OrganizationUserType.User"/> for a brand-new member.
    /// </summary>
    private static OrganizationUserType GetProposedRole(InviteLinkPolicyValidationRequest request) =>
        request.ExistingOrganizationUser?.Type ?? OrganizationUserType.User;

    private static bool IsMemberOfAnotherOrganization(
        ICollection<OrganizationUser> allOrganizationMemberships, Guid organizationId) =>
        allOrganizationMemberships.Any(ou => ou.OrganizationId != organizationId);
}
