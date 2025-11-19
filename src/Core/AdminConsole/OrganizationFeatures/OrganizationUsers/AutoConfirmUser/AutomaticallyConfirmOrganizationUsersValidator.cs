using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class AutomaticallyConfirmOrganizationUsersValidator(
    IOrganizationUserRepository organizationUserRepository,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IPolicyRequirementQuery policyRequirementQuery,
    IPolicyRepository policyRepository) : IAutomaticallyConfirmOrganizationUsersValidator
{
    public async Task<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>> ValidateAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        // User must exist
        if (request is { OrganizationUser: null } || request.OrganizationUser is { UserId: null })
        {
            return Invalid(request, new UserNotFoundError());
        }

        // Organization must exist
        if (request is { Organization: null })
        {
            return Invalid(request, new OrganizationNotFound());
        }

        // User must belong to the organization
        if (request.OrganizationUser.OrganizationId != request.Organization.Id)
        {
            return Invalid(request, new OrganizationUserIdIsInvalid());
        }

        // User must be accepted
        if (request is { OrganizationUser.Status: not OrganizationUserStatusType.Accepted })
        {
            return Invalid(request, new UserIsNotAccepted());
        }

        // User must be of type User
        if (request is { OrganizationUser.Type: not OrganizationUserType.User })
        {
            return Invalid(request, new UserIsNotUserType());
        }

        if (!await OrganizationHasAutomaticallyConfirmUsersPolicyEnabledAsync(request))
        {
            return Invalid(request, new AutomaticallyConfirmUsersPolicyIsNotEnabled());
        }

        if (!await OrganizationUserConformsToTwoFactorRequiredPolicyAsync(request))
        {
            return Invalid(request, new UserDoesNotHaveTwoFactorEnabled());
        }

        if (await OrganizationUserConformsToSingleOrgPolicyAsync(request) is { } error)
        {
            return Invalid(request, error);
        }

        return Valid(request);
    }

    private async Task<bool> OrganizationHasAutomaticallyConfirmUsersPolicyEnabledAsync(
            AutomaticallyConfirmOrganizationUserValidationRequest request) =>
        await policyRepository.GetByOrganizationIdTypeAsync(request.OrganizationId,
            PolicyType.AutomaticUserConfirmation) is { Enabled: true }
        && request.Organization is { UseAutomaticUserConfirmation: true };

    private async Task<bool> OrganizationUserConformsToTwoFactorRequiredPolicyAsync(AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        if ((await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync([request.OrganizationUser!.UserId!.Value]))
            .Any(x => x.userId == request.OrganizationUser.UserId && x.twoFactorIsEnabled))
        {
            return true;
        }

        return !(await policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(request.OrganizationUser.UserId!.Value))
            .IsTwoFactorRequiredForOrganization(request.Organization!.Id);
    }

    private async Task<Error?> OrganizationUserConformsToSingleOrgPolicyAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        var allOrganizationUsersForUser = await organizationUserRepository
            .GetManyByUserAsync(request.OrganizationUser!.UserId!.Value);

        if (allOrganizationUsersForUser.Count == 1)
        {
            return null;
        }

        var policyRequirement = await policyRequirementQuery
            .GetAsync<SingleOrganizationPolicyRequirement>(request.OrganizationUser!.UserId!.Value);

        if (policyRequirement.IsSingleOrgEnabledForThisOrganization(request.Organization!.Id))
        {
            return new OrganizationEnforcesSingleOrgPolicy();
        }

        if (policyRequirement.IsSingleOrgEnabledForOrganizationsOtherThan(request.Organization.Id))
        {
            return new OtherOrganizationEnforcesSingleOrgPolicy();
        }

        return null;
    }
}
