#nullable enable

using Bit.Core.AdminConsole.Enums;
using OneOf;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

/// <summary>
/// Provides policy-specific event handlers used during the save workflow in <see cref="IVNextSavePolicyCommand"/>.
/// </summary>
/// <remarks>
/// Supported handlers:  
/// - <see cref="IEnforceDependentPoliciesEvent"/> for dependency checks  
/// - <see cref="IPolicyValidationEvent"/> for custom validation  
/// - <see cref="IOnPolicyPreUpdateEvent"/> for pre-save logic  
/// - <see cref="IOnPolicyPostUpdateEvent"/> for post-save logic  
/// </remarks>
public interface IPolicyEventHandlerFactory
{
/// <summary>
    /// Gets the event handler for the given policy type and handler interface.
    /// </summary>
    /// <typeparam name="T">Handler type implementing <see cref="IPolicyUpdateEvent"/>.</typeparam>
    /// <param name="policyType">The policy type to resolve.</param>
    /// <returns>
    /// <see cref="OneOf{T, None}"/> — the handler if available, or None if not implemented.
    /// </returns>
    OneOf<T, None> GetHandler<T>(PolicyType policyType) where T : IPolicyUpdateEvent;
}
