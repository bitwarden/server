#nullable enable

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;

namespace Bit.Core.Services;

public class WebhookIntegrationHandler(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider)
    : IntegrationHandlerBase<WebhookIntegrationConfigurationDetails>
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);

    public const string HttpClientName = "WebhookIntegrationHandlerHttpClient";

    public override async Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, message.Configuration.Uri);
        request.Content = new StringContent(message.RenderedTemplate, Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(message.Configuration.Scheme))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                scheme: message.Configuration.Scheme,
                parameter: message.Configuration.Token
            );
        }
        var response = await _httpClient.SendAsync(request);
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
                        result.DelayUntilDate = timeProvider.GetUtcNow().AddSeconds(seconds).UtcDateTime;
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
