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

public interface IPolicyUpdateEvent
{
    /// <summary>
    /// The PolicyType that this definition relates to.
    /// </summary>
    public PolicyType Type { get; }
}

public interface IOnPolicyPostSaveEvent : IPolicyUpdateEvent
{
    public Task PostSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy);
}

public interface IOnPolicyPreSaveEvent : IPolicyUpdateEvent
{
    /// <summary>
    /// Performs side effects after a policy is validated but before it is saved.
    /// For example, this can be used to remove non-compliant users from the organization.
    /// Implementation is optional; by default it will not perform any side effects.
    /// </summary>
    /// <param name="policyUpdate">The policy update request</param>
    /// <param name="currentPolicy">The current policy, if any</param>
    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy);
}

public interface IEnforceDependentPoliciesEvent : IPolicyUpdateEvent
{
    /// <summary>
    /// PolicyTypes that must be enabled before this policy can be enabled, if any.
    /// These dependencies will be checked when this policy is enabled and when any required policy is disabled.
    /// </summary>
    public IEnumerable<PolicyType> RequiredPolicies { get; }
}

public interface IPolicyValidationEvent : IPolicyUpdateEvent
{
    /// <summary>
    /// Validates a policy before saving it.
    /// Do not use this for simple dependencies between different policies - see <see cref="RequiredPolicies"/> instead.
    /// Implementation is optional; by default it will not perform any validation.
    /// </summary>
    /// <param name="policyUpdate">The policy update request</param>
    /// <param name="currentPolicy">The current policy, if any</param>
    /// <returns>A validation error if validation was unsuccessful, otherwise an empty string</returns>
    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy);
}

public class NoOpPolicyHandler :
    IPolicyValidationEvent,
    IEnforceDependentPoliciesEvent,
    IOnPolicyPreSaveEvent,
    IOnPolicyPostSaveEvent
{
    private readonly PolicyType _policyType;

    // Jimmy Since it requires a policy type, not sure if this is the right move.

    // Jimmy another issue with noops
    // return (T)(object)new NoOpPolicyHandler(policyType);
    public NoOpPolicyHandler(PolicyType policyType)
    {
        _policyType = policyType;
    }

    /// <summary>
    /// The PolicyType that this definition relates to.
    /// </summary>
    public PolicyType Type => _policyType;

    /// <summary>
    /// No validation is performed - always returns success.
    /// </summary>
    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        return Task.FromResult(string.Empty);
    }

    /// <summary>
    /// No policy dependencies are required.
    /// </summary>
    public IEnumerable<PolicyType> RequiredPolicies => Enumerable.Empty<PolicyType>();

    /// <summary>
    /// No pre-save side effects are performed.
    /// </summary>
    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// No post-save side effects are performed.
    /// </summary>
    public Task PostSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        return Task.CompletedTask;
    }
}


public class PolicyEventOrchestrator2(
    IEnumerable<IPolicyValidationEvent> validationHandlers,
    IEnumerable<IEnforceDependentPoliciesEvent> dependencyHandlers,
    IEnumerable<IOnPolicyPreSaveEvent> preSaveHandlers,
    IEnumerable<IOnPolicyPostSaveEvent> postSaveHandlers)
{
    public T? GetHandler<T>(PolicyType policyType) where T : IPolicyUpdateEvent
    {
        var handlers = GetHandlerCollection<T>();

        return handlers.SingleOrDefault(h => h.Type == policyType);
    }

    private IEnumerable<T> GetHandlerCollection<T>() where T : IPolicyUpdateEvent
    {
        return typeof(T) switch
        {
            var t when t == typeof(IPolicyValidationEvent) => validationHandlers.Cast<T>(),
            var t when t == typeof(IEnforceDependentPoliciesEvent) => dependencyHandlers.Cast<T>(),
            var t when t == typeof(IOnPolicyPreSaveEvent) => preSaveHandlers.Cast<T>(),
            var t when t == typeof(IOnPolicyPostSaveEvent) => postSaveHandlers.Cast<T>(),
            _ => throw new ArgumentException($"Unsupported handler type: {typeof(T)}")
        };
    }
}
