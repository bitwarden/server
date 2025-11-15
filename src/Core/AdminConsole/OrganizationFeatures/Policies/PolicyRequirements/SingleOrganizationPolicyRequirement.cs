using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class SingleOrganizationPolicyRequirement(IEnumerable<PolicyDetails> policyDetails) : IPolicyRequirement
{
    public bool IsSingleOrgEnabledForThisOrganization(Guid organizationId) =>
        policyDetails.Any(p => p.OrganizationId == organizationId);

    public bool IsSingleOrgEnabledForOrganizationsOtherThan(Guid organizationId) =>
        policyDetails.Any(p => p.OrganizationId != organizationId);
}

public class SingleOrganizationPolicyRequirementFactory : BasePolicyRequirementFactory<SingleOrganizationPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.SingleOrg;

    public override SingleOrganizationPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails) =>
        new(policyDetails);
}
