using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class PhishingDomainRepository : IPhishingDomainRepository
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IDistributedCache _cache;
    private readonly ILogger<PhishingDomainRepository> _logger;
    private const string _cacheKey = "PhishingDomains_v1";
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
        SlidingExpiration = TimeSpan.FromHours(1)
    };

    public PhishingDomainRepository(
        IServiceScopeFactory serviceScopeFactory, 
        IDistributedCache cache,
        ILogger<PhishingDomainRepository> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ICollection<string>> GetActivePhishingDomainsAsync()
    {
        try
        {
            var cachedDomains = await _cache.GetStringAsync(_cacheKey);

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

        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var domains = await dbContext.PhishingDomains
                .Select(d => d.Domain)
                .ToListAsync();

            try
            {
                await _cache.SetStringAsync(
                    _cacheKey,
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
    }

    public async Task UpdatePhishingDomainsAsync(IEnumerable<string> domains)
    {
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            await dbContext.PhishingDomains.ExecuteDeleteAsync();

            var phishingDomains = domains.Select(d => new PhishingDomain
            {
                Id = Guid.NewGuid(),
                Domain = d,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            });
            await dbContext.PhishingDomains.AddRangeAsync(phishingDomains);
            await dbContext.SaveChangesAsync();
        }

        try
        {
            await _cache.SetStringAsync(
                _cacheKey,
                JsonSerializer.Serialize(domains),
                _cacheOptions);
            _logger.LogDebug("Updated phishing domains cache after update operation");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update phishing domains in cache");
        }
    }
}
