using System.Reflection;
using Bit.Api.Billing.Public.Controllers;
using Bit.Core.Auth.Identity;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Bit.Api.Test.Billing.Public.Controllers;

public class OrganizationControllerTests
{
    [Theory]
    [InlineData(nameof(OrganizationController.GetSubscriptionAsync), Policies.OrganizationSubscriptionRead)]
    [InlineData(nameof(OrganizationController.PostSubscriptionAsync), Policies.OrganizationSubscriptionWrite)]
    public void Action_RequiresOnlyItsSubscriptionPolicy(string actionName, string expectedPolicy)
    {
        var action = typeof(OrganizationController).GetMethod(actionName)!;

        var policies = typeof(OrganizationController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>())
            .Select(a => a.Policy);

        Assert.Equal([expectedPolicy], policies);
    }
}
