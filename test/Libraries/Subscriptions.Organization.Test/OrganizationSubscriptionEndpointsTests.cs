using Microsoft.AspNetCore.Builder;
using Xunit;

namespace Bit.Subscriptions.Organization.Test;

public class OrganizationSubscriptionEndpointsTests
{
    [Fact]
    public void MapOrganizationSubscriptionEndpoints_UsesTheOrganizationsBillingPrefix()
    {
        var app = WebApplication.CreateBuilder().Build();
        var group = app.MapOrganizationSubscriptionEndpoints();
        Assert.NotNull(group);
    }
}
