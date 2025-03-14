using Bit.Core.PhishingDomainFeatures.Interfaces;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.PhishingDomainFeatures;

/// <summary>
/// Implementation of ICloudPhishingDomainQuery for self-hosted environments 
/// that relays the request to the Bitwarden cloud API
/// </summary>
public class CloudPhishingDomainRelayQuery : BaseIdentityClientService, ICloudPhishingDomainQuery
{
    private readonly IGlobalSettings _globalSettings;

    public CloudPhishingDomainRelayQuery(
        IHttpClientFactory httpFactory,
        IGlobalSettings globalSettings,
        ILogger<CloudPhishingDomainRelayQuery> logger)
        : base(
            httpFactory,
            globalSettings.Installation.ApiUri,
            globalSettings.Installation.IdentityUri,
            "api.installation",
            $"installation.{globalSettings.Installation.Id}",
            globalSettings.Installation.Key,
            logger)
    {
        _globalSettings = globalSettings;
    }

    public async Task<List<string>> GetPhishingDomainsAsync()
    {
        if (!_globalSettings.SelfHosted || !_globalSettings.EnableCloudCommunication)
        {
            throw new InvalidOperationException("This query is only for self-hosted installations with cloud communication enabled.");
        }

        var result = await SendAsync<object, string[]>(HttpMethod.Get, "phishing-domains", null, true);
        return result?.ToList() ?? new List<string>();
    }
} 