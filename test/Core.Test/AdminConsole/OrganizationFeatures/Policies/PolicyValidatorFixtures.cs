#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Services;
using NSubstitute;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

public class FakeSingleOrgPolicyValidator : IPolicyValidator
{
    public PolicyType Type => PolicyType.SingleOrg;
    public IEnumerable<PolicyType> RequiredPolicies => Array.Empty<PolicyType>();

    public readonly Func<PolicyUpdate, Policy?, Task<string>> ValidateAsyncMock = Substitute.For<Func<PolicyUpdate, Policy?, Task<string>>>();
    public readonly Action<PolicyUpdate, Policy?, IOrganizationService> OnSaveSideEffectsAsyncMock = Substitute.For<Action<PolicyUpdate, Policy?, IOrganizationService>>();

    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        return ValidateAsyncMock(policyUpdate, currentPolicy);
    }

    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy, IOrganizationService organizationService)
    {
        OnSaveSideEffectsAsyncMock(policyUpdate, currentPolicy, organizationService);
        return Task.FromResult(0);
    }
}
public class FakeRequireSsoPolicyValidator : IPolicyValidator
{
    public PolicyType Type => PolicyType.RequireSso;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
}
