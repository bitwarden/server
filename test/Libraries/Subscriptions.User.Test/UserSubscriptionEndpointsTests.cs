using Microsoft.AspNetCore.Builder;
using Xunit;

namespace Bit.Subscriptions.User.Test;

public class UserSubscriptionEndpointsTests
{
    [Fact]
    public void MapUserSubscriptionEndpoints_UsesTheSubscriptionsPrefix()
    {
        var app = WebApplication.CreateBuilder().Build();
        var group = app.MapUserSubscriptionEndpoints();
        Assert.NotNull(group);
    }
}
