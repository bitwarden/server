#nullable enable

using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class MaximumVaultTimeoutPolicyEventEventValidator : IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.MaximumVaultTimeout;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
}
