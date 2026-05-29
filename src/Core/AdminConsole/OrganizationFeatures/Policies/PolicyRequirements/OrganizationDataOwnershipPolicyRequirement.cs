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
    private readonly IEnumerable<PolicyDetails> _policyDetails;

    /// <param name="organizationDataOwnershipState">
    /// The organization data ownership state for the user.
    /// </param>
    /// <param name="policyDetails">
    /// An enumerable collection of PolicyDetails for the organizations.
    /// </param>
    public OrganizationDataOwnershipPolicyRequirement(
        OrganizationDataOwnershipState organizationDataOwnershipState,
        IEnumerable<PolicyDetails> policyDetails)
    {
        _policyDetails = policyDetails;
        State = organizationDataOwnershipState;
    }

    /// <summary>
    /// The Organization data ownership policy state for the user.
    /// </summary>
    public OrganizationDataOwnershipState State { get; }

    /// <summary>
    /// Gets a default collection request for enforcing the Organization Data Ownership policy.
    /// Only confirmed users are applicable.
    /// This indicates whether the user should have a default collection created for them when the policy is enabled,
    /// and if so, the relevant OrganizationUserId to create the collection for.
    /// </summary>
    /// <param name="organizationId">The organization ID to create the request for.</param>
    /// <returns>A DefaultCollectionRequest containing the OrganizationUserId and a flag indicating whether to create a default collection.</returns>
    public DefaultCollectionRequest GetDefaultCollectionRequestOnPolicyEnable(Guid organizationId)
    {
        var policyDetail = _policyDetails
            .FirstOrDefault(p => p.OrganizationId == organizationId);

        if (policyDetail != null && policyDetail.HasStatus([OrganizationUserStatusType.Confirmed]))
        {
            return new DefaultCollectionRequest(policyDetail.OrganizationUserId, true);
        }

        var noCollectionNeeded = new DefaultCollectionRequest(Guid.Empty, false);
        return noCollectionNeeded;
    }

    public DefaultCollectionRequest GetDefaultCollectionRequestOnConfirm(Guid organizationId)
    {
        var matchingOrgUserId =
            _policyDetails.FirstOrDefault(p => p.OrganizationId == organizationId)?.OrganizationUserId;

        return new DefaultCollectionRequest(
            OrganizationUserId: matchingOrgUserId.GetValueOrDefault(Guid.Empty),
            ShouldCreateDefaultCollection: matchingOrgUserId.HasValue);
    }

    /// <summary>
    /// Ignore storage limits if the organization has data ownership policy enabled.
    /// Allows users to seamlessly migrate their data into the organization without being blocked by storage limits.
    /// Organization admins will need to manage storage after migration should overages occur.
    /// </summary>
    public bool IgnoreStorageLimitsOnMigration(Guid organizationId)
    {
        return _policyDetails.Any(p => p.OrganizationId == organizationId &&
                                       p.OrganizationUserStatus == OrganizationUserStatusType.Confirmed);
    }

    /// <summary>
    /// Determines if a user is eligible for self-revocation under the Organization Data Ownership policy.
    /// A user is eligible if they are a confirmed member of the organization and the policy is enabled.
    /// This also handles exempt roles (Owner/Admin) and policy disabled state via the factory's Enforce predicate.
    /// </summary>
    /// <param name="organizationId">The organization ID to check.</param>
    /// <returns>True if the user is eligible for self-revocation (policy applies to them), false otherwise.</returns>
    /// <remarks>
    /// Self-revoke is used to opt out of migrating the user's personal vault to the organization as required by this policy.
    /// </remarks>
    public bool EligibleForSelfRevoke(Guid organizationId)
    {
        var policyDetail = _policyDetails
            .FirstOrDefault(p => p.OrganizationId == organizationId);

        return policyDetail?.HasStatus([OrganizationUserStatusType.Confirmed]) ?? false;
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

        return new OrganizationDataOwnershipPolicyRequirement(
            organizationDataOwnershipState,
            policyDetails);
    }
}
