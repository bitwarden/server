#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class BlockClaimedDomainAccountCreationPolicyValidator : IPolicyValidator, IPolicyValidationEvent
{
    private readonly IOrganizationHasVerifiedDomainsQuery _organizationHasVerifiedDomainsQuery;

    public BlockClaimedDomainAccountCreationPolicyValidator(
        IOrganizationHasVerifiedDomainsQuery organizationHasVerifiedDomainsQuery)
    {
        _organizationHasVerifiedDomainsQuery = organizationHasVerifiedDomainsQuery;
    }

    public PolicyType Type => PolicyType.BlockClaimedDomainAccountCreation;

    // No prerequisites - this policy stands alone
    public IEnumerable<PolicyType> RequiredPolicies => [];

    public async Task<string> ValidateAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        return await ValidateAsync(policyRequest.PolicyUpdate, currentPolicy);
    }

    public async Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        // Only validate when trying to ENABLE the policy
        if (policyUpdate is { Enabled: true })
        {
            // Check if organization has at least one verified domain
            if (!await _organizationHasVerifiedDomainsQuery.HasVerifiedDomainsAsync(policyUpdate.OrganizationId))
            {
                return "You must claim at least one domain to turn on this policy";
            }
        }

        // Disabling the policy is always allowed
        return string.Empty;
    }

    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
        => Task.CompletedTask;
}
