using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Determines whether a policy should be enforced against a user before they have access to the organization,
/// e.g. while they are joining it or transitioning status.
/// </summary>
/// <remarks>
/// This is non-generic so that factories can be matched regardless of the requirement type they produce.
/// See <see cref="BasePolicyRequirementFactory{T}"/> for the default implementation.
/// </remarks>
public interface IPreAccessPolicyRequirementFactory
{
    /// <summary>
    /// The <see cref="PolicyType"/> that the requirement relates to.
    /// </summary>
    PolicyType PolicyType { get; }

    /// <summary>
    /// A predicate that determines whether a policy should be enforced against a user in the proposed role.
    /// </summary>
    /// <remarks>
    /// Status exemptions are not applied, because the caller is evaluating a status transition to which the policy
    /// may apply.
    /// </remarks>
    /// <param name="proposedRole">The role the user will have in the organization.</param>
    /// <param name="isProvider">Whether the user is a provider user for the organization.</param>
    /// <returns>True if the policy should be enforced against the user, false otherwise.</returns>
    bool EnforcePreAccess(OrganizationUserType proposedRole, bool isProvider);
}
