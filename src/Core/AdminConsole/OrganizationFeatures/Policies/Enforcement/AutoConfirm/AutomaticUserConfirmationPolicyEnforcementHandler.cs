using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

public class AutomaticUserConfirmationPolicyEnforcementHandler(
    IPolicyRequirementQuery policyRequirementQuery,
    IProviderUserRepository providerUserRepository)
    : IAutomaticUserConfirmationPolicyEnforcementHandler
{
    public async Task<ValidationResult<AutomaticUserConfirmationPolicyEnforcementRequest>> IsCompliantAsync(
        AutomaticUserConfirmationPolicyEnforcementRequest request)
    {
        var automaticUserConfirmationPolicyRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(request.User.Id);

        return await IsCompliantAsync(request, automaticUserConfirmationPolicyRequirement);
    }

    public async Task<ValidationResult<AutomaticUserConfirmationPolicyEnforcementRequest>> IsCompliantAsync(
        AutomaticUserConfirmationPolicyEnforcementRequest request,
        AutomaticUserConfirmationPolicyRequirement policyRequirement)
    {
        var currentOrganizationUser = request.AllOrganizationUsers
            .FirstOrDefault(x => x.OrganizationId == request.OrganizationId
                                 // invited users do not have a userId but will have email
                                 && (x.UserId == request.User.Id || x.Email == request.User.Email));

        if (currentOrganizationUser is null)
        {
            return Invalid(request, new CurrentOrganizationUserIsNotPresentInRequest());
        }

        var isProviderUser = (await providerUserRepository.GetManyByUserAsync(request.User.Id)).Count != 0;
        var violation = GetAutoConfirmPolicyViolation(policyRequirement, request.OrganizationId,
            isProviderUser, request.AllOrganizationUsers.Count);

        var violationWithEmail = violation switch
        {
            UserCannotBelongToAnotherOrganization => new UserCannotBelongToAnotherOrganization(request.User.Email),
            OtherOrganizationDoesNotAllowOtherMembership => new OtherOrganizationDoesNotAllowOtherMembership(request.User.Email),
            _ => violation
        };

        return violationWithEmail is not null ? Invalid(request, violationWithEmail) : Valid(request);
    }

    public Error? GetAutoConfirmPolicyViolation(
        AutomaticUserConfirmationPolicyRequirement policyRequirement,
        Guid organizationId,
        bool isProviderUser,
        int orgMembershipCount)
    {
        if (policyRequirement.IsEnabled(organizationId))
        {
            if (isProviderUser)
            {
                return new ProviderUsersCannotJoin();
            }

            if (orgMembershipCount > 1)
            {
                return new UserCannotBelongToAnotherOrganization(string.Empty);
            }
        }

        if (policyRequirement.IsEnabledForOrganizationsOtherThan(organizationId))
        {
            return new OtherOrganizationDoesNotAllowOtherMembership(string.Empty);
        }

        return null;
    }
}
