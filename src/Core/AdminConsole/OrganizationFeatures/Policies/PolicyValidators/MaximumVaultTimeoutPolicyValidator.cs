#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class MaximumVaultTimeoutPolicyValidator : IPolicyValidator
{
    public PolicyType Type => PolicyType.MaximumVaultTimeout;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.FromResult("");
    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.FromResult(0);
}
