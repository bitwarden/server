using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

namespace Bit.Api.AdminConsole.Models.Response.Helpers;

public static class PolicyDetailResponses
{
    public static async Task<PolicyDetailResponseModel> GetSingleOrgPolicyDetailResponseAsync(
        this Policy policy,
        IOrganizationHasVerifiedDomainsQuery hasVerifiedDomainsQuery
    )
    {
        if (policy.Type is not PolicyType.SingleOrg)
        {
            throw new ArgumentException(
                $"'{nameof(policy)}' must be of type '{nameof(PolicyType.SingleOrg)}'.",
                nameof(policy)
            );
        }

        return new PolicyDetailResponseModel(
            policy,
            !await hasVerifiedDomainsQuery.HasVerifiedDomainsAsync(policy.OrganizationId)
        );
    }
}
