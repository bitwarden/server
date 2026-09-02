#nullable enable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// An interface that defines how to create a single <see cref="IPolicyRequirement"/> from a sequence of
/// <see cref="PolicyDetails"/>.
/// </summary>
/// <typeparam name="T">The <see cref="IPolicyRequirement"/> that the factory produces.</typeparam>
/// <remarks>
/// See <see cref="BasePolicyRequirementFactory{T}"/> for a simple base implementation suitable for most policies.
/// </remarks>
public interface IPolicyRequirementFactory<out T> where T : IPolicyRequirement
{
    /// <summary>
    /// The <see cref="PolicyType"/> that the requirement relates to.
    /// </summary>
    PolicyType PolicyType { get; }

    /// <summary>
    /// A predicate that determines whether a policy should be enforced against the user.
    /// </summary>
    /// <remarks>Use this to exempt users based on their role, status or other attributes.</remarks>
    /// <param name="policyDetails">Policy details for the defined PolicyType.</param>
    /// <returns>True if the policy should be enforced against the user, false otherwise.</returns>
    bool Enforce(PolicyDetails policyDetails);

    /// <summary>
    /// A reducer method that creates a single <see cref="IPolicyRequirement"/> from a set of PolicyDetails.
    /// </summary>
    /// <param name="policyDetails">
    /// PolicyDetails for the specified PolicyType, after they have been filtered by the Enforce predicate. That is,
    /// this is the final interface to be called.
    /// </param>
    T Create(IEnumerable<PolicyDetails> policyDetails);
}
