using System.Diagnostics;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
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
    public async Task<CommandResult<ConfirmOrganizationInviteLinkValidationResult>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request)
    {
        var user = request.User;

        var existingLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId);
        var existingOrganization = existingLink is null
            ? null
            : await organizationRepository.GetByIdAsync(existingLink.OrganizationId);

        var eligibilityError = InviteLinkRules.ValidateEligibility(existingLink, request.Code, existingOrganization, user);
        if (eligibilityError is not null)
        {
            return ToConfirmValidationError(eligibilityError);
        }

        // ValidateEligibility guarantees the invite link and organization exist.
        var link = existingLink!;
        var organization = existingOrganization!;

        if (!link.SupportsConfirmation)
        {
            return new ConfirmInviteLinkConfirmationNotSupported();
        }

        var existingOrganizationUser = await ResolveExistingOrganizationUserAsync(organization, user);

        var membershipStatusError = InviteLinkRules.ValidateConfirmMembershipStatus(organization, existingOrganizationUser);
        if (membershipStatusError is not null)
        {
            return ToConfirmValidationError(membershipStatusError);
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
            return ToConfirmValidationError(joinRequirementsResult.Error!);
        }

        var accountRecoveryResult = joinOrganizationPolicyValidationService.ValidateAccountRecoveryPolicy(
            organization.Id,
            user,
            proposedRole,
            policyEnforcer,
            request.ResetPasswordKey);

        if (accountRecoveryResult.IsError)
        {
            return ToConfirmValidationError(accountRecoveryResult.Error!);
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

    /// <summary>
    /// Translates a shared invite link or policy error into the link-confirm validation-problem variant so the
    /// endpoint always returns the RFC 7807 shape. A new error must be mapped here rather than silently falling back
    /// to a plain 400.
    /// </summary>
    private static Error ToConfirmValidationError(Error error) =>
        error switch
        {
            InviteLinkNotFound => error,
            InviteLinkNotAvailable => new ConfirmInviteLinkNotAvailable(),
            EmailDomainNotAllowed e => new ConfirmEmailDomainNotAllowed(e.OrgName),
            EmailNotVerified => new ConfirmEmailNotVerified(),
            OrganizationAccessRevoked e => new ConfirmOrganizationAccessRevoked(e.OrgName),
            AlreadyOrganizationMember e => new ConfirmAlreadyOrganizationMember(e.OrgName),
            CannotBeAdminOfMultipleFreeOrganizations => new ConfirmOnlyOneFreeOrganizationAdminAllowed(),
            OtherOrganizationDoesNotAllowOtherMembership e => new ConfirmOtherOrganizationDoesNotAllowOtherMembership(e.Email),
            UserCannotBelongToAnotherOrganization e => new ConfirmUserCannotBelongToAnotherOrganization(e.Email),
            ProviderUsersCannotJoin => new ConfirmProviderUsersCannotAcceptInviteLink(),
            UserIsAMemberOfAnotherOrganization => new ConfirmUserIsAMemberOfAnotherOrganization(),
            UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy => new ConfirmUserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy(),
            TwoFactorRequiredForMembership => new ConfirmTwoFactorRequiredForMembership(),
            ResetPasswordKeyRequired => new ConfirmResetPasswordKeyRequired(),
            _ => throw new UnreachableException($"Unmapped invite link error: {error.GetType().Name}")
        };
}
