using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IAcceptInviteLinkMembershipValidator"/>.
/// </summary>
public class AcceptInviteLinkMembershipValidator(
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
    IAcceptInviteLinkMembershipValidator
{
    public async Task<CommandResult<InviteLinkMembershipValidationResult>> ValidateAsync(
        AcceptOrganizationInviteLinkRequest request)
    {
        var inviteLinkResult = await ValidateInviteLinkAsync(
            request.OrganizationId,
            request.Code,
            request.User);

        if (inviteLinkResult.IsError)
        {
            return inviteLinkResult.AsError;
        }

        var organization = inviteLinkResult.AsSuccess.Organization;

        var existingOrganizationUser = await ResolveExistingOrganizationUserAsync(organization, request.User);

        var membershipStatusError = InviteLinkRules.ValidateAcceptMembershipStatus(organization, existingOrganizationUser);

        if (membershipStatusError is not null)
        {
            return membershipStatusError;
        }

        var policyEnforcer = await preAccessEnforcerQuery.RunAsync(organization.Id);

        var policyResult = await ValidateJoinPoliciesAsync(
            organization,
            existingOrganizationUser,
            request.User,
            policyEnforcer);

        if (policyResult.IsError)
        {
            return ToAcceptError(policyResult.AsError);
        }

        var accountRecoveryResult = JoinOrganizationPolicyValidationService.ValidateAccountRecoveryPolicy(
            organization.Id,
            request.User,
            GetProposedRole(existingOrganizationUser),
            policyEnforcer,
            request.ResetPasswordKey);

        if (accountRecoveryResult.IsError)
        {
            return ToAcceptError(accountRecoveryResult.Error!);
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
    /// Keeps the accept endpoint's existing invite link error messages.
    /// </summary>
    private static Error ToAcceptError(Error error) =>
        error switch
        {
            CannotBeAdminOfMultipleFreeOrganizations => new OnlyOneFreeOrganizationAdminAllowed(),
            ProviderUsersCannotJoin => new ProviderUsersCannotAcceptInviteLink(),
            _ => error
        };
}
