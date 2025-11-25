using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

/// <summary>
/// Used to enforce the Automatic User Confirmation policy. It uses the <see cref="IPolicyRequirementQuery"/> to retrieve
/// the <see cref="AutomaticUserConfirmationPolicyRequirement"/>. It is used to check to make sure the given user is
/// valid for the Automatic User Confirmation policy. It also validates that the given user is not a provider
/// or a member of another organization regardless of status or type.
/// </summary>
public interface IAutomaticUserConfirmationPolicyEnforcementQuery
{

    /// <summary>
    /// Checks if the given user is compliant with the Automatic User Confirmation policy.
    /// </summary>
    /// <param name="request"></param>
    /// <remarks>
    /// This uses the validation result pattern to avoid throwing exceptions.
    /// </remarks>
    /// <returns>A validation result with the error message if applicable.</returns>
    Task<ValidationResult<AutomaticUserConfirmationPolicyEnforcementRequest>> IsCompliantAsync(
        AutomaticUserConfirmationPolicyEnforcementRequest request);
}

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
            return Invalid(request, new AutoConfirmDoesNotAllowProviderUsers());
        }

        // This is a shortcut to potentially save a database call
        if (automaticUserConfirmationPolicyRequirement.IsEnabledForOrganizationsOtherThan(organizationUser
                .OrganizationId))
        {
            return Invalid(request, new AutoConfirmDoesNotAllowMembershipToOtherOrganizations());
        }

        if (otherOrganizationsOrganizationUsers is { Count: > 0 }
            || (await organizationUserRepository.GetManyByUserAsync(user.Id))
            .Any(x => x.OrganizationId != organizationUser.OrganizationId))
        {
            return Invalid(request, new AutoConfirmDoesNotAllowMembershipToOtherOrganizations());
        }

        return Valid(request);
    }
}
