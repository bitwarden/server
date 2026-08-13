using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

public class OrganizationUserNotificationPolicyEventHandler : IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.OrganizationUserNotification;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
}
