using Bit.Core.PhishingDomainFeatures.Interfaces;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.PhishingDomainFeatures;

/// <summary>
/// Implementation of ICloudPhishingDomainQuery for cloud environments
/// that directly calls the external phishing domain source
/// </summary>
public class CloudPhishingDomainDirectQuery : ICloudPhishingDomainQuery
{
    private readonly IGlobalSettings _globalSettings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CloudPhishingDomainDirectQuery> _logger;

    public CloudPhishingDomainDirectQuery(
        IGlobalSettings globalSettings,
        IHttpClientFactory httpClientFactory,
        ILogger<CloudPhishingDomainDirectQuery> logger)
    {
        _globalSettings = globalSettings;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<string>> GetPhishingDomainsAsync()
    {
        if (string.IsNullOrWhiteSpace(_globalSettings.PhishingDomain?.UpdateUrl))
        {
            throw new InvalidOperationException("Phishing domain update URL is not configured.");
        }

        var httpClient = _httpClientFactory.CreateClient("PhishingDomains");
        var response = await httpClient.GetAsync(_globalSettings.PhishingDomain.UpdateUrl);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return ParseDomains(content);
    }

    private static List<string> ParseDomains(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .ToList();
    }
} 