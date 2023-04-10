using System.Net;
using Amazon;
using Amazon.SimpleEmail;
using Bit.Core.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bit.Api.Health;

public class AmazonSesHealthCheck : IHealthCheck
{
    private readonly IAmazonSimpleEmailService _client;

    public AmazonSesHealthCheck(GlobalSettings globalSettings)
        : this(globalSettings, new AmazonSimpleEmailServiceClient(
            globalSettings.Amazon.AccessKeyId,
            globalSettings.Amazon.AccessKeySecret,
            RegionEndpoint.GetBySystemName(globalSettings.Amazon.Region)
            ))
    { }

    public AmazonSesHealthCheck(GlobalSettings globalSettings, IAmazonSimpleEmailService amazonSimpleEmailService)
    {
        if (string.IsNullOrWhiteSpace(globalSettings.Amazon?.AccessKeyId))
        {
            throw new ArgumentNullException(nameof(globalSettings.Amazon.AccessKeyId));
        }
        if (string.IsNullOrWhiteSpace(globalSettings.Amazon?.AccessKeySecret))
        {
            throw new ArgumentNullException(nameof(globalSettings.Amazon.AccessKeySecret));
        }
        if (string.IsNullOrWhiteSpace(globalSettings.Amazon?.Region))
        {
            throw new ArgumentNullException(nameof(globalSettings.Amazon.Region));
        }

        _client = amazonSimpleEmailService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            var response = await _client.ListVerifiedEmailAddressesAsync(cancellationToken);
            return response.HttpStatusCode == HttpStatusCode.OK ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Amazon SES health check failed with status code: {response.HttpStatusCode}.");
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy($"Amazon SES health check failed with exception: {e.Message}.");
        }
    }
}
