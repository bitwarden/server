#nullable enable

using System.Globalization;
using System.Net;
using System.Text;
using Bit.Core.AdminConsole.Models.Data.Integrations;

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
                result.FailureReason = response.ReasonPhrase ?? $"Failure with status code: {(int)response.StatusCode}";

                if (response.Headers.TryGetValues("Retry-After", out var values))
                {
                    var value = values.FirstOrDefault();
                    if (int.TryParse(value, out var seconds))
                    {
                        // Retry-after was specified in seconds. Adjust DelayUntilDate by the requested number of seconds.
                        result.DelayUntilDate = DateTime.UtcNow.AddSeconds(seconds);
                    }
                    else if (DateTimeOffset.TryParseExact(value,
                                 "r", // "r" is the round-trip format: RFC1123
                                 CultureInfo.InvariantCulture,
                                 DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                 out var retryDate))
                    {
                        // Retry-after was specified as a date. Adjust DelayUntilDate to the specified date.
                        result.DelayUntilDate = retryDate.UtcDateTime;
                    }
                }
                break;
            default:
                result.Retryable = false;
                result.FailureReason = response.ReasonPhrase ?? $"Failure with status code {(int)response.StatusCode}";
                break;
        }

        return result;
    }
}
