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

    public override async Task<IntegrationHandlerResult> HandleAsync(
        IntegrationMessage<WebhookIntegrationConfigurationDetails> message)
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
        return ResultFromHttpResponse(response, message, timeProvider);
    }
}
