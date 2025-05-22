#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// An interface that defines how to create a single <see cref="IPolicyRequirement"/> from a Policy.
/// </summary>
/// <typeparam name="T">The <see cref="IPolicyRequirement"/> that the factory produces.</typeparam>
public interface IOrganizationPolicyRequirementFactory<out T> where T : IPolicyRequirement
{
    /// <summary>
    /// The <see cref="PolicyType"/> that the requirement relates to.
    /// </summary>
    PolicyType PolicyType { get; }

    /// <summary>
    /// A reducer method that creates a <see cref="IPolicyRequirement"/> from a Policy.
    /// </summary>
    /// <param name="policy">The policy for the specified PolicyType.</param>
    T Create(Policy? policy);
}
