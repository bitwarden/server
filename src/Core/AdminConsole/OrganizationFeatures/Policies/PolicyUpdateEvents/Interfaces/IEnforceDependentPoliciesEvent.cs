using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

public interface IEnforceDependentPoliciesEvent : IPolicyUpdateEvent
{
    /// <summary>
    /// PolicyTypes that must be enabled before this policy can be enabled, if any.
    /// These dependencies will be checked when this policy is enabled and when any required policy is disabled.
    /// </summary>
    public IEnumerable<PolicyType> RequiredPolicies { get; }
}
