using Bit.Core.Enums;
using Xunit;

namespace Bit.Core.Test.Services;

public class IntegrationTypeTests
{
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
    public void ToRoutingKey_CloudBillingSync_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegrationType.CloudBillingSync.ToRoutingKey());
    }

    [Fact]
    public void ToRoutingKey_Scim_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IntegrationType.Scim.ToRoutingKey());
    }
}
