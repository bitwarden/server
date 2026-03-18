#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class OrganizationUserNotificationPolicyValidator : IPolicyValidator, IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.OrganizationUserNotificationPolicy;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.FromResult(string.Empty);
    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.CompletedTask;
}
