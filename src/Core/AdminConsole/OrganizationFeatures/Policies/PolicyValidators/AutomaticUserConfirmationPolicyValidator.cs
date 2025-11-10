using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

/// <summary>
/// Represents a validator for the Automatic User Confirmation policy.
///
/// This class validates that the following conditions are met:
/// <ul>
///     <li>The Single organization policy is enabled</li>
///     <li>All organization users are compliant with the Single organization policy</li>
///     <li>No provider users exist</li>
/// </ul>
///
/// This class also performs side effects when the policy is being enabled. They are:
/// <ul>
///     <li>Sets the UseAutomaticUserConfirmation organization feature to match the policy update</li>
/// </ul>
/// </summary>
public class AutomaticUserConfirmationPolicyValidator(
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository,
    IPolicyRepository policyRepository,
    IOrganizationRepository organizationRepository,
    TimeProvider timeProvider)
    : IPolicyValidator, IOnPolicyPreUpdateEvent
{
    public PolicyType Type => PolicyType.AutomaticUserConfirmation;

    private const string _singleOrgPolicyNotEnabledErrorMessage = "The Single organization policy must be enabled before enabling the Automatically confirm invited users policy.";
    private const string _usersNotCompliantWithSingleOrgErrorMessage = "All organization users must be compliant with the Single organization policy before enabling the Automatically confirm invited users policy. Please remove users who are members of multiple organizations.";
    private const string _providerUsersExistErrorMessage = "The organization has users with the Provider user type. Please remove provider users before enabling the Automatically confirm invited users policy.";

    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];

    public async Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        // Only perform validation when the policy is being enabled (previously disabled or null → now enabled)
        if (policyUpdate is not { Enabled: true })
        {
            return string.Empty;
        }

        // If current policy is already enabled, no validation needed
        if (currentPolicy is { Enabled: true })
        {
            return string.Empty;
        }

        return await ValidateEnablingPolicyAsync(policyUpdate.OrganizationId);
    }

    public async Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        var organization = await organizationRepository.GetByIdAsync(policyUpdate.OrganizationId);

        if (organization is not null)
        {
            organization.UseAutomaticUserConfirmation = policyUpdate.Enabled;
            organization.RevisionDate = timeProvider.GetUtcNow().UtcDateTime;
            await organizationRepository.UpsertAsync(organization);
        }
    }

    public async Task ExecutePreUpsertSideEffectAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        // Only validate when the policy is being enabled (previously disabled or null -> now enabled)
        if (policyRequest.PolicyUpdate is not { Enabled: true })
        {
            return;
        }

        // If the current policy is already enabled, no validation needed
        if (currentPolicy is { Enabled: true })
        {
            return;
        }

        var validationError = await ValidateEnablingPolicyAsync(policyRequest.PolicyUpdate.OrganizationId);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            throw new BadRequestException(validationError);
        }
    }

    private async Task<string> ValidateEnablingPolicyAsync(Guid organizationId)
    {
        // Check 1: Validate Single Org policy is enabled and all users are compliant
        var singleOrgValidationError = await ValidateSingleOrgPolicyComplianceAsync(organizationId);
        if (!string.IsNullOrWhiteSpace(singleOrgValidationError))
        {
            return singleOrgValidationError;
        }

        // Check 2: Validate no provider users exist
        var providerValidationError = await ValidateNoProviderUsersAsync(organizationId);
        if (!string.IsNullOrWhiteSpace(providerValidationError))
        {
            return providerValidationError;
        }

        return string.Empty;
    }

    private async Task<string> ValidateSingleOrgPolicyComplianceAsync(Guid organizationId)
    {
        // First check if Single Org policy is enabled (this is also handled by RequiredPolicies,
        // but we check explicitly to provide a better error message)
        var singleOrgPolicy = await policyRepository.GetByOrganizationIdTypeAsync(organizationId, PolicyType.SingleOrg);
        if (singleOrgPolicy is not { Enabled: true })
        {
            return _singleOrgPolicyNotEnabledErrorMessage;
        }

        // Get all active, non-revoked organization users (including Owners and Admins)
        var organizationUsers = (await organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId))
            .Where(ou => ou.Status != OrganizationUserStatusType.Invited &&
                         ou.Status != OrganizationUserStatusType.Revoked)
            .ToList();

        if (organizationUsers.Count == 0)
        {
            return string.Empty;
        }

        // Check if any users are members of multiple organizations
        var allUserOrgs = await organizationUserRepository.GetManyByManyUsersAsync(
            organizationUsers.Select(ou => ou.UserId!.Value));

        var nonCompliantUsers = organizationUsers.Where(ou =>
            allUserOrgs.Any(uo => uo.UserId == ou.UserId &&
                uo.OrganizationId != organizationId &&
                uo.Status != OrganizationUserStatusType.Invited)).ToList();

        if (nonCompliantUsers.Count > 0)
        {
            return _usersNotCompliantWithSingleOrgErrorMessage;
        }

        return string.Empty;
    }

    private async Task<string> ValidateNoProviderUsersAsync(Guid organizationId)
    {
        var providerUsers = await providerUserRepository.GetManyByOrganizationAsync(organizationId);

        return providerUsers.Count > 0 ? _providerUsersExistErrorMessage : string.Empty;
    }
}
