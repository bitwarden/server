using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class SingleOrganizationPolicyRequirement(
    IEnumerable<PolicyDetails> policyDetails,
    Func<PolicyDetails, Task<bool>> enforce) : IPolicyRequirement
{
    public async Task<bool> IsSingleOrgEnabledForThisOrganizationAsync(Guid organizationId)
    {
        foreach (var policy in policyDetails)
        {
            if (policy.OrganizationId == organizationId && await enforce(policy))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> IsSingleOrgEnabledForOrganizationsOtherThanAsync(Guid organizationId)
    {

        foreach (var policy in policyDetails)
        {
            if (policy.OrganizationId != organizationId && await enforce(policy))
            {
                return true;
            }
        }

        return false;
    }
}

public class SingleOrganizationPolicyRequirementFactory(
    IApplicationCacheService applicationCacheService,
    IPolicyRepository policyRepository) : IPolicyRequirementFactory<SingleOrganizationPolicyRequirement>
{
    public PolicyType PolicyType => PolicyType.SingleOrg;

    // This has complex logic, so enforce filtering is done in the requirement object itself
    public bool Enforce(PolicyDetails policyDetails) => true;

    public SingleOrganizationPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails) =>
        new(policyDetails, policy =>
        {
            // conditional logic here, returning true if the policy should be enforced.
            // This should take into account the user role, status, and whether they are a provider.
            // Those checks would be more or less strict depending on whether autoconfirm applies to the org.
            // We can check the appCacheService first to shortcut the extra db call in most cases.
        });
}
