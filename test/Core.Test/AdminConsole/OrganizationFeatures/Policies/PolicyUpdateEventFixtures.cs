using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using NSubstitute;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

public class FakeSingleOrgDependencyEvent : IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.SingleOrg;
    public IEnumerable<PolicyType> RequiredPolicies => [];
}

public class FakeRequireSsoDependencyEvent : IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.RequireSso;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
}

public class FakeVaultTimeoutDependencyEvent : IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.MaximumVaultTimeout;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
}

public class FakeSingleOrgValidationEvent : IPolicyValidationEvent
{
    public PolicyType Type => PolicyType.SingleOrg;

    public readonly Func<SavePolicyModel, Policy?, Task<string>> ValidateAsyncMock = Substitute.For<Func<SavePolicyModel, Policy?, Task<string>>>();

    public Task<string> ValidateAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        return ValidateAsyncMock(policyRequest, currentPolicy);
    }
}
