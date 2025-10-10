using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

public interface IPolicyValidationEvent : IPolicyUpdateEvent
{
    /// <summary>
    /// Performs side effects after a policy is validated but before it is saved.
    /// For example, this can be used to remove non-compliant users from the organization.
    /// Implementation is optional; by default, it will not perform any side effects.
    /// </summary>
    /// <param name="policyRequest">The policy save request containing the policy update and metadata</param>
    /// <param name="currentPolicy">The current policy, if any</param>
    public Task<string> ValidateAsync(
        SavePolicyModel policyRequest,
        Policy? currentPolicy);

}
