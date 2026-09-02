using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

/// <summary>
/// Represents all policies required to be enabled before the given policy can be enabled.
/// </summary>
/// <remarks>
/// This interface is intended for policy event handlers that mandate the activation of other policies
/// as prerequisites for enabling the associated policy.
/// </remarks>
public interface IEnforceDependentPoliciesEvent : IPolicyUpdateEvent
{
    /// <summary>
    /// PolicyTypes that must be enabled before this policy can be enabled, if any.
    /// These dependencies will be checked when this policy is enabled and when any required policy is disabled.
    /// </summary>
    public IEnumerable<PolicyType> RequiredPolicies { get; }
}
