using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

public class AutomaticUserConfirmationPolicyEnforcementValidator(
    IPolicyRequirementQuery policyRequirementQuery,
    IProviderUserRepository providerUserRepository)
    : IAutomaticUserConfirmationPolicyEnforcementValidator
{
    public async Task<ValidationResult<AutomaticUserConfirmationPolicyEnforcementRequest>> IsCompliantAsync(
        AutomaticUserConfirmationPolicyEnforcementRequest request)
    {
        var automaticUserConfirmationPolicyRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(request.User.Id);

        var currentOrganizationUser = request.AllOrganizationUsers
            .FirstOrDefault(x => x.OrganizationId == request.OrganizationId
                                 && x.UserId == request.User.Id);

        if (currentOrganizationUser is null)
        {
            return Invalid(request, new CurrentOrganizationUserIsNotPresentInRequest());
        }

        if ((await providerUserRepository.GetManyByUserAsync(request.User.Id)).Count != 0)
        {
            return Invalid(request, new ProviderUsersCannotJoin());
        }

        if (request.AllOrganizationUsers.Count == 1)
        {
            return Valid(request);
        }

        if (automaticUserConfirmationPolicyRequirement
            .IsEnabledForOrganizationsOtherThan(currentOrganizationUser.OrganizationId))
        {
            return Invalid(request, new OtherOrganizationDoesNotAllowOtherMembership());
        }

        return Valid(request);
    }
}
