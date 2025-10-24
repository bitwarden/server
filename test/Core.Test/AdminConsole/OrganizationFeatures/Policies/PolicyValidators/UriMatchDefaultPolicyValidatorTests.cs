using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class UriMatchDefaultPolicyValidatorTests
{
    private readonly UriMatchDefaultPolicyValidator _validator = new();

    [Fact]
    // Test that the Type property returns the correct PolicyType for this validator
    public void Type_ReturnsUriMatchDefaults()
    {
        Assert.Equal(PolicyType.UriMatchDefaults, _validator.Type);
    }

    [Fact]
    // Test that the RequiredPolicies property returns exactly one policy (SingleOrg) as a prerequisite
    // for enabling the UriMatchDefaults policy, ensuring proper policy dependency enforcement
    public void RequiredPolicies_ReturnsSingleOrgPolicy()
    {
        var requiredPolicies = _validator.RequiredPolicies.ToList();

        Assert.Single(requiredPolicies);
        Assert.Contains(PolicyType.SingleOrg, requiredPolicies);
    }
}
