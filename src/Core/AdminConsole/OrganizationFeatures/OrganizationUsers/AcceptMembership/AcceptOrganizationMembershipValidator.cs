using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;

public class AcceptOrganizationMembershipValidator(
    IPolicyRequirementQuery policyRequirementQuery,
    IAutomaticUserConfirmationPolicyEnforcementHandler automaticUserConfirmationPolicyEnforcementHandler,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery)
    : IAcceptOrganizationMembershipValidator
{
    public async Task<ValidationResult<AcceptOrganizationMembershipValidationResult>> ValidateAsync(
        AcceptOrganizationMembershipValidationRequest request)
    {
        var autoConfirmRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(request.User.Id);

        // The enforcement handler requires the target org membership to be present in the list;
        // stub it for new members who have no org user record yet.
        var memberships = EnsureTargetMembershipPresent(request);

        var enforcementRequest = new AutomaticUserConfirmationPolicyEnforcementRequest(
            request.OrganizationId, memberships, request.User);

        var autoConfirmResult = await automaticUserConfirmationPolicyEnforcementHandler
            .IsCompliantAsync(enforcementRequest, autoConfirmRequirement);

        if (autoConfirmResult.IsError)
        {
            return Invalid(new AcceptOrganizationMembershipValidationResult(), autoConfirmResult.AsError);
        }

        var singleOrgRequirement = await policyRequirementQuery
            .GetAsync<SingleOrganizationPolicyRequirement>(request.User.Id);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(
            request.OrganizationId, request.AllOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return Invalid(new AcceptOrganizationMembershipValidationResult(), singleOrgError);
        }

        if (!await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(request.User))
        {
            var twoFactorRequirement = await policyRequirementQuery
                .GetAsync<RequireTwoFactorPolicyRequirement>(request.User.Id);
            if (twoFactorRequirement.IsTwoFactorRequiredForOrganization(request.OrganizationId))
            {
                return Invalid(new AcceptOrganizationMembershipValidationResult(), new TwoFactorRequiredForMembership());
            }
        }

        return Valid(new AcceptOrganizationMembershipValidationResult
        {
            AutoConfirmPolicyEnabled = autoConfirmRequirement.IsEnabled(request.OrganizationId)
        });
    }

    private static IEnumerable<OrganizationUser> EnsureTargetMembershipPresent(
        AcceptOrganizationMembershipValidationRequest request)
    {
        var memberships = request.AllOrganizationMemberships.ToList();
        if (!memberships.Any(x => x.OrganizationId == request.OrganizationId
                && (x.UserId == request.User.Id
                    || string.Equals(x.Email, request.User.Email, StringComparison.OrdinalIgnoreCase))))
        {
            memberships.Add(request.ExistingMembership
                ?? new OrganizationUser { OrganizationId = request.OrganizationId, UserId = request.User.Id });
        }

        return memberships;
    }
}
