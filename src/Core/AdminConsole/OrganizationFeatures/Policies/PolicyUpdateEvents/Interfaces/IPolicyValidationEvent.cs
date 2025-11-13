using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

/// <summary>
/// Represents all validations that need to be run to enable or disable the given policy.
/// </summary>
/// <remarks>
/// This is used for the VNextSavePolicyCommand. This optional but should be implemented for all policies that have
/// certain requirements for the given organization.
/// </remarks>
public interface IPolicyValidationEvent : IPolicyUpdateEvent
{
    /// <summary>
    /// Performs any validations required to enable or disable the policy.
    /// </summary>
    /// <param name="policyRequest">The policy save request containing the policy update and metadata</param>
    /// <param name="currentPolicy">The current policy, if any</param>
    public Task<string> ValidateAsync(
        SavePolicyModel policyRequest,
        Policy? currentPolicy);

}
