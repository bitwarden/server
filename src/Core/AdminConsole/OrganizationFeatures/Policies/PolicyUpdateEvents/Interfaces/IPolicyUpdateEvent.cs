using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

public interface IPolicyUpdateEvent
{
    /// <summary>
    /// The policy type that the associated handler will handle.
    /// </summary>
    public PolicyType Type { get; }
}
