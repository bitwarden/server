using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class AutomaticallyConfirmOrganizationUsersValidator(
    IOrganizationUserRepository organizationUserRepository,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IPolicyRequirementQuery policyRequirementQuery
    ) : IAutomaticallyConfirmOrganizationUsersValidator
{
    public async Task<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>> ValidateAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request) =>
        await OrganizationUserIsAccepted(request)
            .Then(OrganizationUserBelongsToOrganization)
            .Then(OrganizationUserIsNotOfTypeUser)
            .ThenAsync(OrganizationUserOwnershipValidationAsync)
            .ThenAsync(OrganizationUserConformsToTwoFactorRequiredPolicyAsync)
            .ThenAsync(OrganizationUserConformsToSingleOrgPolicyAsync);

    private static ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest> OrganizationUserIsNotOfTypeUser(
            AutomaticallyConfirmOrganizationUserValidationRequest request) =>
        request.OrganizationUser is { Type: OrganizationUserType.User }
            ? Valid(request)
            : Invalid(request, new UserIsNotUserType());

    private static ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest> OrganizationUserIsAccepted(
        AutomaticallyConfirmOrganizationUserValidationRequest request) =>
        request.OrganizationUser is { Status: OrganizationUserStatusType.Accepted }
            ? Valid(request)
            : Invalid(request, new UserIsNotAccepted());


    private static ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest> OrganizationUserBelongsToOrganization(
            AutomaticallyConfirmOrganizationUserValidationRequest request) =>
        request.OrganizationUser.OrganizationId == request.Organization.Id
            ? Valid(request)
            : Invalid(request, new OrganizationUserIdIsInvalid());

    private async Task<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>> OrganizationUserOwnershipValidationAsync(
        AutomaticallyConfirmOrganizationUserValidationRequest request) =>
        request.Organization.PlanType is not PlanType.Free
        || await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(request.OrganizationUser.UserId) == 0
            ? Valid(request)
            : Invalid(request, new UserToConfirmIsAnAdminOrOwnerOfAnotherFreeOrganization());

    private async Task<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>> OrganizationUserConformsToTwoFactorRequiredPolicyAsync(
            AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        if ((await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync([request.OrganizationUser.UserId]))
            .Any(x => x.userId == request.OrganizationUser.UserId && x.twoFactorIsEnabled))
        {
            return Valid(request);
        }

        return (await policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(request.OrganizationUser.UserId))
            .IsTwoFactorRequiredForOrganization(request.Organization.Id)
                ? Invalid(request, new UserDoesNotHaveTwoFactorEnabled())
                : Valid(request);
    }

    private async Task<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>> OrganizationUserConformsToSingleOrgPolicyAsync(
            AutomaticallyConfirmOrganizationUserValidationRequest request)
    {
        var allOrganizationUsersForUser = await organizationUserRepository
            .GetManyByUserAsync(request.OrganizationUser.UserId);

        if (allOrganizationUsersForUser.Count == 1)
        {
            return Valid(request);
        }

        var policyRequirement = await policyRequirementQuery
            .GetAsync<SingleOrganizationPolicyRequirement>(request.OrganizationUser.UserId);

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
