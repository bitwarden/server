using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Represents the Organization Data Ownership policy state.
/// </summary>
public enum OrganizationDataOwnershipState
{
    /// <summary>
    /// Organization Data Ownership is enforced- members are required to save items to an organization.
    /// </summary>
    Enabled = 1,

    /// <summary>
    /// Organization Data Ownership is not enforced- users can save items to their personal vault.
    /// </summary>
    Disabled = 2
}

/// <summary>
/// Policy requirements for the Organization data ownership policy
/// </summary>
public class OrganizationDataOwnershipPolicyRequirement : IPolicyRequirement
{
    private readonly IEnumerable<Guid> _organizationIdsWithPolicyEnabled;
    private readonly Dictionary<Guid, Guid> _organizationUserIdsByOrgId;

    /// <param name="organizationDataOwnershipState">
    /// The organization data ownership state for the user.
    /// </param>
    /// <param name="organizationIdsWithPolicyEnabled">
    /// The collection of Organization IDs that have the Organization Data Ownership policy enabled.
    /// </param>
    /// <param name="organizationUserIdsByOrgId">
    /// A dictionary with the OrganizationId as the key and the OrganizationUserId as the value.
    /// </param>
    public OrganizationDataOwnershipPolicyRequirement(
        OrganizationDataOwnershipState organizationDataOwnershipState,
        IEnumerable<Guid> organizationIdsWithPolicyEnabled,
        Dictionary<Guid, Guid> organizationUserIdsByOrgId)
    {
        _organizationIdsWithPolicyEnabled = organizationIdsWithPolicyEnabled;
        _organizationUserIdsByOrgId = organizationUserIdsByOrgId;
        State = organizationDataOwnershipState;
    }

    /// <summary>
    /// The Organization data ownership policy state for the user.
    /// </summary>
    public OrganizationDataOwnershipState State { get; }

    /// <summary>
    /// Returns true if the Organization Data Ownership policy is enforced in that organization.
    /// </summary>
    public bool RequiresDefaultCollection(Guid organizationId)
    {
        return _organizationIdsWithPolicyEnabled.Contains(organizationId);
    }

    /// <summary>
    /// Return the OrganizationUserId for the given OrganizationId.
    /// </summary>
    public Guid? GetOrganizationUserId(Guid organizationId)
    {
        if (_organizationUserIdsByOrgId.TryGetValue(organizationId, out var orgUserId))
        {
            return orgUserId;
        }
        return null;
    }
}

public class OrganizationDataOwnershipPolicyRequirementFactory : BasePolicyRequirementFactory<OrganizationDataOwnershipPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.OrganizationDataOwnership;

    public override OrganizationDataOwnershipPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var organizationDataOwnershipState = policyDetails.Any()
            ? OrganizationDataOwnershipState.Enabled
            : OrganizationDataOwnershipState.Disabled;

        var organizationIdsWithPolicyEnabled = policyDetails
            .Select(p => p.OrganizationId)
            .ToHashSet();

        var organizationUserIdsByOrgId = policyDetails
            .GroupBy(p => p.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First().OrganizationUserId);

        return new OrganizationDataOwnershipPolicyRequirement(
            organizationDataOwnershipState,
            organizationIdsWithPolicyEnabled,
            organizationUserIdsByOrgId);
    }
}
