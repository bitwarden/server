#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyDefinition
{
    /// <summary>
    /// The PolicyType that the strategy is responsible for handling.
    /// </summary>
    public PolicyType Type { get; }

    /// <summary>
    /// PolicyTypes that must be enabled before this policy can be enabled, if any.
    /// </summary>
    public IEnumerable<PolicyType> RequiredPolicies { get; }

    /// <summary>
    /// Validates a policy before saving it.
    /// Basic interdependencies between policies are already handled by the <see cref="RequiredPolicies"/> definition.
    /// Use this for additional or more complex validation, if any.
    /// </summary>
    /// <param name="currentPolicy">The current policy, if any</param>
    /// <param name="modifiedPolicy">The modified policy to be saved</param>
    /// <returns>A sequence of validation errors if validation was unsuccessful</returns>
    public Task<string?> ValidateAsync(Policy? currentPolicy, Policy modifiedPolicy);

    /// <summary>
    /// Optionally performs side effects after a policy is validated but before it is saved.
    /// For example, this can be used to remove non-compliant users from the organization.
    /// </summary>
    /// <param name="currentPolicy">The current policy, if any</param>
    /// <param name="modifiedPolicy">The modified policy to be saved</param>
    public Task OnSaveSideEffectsAsync(Policy? currentPolicy, Policy modifiedPolicy);
}
