using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IGetOrganizationInviteValidator"/>.
/// </summary>
public class GetOrganizationInviteValidator(
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
    IGetOrganizationInviteValidator
{
    public async Task<CommandResult<OrganizationInviteLink>> ValidateAsync(GetOrganizationInviteRequest request)
    {
        var inviteLinkResult = await ValidateInviteLinkAsync(
            request.OrganizationId,
            request.Code,
            request.User);

        if (inviteLinkResult.IsError)
        {
            return inviteLinkResult.AsError;
        }

        var (inviteLink, organization) = inviteLinkResult.AsSuccess;

        var existingOrganizationUser = await ResolveExistingOrganizationUserAsync(organization, request.User);

        // The existing membership must allow the step the link leads to: confirming, or accepting.
        var membershipStatusError = inviteLink.SupportsConfirmation
            ? InviteLinkRules.ValidateConfirmMembershipStatus(organization, existingOrganizationUser)
            : InviteLinkRules.ValidateAcceptMembershipStatus(organization, existingOrganizationUser);

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
            return policyResult.AsError;
        }

        return inviteLink;
    }
}
