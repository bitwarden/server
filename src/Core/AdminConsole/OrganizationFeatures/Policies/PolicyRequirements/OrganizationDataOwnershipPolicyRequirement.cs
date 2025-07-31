using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class OrganizationDataOwnershipPolicyRequirement : ISinglePolicyRequirement
{
    public bool RequiresDefaultCollection { get; set; }
}

public class OrganizationDataOwnershipPolicyAggregateRequirement : IAggregatePolicyRequirement
{
    public bool CanSavePersonallyOwnedItems { get; set; }
}

public class OrganizationDataOwnershipPolicyRequirementFactory :
    ISinglePolicyRequirementFactory<OrganizationDataOwnershipPolicyRequirement>,
    IAggregatePolicyRequirementFactory<OrganizationDataOwnershipPolicyAggregateRequirement>
{
    public PolicyType PolicyType => PolicyType.OrganizationDataOwnership;
    public bool ExemptRoles(OrganizationUserType role) => role is OrganizationUserType.Owner or OrganizationUserType.Admin;
    public bool ExemptProviders => true;
    public bool EnforceInAcceptedStatus => true;

    public OrganizationDataOwnershipPolicyRequirement Create(PolicyDetails? policyDetails = null) => new()
    {
        RequiresDefaultCollection = policyDetails is not null
    };

    public OrganizationDataOwnershipPolicyAggregateRequirement Create(IEnumerable<PolicyDetails> policyDetails) => new()
    {
        CanSavePersonallyOwnedItems = policyDetails.Any()
    };
}
