using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

/// <summary>
/// Represents all side effects that should be executed before a policy is upserted.
/// </summary>
/// <remarks>
/// This should be added to policy handlers that need to perform side effects before policy upserts.
/// </remarks>
public interface IOnPolicyPreUpdateEvent : IPolicyUpdateEvent
{
    /// <summary>
    /// Performs side effects before a policy is upserted.
    /// For example, this can be used to remove non-compliant users from the organization.
    /// </summary>
    /// <param name="policyRequest">The policy save request containing the policy update and metadata</param>
    /// <param name="currentPolicy">The current policy, if any</param>
    public Task ExecutePreUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy? currentPolicy);
}
