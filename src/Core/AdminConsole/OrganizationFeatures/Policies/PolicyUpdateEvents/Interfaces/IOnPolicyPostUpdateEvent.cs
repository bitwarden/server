using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
public interface IOnPolicyPostUpdateEvent : IPolicyUpdateEvent
{
    /// <summary>
    /// Performs side effects after a policy has been upserted.
    /// For example, this can be used for cleanup tasks or notifications.
    /// </summary>
    /// <param name="policyRequest">The policy save request</param>
    /// <param name="postUpsertedPolicyState">The policy after it was upserted</param>
    /// <param name="previousPolicyState">The policy state before it was updated, if any</param>
    public Task ExecutePostUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy postUpsertedPolicyState,
        Policy? previousPolicyState);
}
