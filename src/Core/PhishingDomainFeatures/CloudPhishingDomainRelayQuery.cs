// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
            "api.licensing",
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

    /// <summary>
    /// Gets the SHA256 checksum of the remote phishing domains list
    /// </summary>
    /// <returns>The SHA256 checksum as a lowercase hex string</returns>
    public async Task<string> GetRemoteChecksumAsync()
    {
        if (!_globalSettings.SelfHosted || !_globalSettings.EnableCloudCommunication)
        {
            throw new InvalidOperationException("This query is only for self-hosted installations with cloud communication enabled.");
        }

        try
        {
            // For self-hosted environments, we get the checksum from the Bitwarden cloud API
            var result = await SendAsync<object, string>(HttpMethod.Get, "phishing-domains/checksum", null, true);
            return result ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving phishing domain checksum from Bitwarden cloud API");
            return string.Empty;
        }
    }
}
