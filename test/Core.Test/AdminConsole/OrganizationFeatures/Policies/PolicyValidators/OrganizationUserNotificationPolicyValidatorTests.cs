using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class OrganizationUserNotificationPolicyValidatorTests
{
    [Fact]
    public void Type_ReturnsOrganizationUserNotificationPolicy()
    {
        var validator = new OrganizationUserNotificationPolicyValidator();

        Assert.Equal(PolicyType.OrganizationUserNotificationPolicy, validator.Type);
    }

    [Fact]
    public void RequiredPolicies_ReturnsSingleOrg()
    {
        var validator = new OrganizationUserNotificationPolicyValidator();

        Assert.Equal([PolicyType.SingleOrg], validator.RequiredPolicies);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_ReturnsNoError(
        [PolicyUpdate(PolicyType.OrganizationUserNotificationPolicy, true)] PolicyUpdate policyUpdate)
    {
        var validator = new OrganizationUserNotificationPolicyValidator();

        var result = await validator.ValidateAsync(policyUpdate, null);

        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_ReturnsNoError(
        [PolicyUpdate(PolicyType.OrganizationUserNotificationPolicy, false)] PolicyUpdate policyUpdate)
    {
        var validator = new OrganizationUserNotificationPolicyValidator();

        var result = await validator.ValidateAsync(policyUpdate, null);

        Assert.True(string.IsNullOrEmpty(result));
    }
}
