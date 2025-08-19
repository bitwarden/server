using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

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
    private readonly Dictionary<Guid, PolicyDetails> _policyDetailsByOrganizationId;

    /// <param name="organizationDataOwnershipState">
    /// The organization data ownership state for the user.
    /// </param>
    /// <param name="policyDetailsByOrganizationId">
    /// A dictionary with the OrganizationId as the key and the PolicyDetails as the value.
    /// </param>
    public OrganizationDataOwnershipPolicyRequirement(
        OrganizationDataOwnershipState organizationDataOwnershipState,
        Dictionary<Guid, PolicyDetails> policyDetailsByOrganizationId)
    {
        _policyDetailsByOrganizationId = policyDetailsByOrganizationId;
        State = organizationDataOwnershipState;
    }

    /// <summary>
    /// The Organization data ownership policy state for the user.
    /// </summary>
    public OrganizationDataOwnershipState State { get; }

    /// <summary>
    /// Gets a default collection request for enforcing the Organization Data Ownership policy.
    /// Only accepted users are applicable.
    /// This indicates whether the user should have a default collection created for them when the policy is enabled,
    /// and if so, the relevant OrganizationUserId to create the collection for. Only accepted users are applicable.
    /// </summary>
    /// <param name="organizationId">The organization ID to create the request for.</param>
    /// <returns>A DefaultCollectionRequest containing the OrganizationUserId and a flag indicating whether to create a default collection.</returns>
    public DefaultCollectionRequest GetDefaultCollectionRequest(Guid organizationId)
    {
        var noCollectionNeeded = new DefaultCollectionRequest(Guid.Empty, false);

        if (!_policyDetailsByOrganizationId.TryGetValue(organizationId, out var policyDetails))
        {
            return noCollectionNeeded;
        }

        if (policyDetails.HasStatus([OrganizationUserStatusType.Confirmed]))
        {
            return new DefaultCollectionRequest(policyDetails.OrganizationUserId, true);
        }

        return noCollectionNeeded;

    }
}

public record DefaultCollectionRequest(Guid OrganizationUserId, bool ShouldCreateDefaultCollection)
{
    public readonly bool ShouldCreateDefaultCollection = ShouldCreateDefaultCollection;
    public readonly Guid OrganizationUserId = OrganizationUserId;
}

public class OrganizationDataOwnershipPolicyRequirementFactory : BasePolicyRequirementFactory<OrganizationDataOwnershipPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.OrganizationDataOwnership;

    public override OrganizationDataOwnershipPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var organizationDataOwnershipState = policyDetails.Any()
            ? OrganizationDataOwnershipState.Enabled
            : OrganizationDataOwnershipState.Disabled;

        var policyDetailsByOrganizationId = policyDetails
            .GroupBy(p => p.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First());

        return new OrganizationDataOwnershipPolicyRequirement(
            organizationDataOwnershipState,
            policyDetailsByOrganizationId);
    }
}
