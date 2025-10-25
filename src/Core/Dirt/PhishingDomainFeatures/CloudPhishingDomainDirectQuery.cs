using Bit.Core.Dirt.PhishingDomainFeatures.Interfaces;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.PhishingDomainFeatures;

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

    /// <summary>
    /// Gets the SHA256 checksum of the remote phishing domains list
    /// </summary>
    /// <returns>The SHA256 checksum as a lowercase hex string</returns>
    public async Task<string> GetRemoteChecksumAsync()
    {
        if (string.IsNullOrWhiteSpace(_globalSettings.PhishingDomain?.ChecksumUrl))
        {
            _logger.LogWarning("Phishing domain checksum URL is not configured.");
            return string.Empty;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("PhishingDomains");
            var response = await httpClient.GetAsync(_globalSettings.PhishingDomain.ChecksumUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return ParseChecksumResponse(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving phishing domain checksum from {Url}",
                _globalSettings.PhishingDomain.ChecksumUrl);
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses a checksum response in the format "hash *filename"
    /// </summary>
    private static string ParseChecksumResponse(string checksumContent)
    {
        if (string.IsNullOrWhiteSpace(checksumContent))
        {
            return string.Empty;
        }

        // Format is typically "hash *filename"
        var parts = checksumContent.Split(' ', 2);

        return parts.Length > 0 ? parts[0].Trim() : string.Empty;
    }

    private static List<string> ParseDomains(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return content
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .ToList();
    }
}
