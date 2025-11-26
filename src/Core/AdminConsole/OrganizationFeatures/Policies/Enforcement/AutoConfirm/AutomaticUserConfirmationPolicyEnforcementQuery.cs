using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

public class AutomaticUserConfirmationPolicyEnforcementQuery(
    IPolicyRequirementQuery policyRequirementQuery,
    IOrganizationUserRepository organizationUserRepository)
    : IAutomaticUserConfirmationPolicyEnforcementQuery
{
    public async Task<ValidationResult<AutomaticUserConfirmationPolicyEnforcementRequest>> IsCompliantAsync(
        AutomaticUserConfirmationPolicyEnforcementRequest request)
    {
        var automaticUserConfirmationPolicyRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(request.User.Id);

        if (automaticUserConfirmationPolicyRequirement.IsEnabled(request.OrganizationUser.OrganizationId)
            && await OrganizationUserBelongsToAnotherOrganizationAsync(request))
        {
            return Invalid(request, new OrganizationEnforcesSingleOrgPolicy());
        }

        if (automaticUserConfirmationPolicyRequirement.IsEnabledAndUserIsAProvider(request.OrganizationUser.OrganizationId))
        {
            return Invalid(request, new ProviderUsersCannotJoin());
        }

        if (automaticUserConfirmationPolicyRequirement.IsEnabledForOrganizationsOtherThan(request.OrganizationUser
                .OrganizationId))
        {
            return Invalid(request, new OtherOrganizationEnforcesSingleOrgPolicy());
        }

        return Valid(request);
    }

    private async Task<bool> OrganizationUserBelongsToAnotherOrganizationAsync(
        AutomaticUserConfirmationPolicyEnforcementRequest request) =>
        request.OtherOrganizationsOrganizationUsers?.ToArray() is { Length: > 0 }
        || (await organizationUserRepository.GetManyByUserAsync(request.User.Id))
        .Any(x => x.OrganizationId != request.OrganizationUser.OrganizationId);
}
