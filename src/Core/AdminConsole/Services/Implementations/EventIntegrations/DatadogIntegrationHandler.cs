using System.Text;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;

namespace Bit.Core.Services;

public class DatadogIntegrationHandler(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider)
    : IntegrationHandlerBase<DatadogIntegrationConfigurationDetails>
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);

    public const string HttpClientName = "DatadogIntegrationHandlerHttpClient";

    public override async Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<DatadogIntegrationConfigurationDetails> message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, message.Configuration.Uri);
        request.Content = new StringContent(message.RenderedTemplate, Encoding.UTF8, "application/json");
        request.Headers.Add("DD-API-KEY", message.Configuration.ApiKey);

        var response = await _httpClient.SendAsync(request);

        return ResultFromHttpResponse(response, message, timeProvider);
    }
}
