using System.Text.Json;
using Bit.Core.Dirt.PhishingDomainFeatures;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Repositories.Implementations;

public class AzurePhishingDomainRepository : IPhishingDomainRepository
{
    private readonly AzurePhishingDomainStorageService _storageService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AzurePhishingDomainRepository> _logger;
    private const string _domainsCacheKey = "PhishingDomains_v1";
    private const string _checksumCacheKey = "PhishingDomains_Checksum_v1";
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
        SlidingExpiration = TimeSpan.FromHours(1)
    };

    public AzurePhishingDomainRepository(
        AzurePhishingDomainStorageService storageService,
        IDistributedCache cache,
        ILogger<AzurePhishingDomainRepository> logger)
    {
        _storageService = storageService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ICollection<string>> GetActivePhishingDomainsAsync()
    {
        try
        {
            var cachedDomains = await _cache.GetStringAsync(_domainsCacheKey);
            if (!string.IsNullOrEmpty(cachedDomains))
            {
                _logger.LogDebug("Retrieved phishing domains from cache");
                return JsonSerializer.Deserialize<ICollection<string>>(cachedDomains) ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve phishing domains from cache");
        }

        var domains = await _storageService.GetDomainsAsync();

        try
        {
            await _cache.SetStringAsync(
                _domainsCacheKey,
                JsonSerializer.Serialize(domains),
                _cacheOptions);
            _logger.LogDebug("Stored {Count} phishing domains in cache", domains.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store phishing domains in cache");
        }

        return domains;
    }

    public async Task<string> GetCurrentChecksumAsync()
    {
        try
        {
            var cachedChecksum = await _cache.GetStringAsync(_checksumCacheKey);
            if (!string.IsNullOrEmpty(cachedChecksum))
            {
                _logger.LogDebug("Retrieved phishing domain checksum from cache");
                return cachedChecksum;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve phishing domain checksum from cache");
        }

        var checksum = await _storageService.GetChecksumAsync();

        try
        {
            if (!string.IsNullOrEmpty(checksum))
            {
                await _cache.SetStringAsync(
                    _checksumCacheKey,
                    checksum,
                    _cacheOptions);
                _logger.LogDebug("Stored phishing domain checksum in cache");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store phishing domain checksum in cache");
        }

        return checksum;
    }

    public async Task UpdatePhishingDomainsAsync(IEnumerable<string> domains, string checksum)
    {
        var domainsList = domains.ToList();
        await _storageService.UpdateDomainsAsync(domainsList, checksum);

        try
        {
            await _cache.SetStringAsync(
                _domainsCacheKey,
                JsonSerializer.Serialize(domainsList),
                _cacheOptions);

            await _cache.SetStringAsync(
                _checksumCacheKey,
                checksum,
                _cacheOptions);

            _logger.LogDebug("Updated phishing domains cache after update operation");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update phishing domains in cache");
        }
    }
}
