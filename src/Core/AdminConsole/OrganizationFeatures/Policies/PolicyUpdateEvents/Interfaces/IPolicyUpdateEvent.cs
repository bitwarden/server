using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

/// <summary>
/// Represents the policy to be upserted.
/// </summary>
/// <remarks>
/// This is used for the VNextSavePolicyCommand. All policy handlers should implement this interface.
/// </remarks>
public interface IPolicyUpdateEvent
{
    /// <summary>
    /// The policy type that the associated handler will handle.
    /// </summary>
    public PolicyType Type { get; }
}
