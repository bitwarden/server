using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

namespace Bit.Api.AdminConsole.Models.Response.Helpers;

public static class PolicyStatusResponses
{
    public static async Task<PolicyStatusResponseModel> GetSingleOrgPolicyStatusResponseAsync(
        this PolicyStatus policy, IOrganizationHasVerifiedDomainsQuery hasVerifiedDomainsQuery)
    {
        if (policy.Type is not PolicyType.SingleOrg)
        {
            throw new ArgumentException($"'{nameof(policy)}' must be of type '{nameof(PolicyType.SingleOrg)}'.", nameof(policy));
        }

        return new PolicyStatusResponseModel(policy, await CanToggleState());

        async Task<bool> CanToggleState()
        {
            if (!await hasVerifiedDomainsQuery.HasVerifiedDomainsAsync(policy.OrganizationId))
            {
                return true;
            }

            return !policy.Enabled;
        }
    }
}
