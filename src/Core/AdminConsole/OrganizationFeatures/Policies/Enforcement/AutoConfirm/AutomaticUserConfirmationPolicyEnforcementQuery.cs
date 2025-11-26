using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

public class AutomaticUserConfirmationPolicyEnforcementQuery(
    IPolicyRequirementQuery policyRequirementQuery)
    : IAutomaticUserConfirmationPolicyEnforcementQuery
{
    public async Task<ValidationResult<AutomaticUserConfirmationPolicyEnforcementRequest>> IsCompliantAsync(
        AutomaticUserConfirmationPolicyEnforcementRequest request)
    {
        var automaticUserConfirmationPolicyRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(request.User.Id);

        if (automaticUserConfirmationPolicyRequirement.IsEnabled(request.OrganizationUser.OrganizationId)
            && OrganizationUserBelongsToAnotherOrganization(request))
        {
            return Invalid(request, new OrganizationEnforcesSingleOrgPolicy());
        }

        if (automaticUserConfirmationPolicyRequirement.IsEnabledAndUserIsAProvider(request.OrganizationUser.OrganizationId))
        {
            return Invalid(request, new ProviderUsersCannotJoin());
        }

        if (automaticUserConfirmationPolicyRequirement
            .IsEnabledForOrganizationsOtherThan(request.OrganizationUser.OrganizationId))
        {
            return Invalid(request, new OtherOrganizationEnforcesSingleOrgPolicy());
        }

        return Valid(request);
    }

    private static bool OrganizationUserBelongsToAnotherOrganization(AutomaticUserConfirmationPolicyEnforcementRequest request) =>
        request.OtherOrganizationsOrganizationUsers.Any(ou =>
            ou.OrganizationId != request.OrganizationUser.OrganizationId);
}
