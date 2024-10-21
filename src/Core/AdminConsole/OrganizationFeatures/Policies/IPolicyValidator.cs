#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

/// <summary>
/// Defines behavior and functionality for a given PolicyType.
/// </summary>
public interface IPolicyValidator
{
    /// <summary>
    /// The PolicyType that this definition relates to.
    /// </summary>
    public PolicyType Type { get; }

    /// <summary>
    /// PolicyTypes that must be enabled before this policy can be enabled, if any.
    /// These dependencies will be checked when this policy is enabled and when any required policy is disabled.
    /// </summary>
    public IEnumerable<PolicyType> RequiredPolicies { get; }

    /// <summary>
    /// Validates a policy before saving it.
    /// Do not use this for simple dependencies between different policies - see <see cref="RequiredPolicies"/> instead.
    /// Implementation is optional; by default it will not perform any validation.
    /// </summary>
    /// <param name="policyUpdate">The policy update request</param>
    /// <param name="currentPolicy">The current policy, if any</param>
    /// <returns>A validation error if validation was unsuccessful, otherwise an empty string</returns>
    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy);

    /// <summary>
    /// Performs side effects after a policy is validated but before it is saved.
    /// For example, this can be used to remove non-compliant users from the organization.
    /// Implementation is optional; by default it will not perform any side effects.
    /// </summary>
    /// <param name="policyUpdate">The policy update request</param>
    /// <param name="currentPolicy">The current policy, if any</param>
    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy);
}
