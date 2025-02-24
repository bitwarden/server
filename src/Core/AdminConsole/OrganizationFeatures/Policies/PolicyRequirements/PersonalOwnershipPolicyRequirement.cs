using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

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

    /// <summary>
    /// Creates a new PersonalOwnershipPolicyRequirement.
    /// </summary>
    /// <param name="policyDetails">All PolicyDetails relating to the user.</param>
    /// <remarks>
    /// This is a <see cref="RequirementFactory{T}"/> for the PersonalOwnershipPolicyRequirement.
    /// </remarks>
    public static PersonalOwnershipPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var filteredPolicies = policyDetails
            .ExemptRoles([OrganizationUserType.Owner, OrganizationUserType.Admin])
            .ExemptStatus([OrganizationUserStatusType.Invited, OrganizationUserStatusType.Revoked])
            .ExemptProviders()
            .ToList();

        var result = new PersonalOwnershipPolicyRequirement
        {
            DisablePersonalOwnership = filteredPolicies.GetPolicyType(PolicyType.PersonalOwnership).Any()
        };

        return result;
    }
}
