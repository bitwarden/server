using Bit.Core.Dirt.Enums;
using Xunit;

namespace Bit.Core.Test.Services;

public class IntegrationTypeTests
{
    [Fact]
    public void ToRoutingKey_CloudBillingSync_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegrationType.CloudBillingSync.ToRoutingKey());
    }

    [Fact]
    public void ToRoutingKey_Scim_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegrationType.Scim.ToRoutingKey());
    }

    [Fact]
    public void ToRoutingKey_Slack_Succeeds()
    {
        Assert.Equal("slack", IntegrationType.Slack.ToRoutingKey());
    }

    [Fact]
    public void ToRoutingKey_Webhook_Succeeds()
    {
        Assert.Equal("webhook", IntegrationType.Webhook.ToRoutingKey());
    }

    [Fact]
    public void ToRoutingKey_Hec_Succeeds()
    {
        Assert.Equal("hec", IntegrationType.Hec.ToRoutingKey());
    }

    [Fact]
    public void ToRoutingKey_Datadog_Succeeds()
    {
        Assert.Equal("datadog", IntegrationType.Datadog.ToRoutingKey());
    }

    [Fact]
    public void ToRoutingKey_Teams_Succeeds()
    {
        Assert.Equal("teams", IntegrationType.Teams.ToRoutingKey());
    }
}
