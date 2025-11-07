#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class AutomaticUserConfirmationPolicyValidator(
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository,
    IPolicyRepository policyRepository)
    : IPolicyValidator, IOnPolicyPreUpdateEvent
{
    public PolicyType Type => PolicyType.AutomaticUserConfirmation;

    private const string _singleOrgPolicyNotEnabledErrorMessage = "The Single organization policy must be enabled before enabling the Automatically confirm invited users policy.";
    private const string _usersNotCompliantWithSingleOrgErrorMessage = "All organization users must be compliant with the Single organization policy before enabling the Automatically confirm invited users policy. Please remove users who are members of multiple organizations.";
    private const string _providerUsersExistErrorMessage = "The organization has users with the Provider user type. Please remove provider users before enabling the Automatically confirm invited users policy.";
    private const string _usersEnrolledInAccountRecoveryErrorMessage = "All organization users must not be enrolled in account recovery before enabling the Automatically confirm invited users policy. Please remove account recovery enrollment for all users.";

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

    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        // No additional side effects needed beyond validation
        return Task.CompletedTask;
    }

    public async Task ExecutePreUpsertSideEffectAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        // Only validate when the policy is being enabled (previously disabled or null → now enabled)
        if (policyRequest.PolicyUpdate is not { Enabled: true })
        {
            return;
        }

        // If current policy is already enabled, no validation needed
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
        // Check 1: Validate Single Org policy is enabled and users are compliant
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

        // Check 3: Validate no users are enrolled in account recovery
        var accountRecoveryValidationError = await ValidateNoAccountRecoveryEnrollmentAsync(organizationId);
        if (!string.IsNullOrWhiteSpace(accountRecoveryValidationError))
        {
            return accountRecoveryValidationError;
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

    private async Task<string> ValidateNoAccountRecoveryEnrollmentAsync(Guid organizationId)
    {
        // Get all organization users (we need their IDs)
        var organizationUsers = await organizationUserRepository.GetManyByOrganizationAsync(organizationId, null);

        if (organizationUsers.Count == 0)
        {
            return string.Empty;
        }

        // Get account recovery details for all organization users
        var accountRecoveryDetails = await organizationUserRepository
            .GetManyAccountRecoveryDetailsByOrganizationUserAsync(
                organizationId,
                organizationUsers.Select(ou => ou.Id));

        // Check if any users have a ResetPasswordKey (indicating they are enrolled in account recovery)
        var usersWithAccountRecovery = accountRecoveryDetails
            .Where(ard => !string.IsNullOrWhiteSpace(ard.ResetPasswordKey))
            .ToList();

        if (usersWithAccountRecovery.Count > 0)
        {
            return _usersEnrolledInAccountRecoveryErrorMessage;
        }

        return string.Empty;
    }
}
