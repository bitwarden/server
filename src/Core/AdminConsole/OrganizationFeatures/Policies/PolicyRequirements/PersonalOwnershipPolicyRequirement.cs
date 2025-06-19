using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Represents the personal ownership policy state.
/// </summary>
public enum PersonalOwnershipState
{
    /// <summary>
    /// Personal ownership is allowed - users can save items to their personal vault.
    /// </summary>
    Allowed,

    /// <summary>
    /// Personal ownership is restricted - members are required to save items to an organization.
    /// </summary>
    Restricted
}

/// <summary>
/// Policy requirements for the Disable Personal Ownership policy.
/// </summary>
public class PersonalOwnershipPolicyRequirement : IPolicyRequirement
{
    private readonly IEnumerable<Guid> _organizationIdsWithPolicyEnabled;

    /// <param name="personalOwnershipState">
    /// The personal ownership state for the user.
    /// </param>
    /// <param name="organizationIdsWithPolicyEnabled">
    /// The collection of Organization IDs that have the Disable Personal Ownership policy enabled.
    /// </param>
    public PersonalOwnershipPolicyRequirement(
        PersonalOwnershipState personalOwnershipState,
        IEnumerable<Guid> organizationIdsWithPolicyEnabled)
    {
        _organizationIdsWithPolicyEnabled = organizationIdsWithPolicyEnabled ?? [];
        State = personalOwnershipState;
    }

    /// <summary>
    /// The personal ownership policy state for the user.
    /// </summary>
    public PersonalOwnershipState State { get; }

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
        var personalOwnershipState = policyDetails.Any()
            ? PersonalOwnershipState.Restricted
            : PersonalOwnershipState.Allowed;
        var organizationIdsWithPolicyEnabled = policyDetails.Select(p => p.OrganizationId).ToHashSet();

        return new PersonalOwnershipPolicyRequirement(
            personalOwnershipState,
            organizationIdsWithPolicyEnabled);
    }
}
