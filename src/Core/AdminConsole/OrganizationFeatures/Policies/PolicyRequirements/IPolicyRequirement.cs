#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Represents the business requirements of how one or more enterprise policies will be enforced against a user.
/// The implementation of this interface will depend on how the policies are enforced in the relevant domain.
/// </summary>
public interface IPolicyRequirement;

/// <summary>
/// An interface that defines how to create a single <see cref="IPolicyRequirement"/> from a sequence of
/// <see cref="PolicyDetails"/>.
/// </summary>
public interface IRequirementFactory<out T> where T : IPolicyRequirement
{
    /// <summary>
    /// The PolicyType that corresponds to the <see cref="Policy"/> and the resulting <see cref="IPolicyRequirement"/>.
    /// </summary>
    PolicyType PolicyType { get; }

    /// <summary>
    /// A filter function that removes <see cref="PolicyDetails"/> that shouldn't be enforced against the user - for
    /// example, because the user's role is exempt, because they are not in the required status, or because they are a provider.
    /// </summary>
    /// <param name="policyDetails">Policy details for the defined PolicyType.</param>
    /// <returns></returns>
    IEnumerable<PolicyDetails> Filter(IEnumerable<PolicyDetails> policyDetails);

    /// <summary>
    /// A reducer method that creates a single <see cref="IPolicyRequirement"/> from the PolicyDetails.
    /// </summary>
    /// <param name="policyDetails">PolicyDetails for the specified PolicyType, after they have been filtered per above.</param>
    /// <returns></returns>
    T Create(IEnumerable<PolicyDetails> policyDetails);
}
