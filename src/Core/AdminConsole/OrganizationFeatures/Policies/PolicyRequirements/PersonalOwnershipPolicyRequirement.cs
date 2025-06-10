using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Disable Personal Ownership policy.
/// </summary>
public class PersonalOwnershipPolicyRequirement(
    bool disablePersonalOwnership,
    IEnumerable<Guid> organizationIdsWithPolicyEnabled) : IPolicyRequirement
{
    private readonly IEnumerable<Guid> _organizationIdsWithPolicyEnabled = organizationIdsWithPolicyEnabled ?? [];

    /// <summary>
    /// Indicates whether Personal Ownership is disabled for the user. If true, members are required to save items to an organization.
    /// </summary>
    public bool DisablePersonalOwnership { get; } = disablePersonalOwnership;

    /// <summary>
    /// Returns true if the Disable Personal Ownership policy is enforced in that organization.
    /// </summary>
    public bool RequiresDefaultCollection(Guid organizationId)
    {
        return _organizationIdsWithPolicyEnabled.Contains(organizationId);
    }
}

public class PersonalOwnershipPolicyRequirementFactory : BasePolicyRequirementFactory<PersonalOwnershipPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.PersonalOwnership;

    public override PersonalOwnershipPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var disablePersonalOwnership = policyDetails.Any();
        var organizationIdsWithPolicyEnabled = policyDetails.Select(p => p.OrganizationId).ToHashSet();

        return new PersonalOwnershipPolicyRequirement(disablePersonalOwnership, organizationIdsWithPolicyEnabled);
    }
}
