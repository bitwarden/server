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
/// Shared read-only precheck for the invite link confirmation flow. See
/// <see cref="IConfirmOrganizationInviteLinkValidator"/> for the checks performed.
/// </summary>
/// <remarks>
/// This performs the eligibility checks without any write side effects, so the confirmation endpoints
/// can verify a user before they are given the organization key. Write-time concerns (e.g. creating the
/// default collection, auto-scaling seats) are left to the consuming command.
/// </remarks>
public class ConfirmOrganizationInviteLinkValidator(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IInviteLinkValidationService inviteLinkValidationService,
    IPreAccessEnforcerQuery preAccessEnforcerQuery,
    IJoinOrganizationPolicyValidationService joinOrganizationPolicyValidationService)
    : IConfirmOrganizationInviteLinkValidator
{
    public async Task<Result<ConfirmOrganizationInviteLinkValidationResult, InviteLinkValidationError>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request)
    {
        var user = request.User;

        var existingLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        var existingOrganization = existingLink is null
            ? null
            : await organizationRepository.GetByIdAsync(existingLink.OrganizationId);

        var eligibilityError = inviteLinkValidationService.ValidateEligibility(
            existingLink,
            request.Code,
            existingOrganization,
            user);
        if (eligibilityError is not null)
        {
            return eligibilityError;
        }

        // ValidateEligibility guarantees the invite link and organization exist.
        var link = existingLink!;
        var organization = existingOrganization!;

        if (!link.SupportsConfirmation)
        {
            return new InviteLinkConfirmationNotSupportedError();
        }

        var existingOrganizationUser = await ResolveExistingOrganizationUserAsync(organization, user);

        var membershipStatusError = InviteLinkRules.ValidateConfirmMembershipStatus(organization, existingOrganizationUser);
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

        return new ConfirmOrganizationInviteLinkValidationResult
        {
            Organization = organization,
            ExistingOrganizationUser = existingOrganizationUser,
            AccountRecoveryAutoEnrollEnabled = accountRecoveryResult.AutoEnrollEnabled,
        };
    }

    /// <summary>
    /// Resolves the membership to confirm, preferring an existing user-linked membership and falling
    /// back to a pending email invitation for the same address.
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
