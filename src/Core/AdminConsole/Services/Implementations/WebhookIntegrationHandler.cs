using System.Net;
using System.Text;
using Bit.Core.Models.Data.Integrations;

#nullable enable

namespace Bit.Core.Services;

public class WebhookIntegrationHandler(IHttpClientFactory httpClientFactory)
    : IntegrationHandlerBase<WebhookIntegrationConfigurationDetails>
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);

    public const string HttpClientName = "WebhookIntegrationHandlerHttpClient";

    public override async Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var content = new StringContent(message.RenderedTemplate, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(message.Configuration.url, content);
        var result = new IntegrationHandlerResult(success: response.IsSuccessStatusCode, message);

        switch (response.StatusCode)
        {
            case HttpStatusCode.TooManyRequests:
            case HttpStatusCode.RequestTimeout:
            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                result.Retryable = true;
                result.FailureReason = response.ReasonPhrase;

                if (response.Headers.TryGetValues("Retry-After", out var values) &&
                    int.TryParse(values.FirstOrDefault(), out var seconds))
                {
                    result.NotBeforeUtc = DateTime.UtcNow.AddSeconds(seconds);
                }
                break;
            default:
                result.Retryable = false;
                result.FailureReason = response.ReasonPhrase;
                break;
        }

        return result;
    }
}
