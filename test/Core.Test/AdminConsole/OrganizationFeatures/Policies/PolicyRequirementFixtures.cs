using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

/// <summary>
/// Intentionally simplified PolicyRequirement that just holds the input PolicyDetails for us to assert against.
/// </summary>
public class TestPolicyRequirement : IPolicyRequirement
{
    public IEnumerable<PolicyDetails> Policies { get; init; } = [];
}

public class TestPolicyRequirementFactory(Func<PolicyDetails, bool> enforce) : IPolicyRequirementFactory<TestPolicyRequirement>
{
    public PolicyType PolicyType => PolicyType.SingleOrg;

    public bool Enforce(PolicyDetails policyDetails) => enforce(policyDetails);

    public TestPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
        => new() { Policies = policyDetails };
}
