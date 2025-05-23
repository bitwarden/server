﻿using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.Integrations;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Core.Test.Models.Data.Integrations;

public class IntegrationMessageTests
{
    [Fact]
    public void ApplyRetry_IncrementsRetryCountAndSetsDelayUntilDate()
    {
        var message = new IntegrationMessage<WebhookIntegrationConfigurationDetails>
        {
            RetryCount = 2,
            DelayUntilDate = null
        };

        var baseline = DateTime.UtcNow;
        message.ApplyRetry(baseline);

        Assert.Equal(3, message.RetryCount);
        Assert.True(message.DelayUntilDate > baseline);
    }

    [Fact]
    public void FromToJson_SerializesCorrectly()
    {
        var message = new IntegrationMessage<WebhookIntegrationConfigurationDetails>
        {
            Configuration = new WebhookIntegrationConfigurationDetails("https://localhost"),
            RenderedTemplate = "This is the message",
            IntegrationType = IntegrationType.Webhook,
            RetryCount = 2,
            DelayUntilDate = null
        };

        var json = message.ToJson();
        var result = IntegrationMessage<WebhookIntegrationConfigurationDetails>.FromJson(json);

        Assert.Equal(message.Configuration, result.Configuration);
        Assert.Equal(message.RenderedTemplate, result.RenderedTemplate);
        Assert.Equal(message.IntegrationType, result.IntegrationType);
        Assert.Equal(message.RetryCount, result.RetryCount);
    }

    [Fact]
    public void FromJson_InvalidJson_ThrowsJsonException()
    {
        var json = "{ Invalid JSON";
        Assert.Throws<JsonException>(() => IntegrationMessage<WebhookIntegrationConfigurationDetails>.FromJson(json));
    }
}
