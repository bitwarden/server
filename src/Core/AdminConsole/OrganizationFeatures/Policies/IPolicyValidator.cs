#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Services;

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
    public IEnumerable<PolicyType> RequiredPolicies => [];

    /// <summary>
    /// Validates a policy before saving it.
    /// Do not use this for simple dependencies between different policies - see <see cref="RequiredPolicies"/> instead.
    /// Implementation is optional; by default it will not perform any validation.
    /// </summary>
    /// <param name="currentPolicy">The current policy, if any</param>
    /// <param name="modifiedPolicy">The modified policy to be saved</param>
    /// <returns>A sequence of validation errors if validation was unsuccessful</returns>
    public Task<string?> ValidateAsync(Policy? currentPolicy, Policy modifiedPolicy) => Task.FromResult<string?>(null);

    /// <summary>
    /// Performs side effects after a policy is validated but before it is saved.
    /// For example, this can be used to remove non-compliant users from the organization.
    /// Implementation is optional; by default it will not perform any side effects.
    /// </summary>
    /// <param name="currentPolicy">The current policy, if any</param>
    /// <param name="modifiedPolicy">The modified policy to be saved</param>
    public Task OnSaveSideEffectsAsync(Policy? currentPolicy, Policy modifiedPolicy, IOrganizationService organizationService) => Task.FromResult(0);
}
