using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;
using Bit.Core.Services;
using Xunit;

namespace Bit.Core.Test.Services;

public class IntegrationHandlerTests
{

    [Fact]
    public async Task HandleAsync_ConvertsJsonToTypedIntegrationMessage()
    {
        var sut = new TestIntegrationHandler();
        var expected = new IntegrationMessage<WebhookIntegrationConfigurationDetails>()
        {
            Configuration = new WebhookIntegrationConfigurationDetails(new Uri("https://localhost"), "Bearer", "AUTH-TOKEN"),
            MessageId = "TestMessageId",
            OrganizationId = "TestOrganizationId",
            IntegrationType = IntegrationType.Webhook,
            RenderedTemplate = "Template",
            DelayUntilDate = null,
            RetryCount = 0
        };

        var result = await sut.HandleAsync(expected.ToJson());
        var typedResult = Assert.IsType<IntegrationMessage<WebhookIntegrationConfigurationDetails>>(result.Message);

        Assert.Equal(expected.MessageId, typedResult.MessageId);
        Assert.Equal(expected.OrganizationId, typedResult.OrganizationId);
        Assert.Equal(expected.Configuration, typedResult.Configuration);
        Assert.Equal(expected.RenderedTemplate, typedResult.RenderedTemplate);
        Assert.Equal(expected.IntegrationType, typedResult.IntegrationType);
    }

    private class TestIntegrationHandler : IntegrationHandlerBase<WebhookIntegrationConfigurationDetails>
    {
        public override Task<IntegrationHandlerResult> HandleAsync(
            IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
        {
            var result = new IntegrationHandlerResult(success: true, message: message);
            return Task.FromResult(result);
        }
    }
}
