using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class AutomaticallyConfirmOrganizationUsersValidator(
    IOrganizationUserRepository organizationUserRepository,
    IFeatureService featureService,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IPolicyService policyService,
    IPolicyRequirementQuery policyRequirementQuery
    ) : IAutomaticallyConfirmOrganizationUsersValidator
{
    public async Task<ValidationResult<AutomaticallyConfirmOrganizationUserRequestData>> ValidateAsync(
        AutomaticallyConfirmOrganizationUserRequestData request)
    {
        var orgUser = request.OrganizationUser;

        if (orgUser.Status != OrganizationUserStatusType.Accepted)
        {
            return Invalid(request, new UserIsNotAccepted());
        }

        if (orgUser.OrganizationId != request.Organization.Id)
        {
            return Invalid(request, new OrganizationUserIdIsInvalid());
        }

        return await OrganizationUserOwnershipValidationAsync(request)
            .ThenAsync(OrganizationUserConformsToOrganizationPoliciesAsync);
    }

    private async Task<ValidationResult<AutomaticallyConfirmOrganizationUserRequestData>> OrganizationUserOwnershipValidationAsync(
        AutomaticallyConfirmOrganizationUserRequestData request) =>
        request.Organization.PlanType is not PlanType.Free
        || await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(request.UserId) == 0
            ? Valid(request)
            : Invalid(request, new UserToConfirmIsAnAdminOrOwnerOfAnotherFreeOrganization());

    private async Task<ValidationResult<AutomaticallyConfirmOrganizationUserRequestData>>
        OrganizationUserConformsToOrganizationPoliciesAsync(AutomaticallyConfirmOrganizationUserRequestData request)
    {
        return await OrganizationUserConformsToTwoFactorRequiredPolicyAsync(request)
            .ThenAsync(OrganizationUserConformsToSingleOrgPolicyAsync);
    }

    private async Task<ValidationResult<AutomaticallyConfirmOrganizationUserRequestData>> OrganizationUserConformsToTwoFactorRequiredPolicyAsync(
            AutomaticallyConfirmOrganizationUserRequestData request)
    {
        if ((await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync([request.UserId]))
            .Any(x => x.userId == request.UserId && x.twoFactorIsEnabled))
        {
            return Valid(request);
        }

        if (!featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements))
        {
            return (await policyService.GetPoliciesApplicableToUserAsync(request.UserId,
                    PolicyType.TwoFactorAuthentication))
                .Any(p => p.OrganizationId == request.Organization.Id)
                    ? Invalid(request, new UserDoesNotHaveTwoFactorEnabled())
                    : Valid(request);
        }

        return (await policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(request.UserId))
            .IsTwoFactorRequiredForOrganization(request.Organization.Id)
                ? Invalid(request, new UserDoesNotHaveTwoFactorEnabled())
                : Valid(request);
    }

    private async Task<ValidationResult<AutomaticallyConfirmOrganizationUserRequestData>> OrganizationUserConformsToSingleOrgPolicyAsync(
            AutomaticallyConfirmOrganizationUserRequestData request)
    {
        var allOrganizationUsersForUser = await organizationUserRepository.GetManyByUserAsync(request.UserId);

        if (allOrganizationUsersForUser.Count == 1)
        {
            return Valid(request);
        }

        if (!featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements))
        {
            var organizationsWithSingleOrgPoliciesForUser =
                await policyService.GetPoliciesApplicableToUserAsync(request.UserId, PolicyType.SingleOrg);

            if (organizationsWithSingleOrgPoliciesForUser.Any(ou => ou.OrganizationId == request.Organization.Id))
            {
                return Invalid(request, new OrganizationEnforcesSingleOrgPolicy());
            }

            if (organizationsWithSingleOrgPoliciesForUser.Any(ou => ou.OrganizationId != request.Organization.Id))
            {
                return Invalid(request, new OtherOrganizationEnforcesSingleOrgPolicy());
            }
        }

        var policyRequirement = await policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(request.UserId);

        if (policyRequirement.IsSingleOrgEnabledForThisOrganization(request.Organization.Id))
        {
            return Invalid(request, new OrganizationEnforcesSingleOrgPolicy());
        }

        if (policyRequirement.IsSingleOrgEnabledForOrganizationsOtherThan(request.Organization.Id))
        {
            return Invalid(request, new OtherOrganizationEnforcesSingleOrgPolicy());
        }

        return Valid(request);
    }
}
