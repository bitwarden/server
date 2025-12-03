using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

/// <summary>
/// Represents an event handler for the Automatic User Confirmation policy.
///
/// This class validates that the following conditions are met:
/// <ul>
///     <li>The Single organization policy is enabled</li>
///     <li>All organization users are compliant with the Single organization policy</li>
///     <li>No provider users exist</li>
/// </ul>
/// </summary>
public class AutomaticUserConfirmationPolicyEventHandler(
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository)
    : IPolicyValidator, IPolicyValidationEvent, IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.AutomaticUserConfirmation;

    private const string _usersNotCompliantWithSingleOrgErrorMessage =
        "All organization users must be compliant with the Single organization policy before enabling the Automatically confirm invited users policy. Please remove users who are members of multiple organizations.";

    private const string _providerUsersExistErrorMessage =
        "The organization has users with the Provider user type. Please remove provider users before enabling the Automatically confirm invited users policy.";

    private const string _failedToFindOrganizationUsers = "Failed to find any organization users.";

    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];

    public async Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        var isNotEnablingPolicy = policyUpdate is not { Enabled: true };
        var policyAlreadyEnabled = currentPolicy is { Enabled: true };
        if (isNotEnablingPolicy || policyAlreadyEnabled)
        {
            return string.Empty;
        }

        return await ValidateEnablingPolicyAsync(policyUpdate.OrganizationId);
    }

    public async Task<string> ValidateAsync(SavePolicyModel savePolicyModel, Policy? currentPolicy) =>
        await ValidateAsync(savePolicyModel.PolicyUpdate, currentPolicy);

    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) =>
        Task.CompletedTask;

    private async Task<string> ValidateEnablingPolicyAsync(Guid organizationId)
    {
        var organizationUsers = await organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);

        var singleOrgValidationError = await ValidateUserComplianceWithSingleOrgAsync(organizationId, organizationUsers);
        if (!string.IsNullOrWhiteSpace(singleOrgValidationError))
        {
            return singleOrgValidationError;
        }

        var providerValidationError = await ValidateNoProviderUsersAsync(organizationUsers);
        if (!string.IsNullOrWhiteSpace(providerValidationError))
        {
            return providerValidationError;
        }

        return string.Empty;
    }

    private async Task<string> ValidateUserComplianceWithSingleOrgAsync(Guid organizationId,
        ICollection<OrganizationUserUserDetails> organizationUsers)
    {
        var hasNonCompliantUser = (await organizationUserRepository.GetManyByManyUsersAsync(
                organizationUsers.Select(ou => ou.UserId!.Value)))
            .Any(uo => uo.OrganizationId != organizationId
                       && uo.Status != OrganizationUserStatusType.Invited);

        return hasNonCompliantUser ? _usersNotCompliantWithSingleOrgErrorMessage : string.Empty;
    }

    private async Task<string> ValidateNoProviderUsersAsync(ICollection<OrganizationUserUserDetails> organizationUsers)
    {
        var userIds = organizationUsers.Where(x => x.UserId is not null)
            .Select(x => x.UserId!.Value);

        return (await providerUserRepository.GetManyByManyUsersAsync(userIds)).Count != 0
            ? _providerUsersExistErrorMessage
            : string.Empty;
    }
}
