using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Disable Personal Ownership policy.
/// </summary>
public class PersonalOwnershipPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether Personal Ownership is disabled for the user. If true, members are required to save items to an organization.
    /// </summary>
    public bool DisablePersonalOwnership { get; init; }
}

public class PersonalOwnershipPolicyRequirementFactory : BasePolicyRequirementFactory<PersonalOwnershipPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.PersonalOwnership;

    public override PersonalOwnershipPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = new PersonalOwnershipPolicyRequirement { DisablePersonalOwnership = policyDetails.Any() };
        return result;
    }
}
