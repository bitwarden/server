using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

public class OrganizationUserNotificationPolicyEventHandlerTests
{
    [Fact]
    public void Type_ReturnsOrganizationUserNotificationPolicy()
    {
        var validator = new OrganizationUserNotificationPolicyEventHandler();

        Assert.Equal(PolicyType.OrganizationUserNotification, validator.Type);
    }

    [Fact]
    public void RequiredPolicies_ReturnsSingleOrg()
    {
        var validator = new OrganizationUserNotificationPolicyEventHandler();

        Assert.Equal([PolicyType.SingleOrg], validator.RequiredPolicies);
    }
}
