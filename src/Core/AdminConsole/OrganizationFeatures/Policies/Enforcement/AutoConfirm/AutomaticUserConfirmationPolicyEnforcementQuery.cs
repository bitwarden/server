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
        var (organizationUser, otherOrganizationsOrganizationUsers, user) = request;

        var automaticUserConfirmationPolicyRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(request.User.Id);

        if (automaticUserConfirmationPolicyRequirement.IsEnabled(organizationUser.OrganizationId))
        {
            return Invalid(request, new AutoConfirmDoesNotAllowMembershipToOtherOrganizations());
        }

        if (automaticUserConfirmationPolicyRequirement.IsEnabledAndUserIsAProvider(organizationUser.OrganizationId))
        {
            return Invalid(request, new ProviderUsersCannotJoin());
        }

        if (automaticUserConfirmationPolicyRequirement.IsEnabledForOrganizationsOtherThan(organizationUser
                .OrganizationId))
        {
            return Invalid(request, new OrganizationEnforcesSingleOrgPolicy());
        }

        if (otherOrganizationsOrganizationUsers is { Count: > 0 }
            || (await organizationUserRepository.GetManyByUserAsync(user.Id))
            .Any(x => x.OrganizationId != organizationUser.OrganizationId))
        {
            return Invalid(request, new OtherOrganizationEnforcesSingleOrgPolicy());
        }

        return Valid(request);
    }
}
