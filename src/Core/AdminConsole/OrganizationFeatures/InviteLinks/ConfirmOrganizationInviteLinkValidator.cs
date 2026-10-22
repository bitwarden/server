using System.Diagnostics;
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
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IConfirmOrganizationInviteLinkValidator"/>.
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
    IOrganizationUserValidationService organizationUserValidationService,
    IPreAccessEnforcerQuery preAccessEnforcerQuery,
    IJoinOrganizationPolicyValidationService joinOrganizationPolicyValidationService)
    : BaseInviteLinkMembershipValidator(
        organizationInviteLinkRepository,
        organizationRepository,
        organizationUserRepository,
        organizationUserValidationService,
        joinOrganizationPolicyValidationService),
    IConfirmOrganizationInviteLinkValidator
{
    public async Task<CommandResult<InviteLinkMembershipValidationResult>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request)
    {
        var inviteLinkResult = await ValidateInviteLinkAsync(
            request.OrganizationId,
            request.Code,
            request.User);

        if (inviteLinkResult.IsError)
        {
            return ToValidationError(inviteLinkResult.AsError);
        }

        var (inviteLink, organization) = inviteLinkResult.AsSuccess;

        if (!inviteLink.SupportsConfirmation)
        {
            return new ConfirmInviteLinkConfirmationNotSupported();
        }

        var existingOrganizationUser = await ResolveExistingOrganizationUserAsync(organization, request.User);

        var membershipStatusError = InviteLinkRules.ValidateConfirmMembershipStatus(organization, existingOrganizationUser);

        if (membershipStatusError is not null)
        {
            return ToValidationError(membershipStatusError);
        }

        var policyEnforcer = await preAccessEnforcerQuery.RunAsync(organization.Id);

        var policyResult = await ValidateJoinPoliciesAsync(
            organization,
            existingOrganizationUser,
            request.User,
            policyEnforcer);

        if (policyResult.IsError)
        {
            return ToValidationError(policyResult.AsError);
        }

        var accountRecoveryResult = JoinOrganizationPolicyValidationService.ValidateAccountRecoveryPolicy(
            organization.Id,
            request.User,
            GetProposedRole(existingOrganizationUser),
            policyEnforcer,
            request.ResetPasswordKey);

        if (accountRecoveryResult.IsError)
        {
            return ToValidationError(accountRecoveryResult.Error!);
        }

        return new InviteLinkMembershipValidationResult
        {
            Organization = organization,
            ExistingOrganizationUser = existingOrganizationUser,
            AccountRecoveryAutoEnrollEnabled = accountRecoveryResult.AutoEnrollEnabled,
            AutoConfirmPolicyEnabled = policyResult.AsSuccess.AutoConfirmPolicyEnabled,
        };
    }

    /// <summary>
    /// Translates a shared invite link or policy error into the link-confirm validation-problem variant so the
    /// endpoint always returns the RFC 7807 shape. A new error must be mapped here rather than silently falling back
    /// to a plain 400.
    /// </summary>
    private static Error ToValidationError(Error error) =>
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
            _ => throw new UnreachableException($"Unmapped error: {error.GetType().Name}")
        };
}
