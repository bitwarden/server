using System.Reflection;
using Bit.Api.Dirt.Public.Controllers;
using Bit.Core.Auth.Identity;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Bit.Api.Test.Dirt.Public.Controllers;

public class EventsControllerTests
{
    [Fact]
    public void List_RequiresOnlyEventsReadPolicy()
    {
        var action = typeof(EventsController).GetMethod(nameof(EventsController.List))!;

        var policies = typeof(EventsController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>())
            .Select(a => a.Policy);

        Assert.Equal([Policies.OrganizationEventsRead], policies);
    }
}
