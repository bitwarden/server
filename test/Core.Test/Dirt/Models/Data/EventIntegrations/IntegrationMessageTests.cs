using System.Text.Json;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Xunit;

namespace Bit.Core.Test.Dirt.Models.Data.EventIntegrations;

public class IntegrationMessageTests
{
    private const string _messageId = "TestMessageId";
    private const string _organizationId = "TestOrganizationId";

    [Fact]
    public void ApplyRetry_IncrementsRetryCountAndSetsDelayUntilDate()
    {
        var message = new IntegrationMessage<WebhookIntegrationConfigurationDetails>
        {
            Configuration = new WebhookIntegrationConfigurationDetails(new Uri("https://localhost"), "Bearer", "AUTH-TOKEN"),
            MessageId = _messageId,
            OrganizationId = _organizationId,
            RetryCount = 2,
            RenderedTemplate = string.Empty,
            DelayUntilDate = null
        };

        var baseline = DateTime.UtcNow;
        message.ApplyRetry(baseline);

        Assert.Equal(3, message.RetryCount);
        Assert.NotNull(message.DelayUntilDate);
        Assert.True(message.DelayUntilDate > baseline);
    }

    [Fact]
    public void FromToJson_SerializesCorrectly()
    {
        var message = new IntegrationMessage<WebhookIntegrationConfigurationDetails>
        {
            Configuration = new WebhookIntegrationConfigurationDetails(new Uri("https://localhost"), "Bearer", "AUTH-TOKEN"),
            MessageId = _messageId,
            OrganizationId = _organizationId,
            RenderedTemplate = "This is the message",
            IntegrationType = IntegrationType.Webhook,
            RetryCount = 2,
            DelayUntilDate = DateTime.UtcNow
        };

        var json = message.ToJson();
        var result = IntegrationMessage<WebhookIntegrationConfigurationDetails>.FromJson(json);

        Assert.NotNull(result);
        Assert.Equal(message.Configuration, result.Configuration);
        Assert.Equal(message.MessageId, result.MessageId);
        Assert.Equal(message.OrganizationId, result.OrganizationId);
        Assert.Equal(message.RenderedTemplate, result.RenderedTemplate);
        Assert.Equal(message.IntegrationType, result.IntegrationType);
        Assert.Equal(message.RetryCount, result.RetryCount);
        Assert.Equal(message.DelayUntilDate, result.DelayUntilDate);
    }

    [Fact]
    public void FromJson_InvalidJson_ThrowsJsonException()
    {
        var json = "{ Invalid JSON";
        Assert.Throws<JsonException>(() => IntegrationMessage<WebhookIntegrationConfigurationDetails>.FromJson(json));
    }

    [Fact]
    public void ToJson_BaseIntegrationMessage_DeserializesCorrectly()
    {
        var message = new IntegrationMessage
        {
            MessageId = _messageId,
            OrganizationId = _organizationId,
            RenderedTemplate = "This is the message",
            IntegrationType = IntegrationType.Webhook,
            RetryCount = 2,
            DelayUntilDate = DateTime.UtcNow
        };

        var json = message.ToJson();
        var result = JsonSerializer.Deserialize<IntegrationMessage>(json);

        Assert.Equal(message.MessageId, result.MessageId);
        Assert.Equal(message.OrganizationId, result.OrganizationId);
        Assert.Equal(message.RenderedTemplate, result.RenderedTemplate);
        Assert.Equal(message.IntegrationType, result.IntegrationType);
        Assert.Equal(message.RetryCount, result.RetryCount);
        Assert.Equal(message.DelayUntilDate, result.DelayUntilDate);
    }
}
