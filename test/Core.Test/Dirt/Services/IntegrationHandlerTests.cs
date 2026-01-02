using System.Net;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Services;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

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

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void ClassifyHttpStatusCode_AuthenticationFailed(HttpStatusCode code)
    {
        Assert.Equal(
            IntegrationFailureCategory.AuthenticationFailed,
            TestIntegrationHandler.Classify(code));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public void ClassifyHttpStatusCode_ConfigurationError(HttpStatusCode code)
    {
        Assert.Equal(
            IntegrationFailureCategory.ConfigurationError,
            TestIntegrationHandler.Classify(code));
    }

    [Fact]
    public void ClassifyHttpStatusCode_TooManyRequests_IsRateLimited()
    {
        Assert.Equal(
            IntegrationFailureCategory.RateLimited,
            TestIntegrationHandler.Classify(HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public void ClassifyHttpStatusCode_RequestTimeout_IsTransient()
    {
        Assert.Equal(
            IntegrationFailureCategory.TransientError,
            TestIntegrationHandler.Classify(HttpStatusCode.RequestTimeout));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void ClassifyHttpStatusCode_Common5xx_AreTransient(HttpStatusCode code)
    {
        Assert.Equal(
            IntegrationFailureCategory.TransientError,
            TestIntegrationHandler.Classify(code));
    }

    [Fact]
    public void ClassifyHttpStatusCode_ServiceUnavailable_IsServiceUnavailable()
    {
        Assert.Equal(
            IntegrationFailureCategory.ServiceUnavailable,
            TestIntegrationHandler.Classify(HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public void ClassifyHttpStatusCode_NotImplemented_IsPermanentFailure()
    {
        Assert.Equal(
            IntegrationFailureCategory.PermanentFailure,
            TestIntegrationHandler.Classify(HttpStatusCode.NotImplemented));
    }

    [Fact]
    public void FClassifyHttpStatusCode_Unhandled3xx_IsConfigurationError()
    {
        Assert.Equal(
            IntegrationFailureCategory.ConfigurationError,
            TestIntegrationHandler.Classify(HttpStatusCode.Found));
    }

    [Fact]
    public void ClassifyHttpStatusCode_Unhandled4xx_IsConfigurationError()
    {
        Assert.Equal(
            IntegrationFailureCategory.ConfigurationError,
            TestIntegrationHandler.Classify(HttpStatusCode.BadRequest));
    }

    [Fact]
    public void ClassifyHttpStatusCode_Unhandled5xx_IsServiceUnavailable()
    {
        Assert.Equal(
            IntegrationFailureCategory.ServiceUnavailable,
            TestIntegrationHandler.Classify(HttpStatusCode.HttpVersionNotSupported));
    }

    [Fact]
    public void ClassifyHttpStatusCode_UnknownCode_DefaultsToServiceUnavailable()
    {
        // cast an out-of-range value to ensure default path is stable
        Assert.Equal(
            IntegrationFailureCategory.ServiceUnavailable,
            TestIntegrationHandler.Classify((HttpStatusCode)799));
    }

    private class TestIntegrationHandler : IntegrationHandlerBase<WebhookIntegrationConfigurationDetails>
    {
        public override Task<IntegrationHandlerResult> HandleAsync(
            IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
        {
            return Task.FromResult(IntegrationHandlerResult.Succeed(message: message));
        }

        public static IntegrationFailureCategory Classify(HttpStatusCode code) => ClassifyHttpStatusCode(code);
    }
}
