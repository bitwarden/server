#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyStrategy<TData, TRequirement>
{
    /// <summary>
    /// The PolicyType that the strategy is responsible for handling.
    /// </summary>
    public PolicyType Type { get; }

    /// <summary>
    /// A factory that transforms the untyped Policy.Data JSON object to a domain specific object,
    /// usually used for additional policy configuration.
    /// </summary>
    public Func<object, TData>? DataFactory { get; }

    /// <summary>
    /// A predicate function that returns true if a policy should be enforced against a user
    /// and false otherwise. This does not need to check Organization.UsePolicies or Policy.Enabled.
    /// </summary>
    public Predicate<(OrganizationUser, Policy)> Filter { get; }

    /// <summary>
    /// A reducer function that reduces Policies into policy requirements (as defined by TRequirement).
    /// This is used to reconcile policies of the same type from different organizations and combine them into
    /// a single object that represents the requirements of the domain.
    /// </summary>
    public (Func<TRequirement, Policy> reducer, TRequirement initialValue) Reducer { get;  }

    /// <summary>
    /// Validates a policy before saving it.
    /// </summary>
    /// <param name="currentPolicy">The current policy, if any</param>
    /// <param name="modifiedPolicy">The modified policy to be saved</param>
    /// <returns>A sequence of validation errors if validation was unsuccessful</returns>
    public IEnumerable<string>? Validate(Policy? currentPolicy, Policy modifiedPolicy);

    /// <summary>
    /// Optionally performs side effects after a policy is validated but before it is saved.
    /// For example, this can be used to remove non-compliant users from the organization.
    /// </summary>
    /// <param name="currentPolicy">The current policy, if any</param>
    /// <param name="modifiedPolicy">The modified policy to be saved</param>
    public Task OnSaveSideEffects(Policy? currentPolicy, Policy modifiedPolicy);
}
