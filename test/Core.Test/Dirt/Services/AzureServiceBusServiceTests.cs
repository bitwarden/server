#nullable enable

using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Services.Implementations;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

public class AzureServiceBusServiceTests
{
    private static IntegrationMessage BuildMessage() => new()
    {
        IntegrationType = IntegrationType.Webhook,
        MessageId = "message-id",
        OrganizationId = "organization-id",
        RenderedTemplate = "{}"
    };

    [Fact]
    public void BuildIntegrationMessage_AppliesTimeToLive()
    {
        var timeToLive = TimeSpan.FromHours(6);

        var result = AzureServiceBusService.BuildIntegrationMessage(BuildMessage(), timeToLive);

        Assert.Equal(timeToLive, result.TimeToLive);
    }

    [Fact]
    public void BuildIntegrationMessage_NonPositiveTimeToLive_LeavesSubscriptionDefault()
    {
        var result = AzureServiceBusService.BuildIntegrationMessage(BuildMessage(), TimeSpan.Zero);

        Assert.Equal(TimeSpan.MaxValue, result.TimeToLive);
    }

    [Fact]
    public void BuildIntegrationMessage_WithoutScheduledEnqueueTime_DoesNotSchedule()
    {
        var result = AzureServiceBusService.BuildIntegrationMessage(BuildMessage(), TimeSpan.FromDays(1));

        Assert.Equal(default, result.ScheduledEnqueueTime);
    }

    [Fact]
    public void BuildIntegrationMessage_WithScheduledEnqueueTime_Schedules()
    {
        var scheduledEnqueueTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = AzureServiceBusService.BuildIntegrationMessage(
            BuildMessage(),
            TimeSpan.FromDays(1),
            scheduledEnqueueTime);

        Assert.Equal(scheduledEnqueueTime, result.ScheduledEnqueueTime);
    }

    [Fact]
    public void BuildIntegrationMessage_MapsRoutingAndPartitioning()
    {
        var message = BuildMessage();

        var result = AzureServiceBusService.BuildIntegrationMessage(message, TimeSpan.FromDays(1));

        Assert.Equal(message.IntegrationType.ToRoutingKey(), result.Subject);
        Assert.Equal(message.MessageId, result.MessageId);
        Assert.Equal(message.OrganizationId, result.PartitionKey);
        Assert.Equal(message.ToJson(), result.Body.ToString());
    }
}
