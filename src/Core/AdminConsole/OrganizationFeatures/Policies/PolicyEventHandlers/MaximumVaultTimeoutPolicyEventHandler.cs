using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

public class MaximumVaultTimeoutPolicyEventHandler : IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.MaximumVaultTimeout;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
}
