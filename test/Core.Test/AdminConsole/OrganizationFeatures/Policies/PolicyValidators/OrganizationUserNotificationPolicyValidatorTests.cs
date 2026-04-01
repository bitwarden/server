using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class OrganizationUserNotificationPolicyValidatorTests
{
    [Fact]
    public void Type_ReturnsOrganizationUserNotificationPolicy()
    {
        var validator = new OrganizationUserNotificationPolicyValidator();

        Assert.Equal(PolicyType.OrganizationUserNotification, validator.Type);
    }

    [Fact]
    public void RequiredPolicies_ReturnsSingleOrg()
    {
        var validator = new OrganizationUserNotificationPolicyValidator();

        Assert.Equal([PolicyType.SingleOrg], validator.RequiredPolicies);
    }
}
