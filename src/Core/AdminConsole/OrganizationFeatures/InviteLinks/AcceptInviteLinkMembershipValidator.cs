using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IAcceptInviteLinkMembershipValidator"/>.
/// </summary>
public class AcceptInviteLinkMembershipValidator(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IInviteLinkValidationService inviteLinkValidationService,
    IPreAccessEnforcerQuery preAccessEnforcerQuery,
    IJoinOrganizationPolicyValidationService joinOrganizationPolicyValidationService)
    : IAcceptInviteLinkMembershipValidator
{
    public async Task<Result<AcceptInviteLinkMembershipValidationResult, InviteLinkValidationError>> ValidateAsync(
        AcceptInviteLinkMembershipValidationRequest request)
    {
        var user = request.User;

        var link = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        var existingOrganization = link is null
            ? null
            : await organizationRepository.GetByIdAsync(link.OrganizationId);

        var eligibilityError = inviteLinkValidationService.ValidateEligibility(
            link,
            request.Code,
            existingOrganization,
            user);
        if (eligibilityError is not null)
        {
            return eligibilityError;
        }

        // ValidateEligibility guarantees the organization exists.
        var organization = existingOrganization!;

        var existingOrganizationUser = await ResolveExistingOrganizationUserAsync(organization, user);

        var membershipStatusError = InviteLinkRules.ValidateAcceptMembershipStatus(organization, existingOrganizationUser);
        if (membershipStatusError is not null)
        {
            return membershipStatusError;
        }

        var proposedRole = existingOrganizationUser?.Type ?? OrganizationUserType.User;
        var policyEnforcer = await preAccessEnforcerQuery.RunAsync(organization.Id);

        var joinRequirementsResult = await inviteLinkValidationService.ValidateJoinRequirementsAsync(
            organization,
            user,
            proposedRole,
            policyEnforcer);

        if (joinRequirementsResult.IsError)
        {
            return joinRequirementsResult.Error!;
        }

        var accountRecoveryResult = joinOrganizationPolicyValidationService.ValidateAccountRecoveryPolicy(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer,
            request.ResetPasswordKey);

        if (accountRecoveryResult.ResetPasswordKeyMissing)
        {
            return new ResetPasswordKeyRequiredError();
        }

        return new AcceptInviteLinkMembershipValidationResult
        {
            Organization = organization,
            ExistingOrganizationUser = existingOrganizationUser,
            AutoConfirmPolicyEnabled = joinRequirementsResult.AutoConfirmPolicyEnabled,
            AccountRecoveryAutoEnrollEnabled = accountRecoveryResult.AutoEnrollEnabled,
        };
    }

    /// <summary>
    /// Resolves the organization user to accept, preferring an existing user-linked membership
    /// and falling back to a pending email invitation for the same address.
    /// </summary>
    private async Task<OrganizationUser?> ResolveExistingOrganizationUserAsync(Organization organization, User user)
    {
        var userLinkedOrganizationUser = await organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);
        if (userLinkedOrganizationUser is not null)
        {
            return userLinkedOrganizationUser;
        }

        return await organizationUserRepository.GetByOrganizationEmailAsync(organization.Id, user.Email);
    }
}
