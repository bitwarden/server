using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

public class BlockClaimedDomainAccountCreationPolicyEventHandler : IPolicyValidationEvent
{
    private readonly IOrganizationHasVerifiedDomainsQuery _organizationHasVerifiedDomainsQuery;

    public BlockClaimedDomainAccountCreationPolicyEventHandler(
        IOrganizationHasVerifiedDomainsQuery organizationHasVerifiedDomainsQuery)
    {
        _organizationHasVerifiedDomainsQuery = organizationHasVerifiedDomainsQuery;
    }

    public PolicyType Type => PolicyType.BlockClaimedDomainAccountCreation;

    public async Task<string> ValidateAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        var policyUpdate = policyRequest.PolicyUpdate;

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
}
