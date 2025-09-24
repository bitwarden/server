
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IOnPolicyPreUpsertEvent : IPolicyUpdateEvent
{
    /// <summary>
    /// Performs side effects before a policy is upserted.
    /// For example, this can be used to remove non-compliant users from the organization.
    /// </summary>
    /// <param name="policyUpdate">The policy update request</param>
    /// <param name="currentPolicy">The current policy, if any</param>
    public Task ExecutePreUpsertSideEffectAsync(PolicyUpdate policyUpdate, Policy? currentPolicy);
}
