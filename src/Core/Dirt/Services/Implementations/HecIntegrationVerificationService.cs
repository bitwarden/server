using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Services.Implementations;

public class HecIntegrationVerificationService(
    IHttpClientFactory httpClientFactory,
    ILogger<HecIntegrationVerificationService> logger)
    : IHecIntegrationVerificationService
{
    public const string HttpClientName = "HecIntegrationVerificationHttpClient";

    public async Task<HecVerificationResult> VerifyAsync(HecIntegration integration, Guid organizationId)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient(HttpClientName);

            var payload = JsonSerializer.Serialize(new
            {
                source = "bitwarden",
                organizationId,
                date = DateTime.UtcNow,
            });

            var request = new HttpRequestMessage(HttpMethod.Post, integration.Uri);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                integration.Scheme,
                integration.Token);

            logger.LogInformation("Verifying HEC integration for organization {OrganizationId}.", organizationId);
            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("HEC integration verification succeeded for organization {OrganizationId}.", organizationId);
                return new HecVerificationResult(true, null);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                var authFailure = "Authentication failed: invalid token or unauthorized.";
                logger.LogWarning("HEC integration verification failed for organization {OrganizationId}: {Reason}",
                    organizationId, authFailure);
                return new HecVerificationResult(false, authFailure);
            }

            // Generic error — can be expanded to map specific HEC status/response codes
            // to more actionable failure reasons (e.g. index policy rejections).
            var errorReason = $"Endpoint returned an error: {(int)response.StatusCode} {response.ReasonPhrase}.";
            logger.LogWarning("HEC integration verification failed for organization {OrganizationId}: {Reason}",
                organizationId, errorReason);
            return new HecVerificationResult(false, errorReason);
        }
        catch (HttpRequestException ex)
        {
            var unreachableReason = $"Endpoint is unreachable: {ex.Message}.";
            logger.LogWarning(ex, "HEC integration verification failed for organization {OrganizationId}: {Reason}",
                organizationId, unreachableReason);
            return new HecVerificationResult(false, unreachableReason);
        }
    }
}
