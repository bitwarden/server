using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class UriMatchDefaultPolicyValidator : IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.UriMatchDefaults;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
}
