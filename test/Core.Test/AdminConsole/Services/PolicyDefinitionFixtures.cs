#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using NSubstitute;

namespace Bit.Core.Test.AdminConsole.Services;

public class FakeSingleOrgPolicyDefinition : IPolicyDefinition
{
    public PolicyType Type => PolicyType.SingleOrg;
    public IEnumerable<PolicyType> RequiredPolicies => Array.Empty<PolicyType>();
    public Func<Policy?, Policy, Task<string?>> Validate => Substitute.For<Func<Policy?, Policy, Task<string?>>>();
    public Func<Policy?, Policy, Task> OnSaveSideEffects => Substitute.For<Func<Policy?, Policy, Task>>();
}

public class FakeRequireSsoPolicyDefinition : IPolicyDefinition
{
    public PolicyType Type => PolicyType.RequireSso;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
    public Func<Policy?, Policy, Task<string?>> Validate => Substitute.For<Func<Policy?, Policy, Task<string?>>>();
    public Func<Policy?, Policy, Task> OnSaveSideEffects => Substitute.For<Func<Policy?, Policy, Task>>();
}

