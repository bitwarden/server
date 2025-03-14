using System.Data;
using System.Text.Json;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Bit.Infrastructure.Dapper.Repositories;

public class PhishingDomainRepository : IPhishingDomainRepository
{
    private readonly string _connectionString;
    private readonly IDistributedCache _cache;
    private readonly ILogger<PhishingDomainRepository> _logger;
    private const string _cacheKey = "PhishingDomains_v1";
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
        SlidingExpiration = TimeSpan.FromHours(1)
    };

    public PhishingDomainRepository(
        GlobalSettings globalSettings, 
        IDistributedCache cache,
        ILogger<PhishingDomainRepository> logger)
        : this(globalSettings.SqlServer.ConnectionString, cache, logger)
    { }

    public PhishingDomainRepository(
        string connectionString, 
        IDistributedCache cache,
        ILogger<PhishingDomainRepository> logger)
    {
        _connectionString = connectionString;
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

        using var connection = new SqlConnection(_connectionString);

        var results = await connection.QueryAsync<string>(
            "[dbo].[PhishingDomain_ReadAll]",
            commandType: CommandType.StoredProcedure);

        var domains = results.AsList();

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

    public async Task UpdatePhishingDomainsAsync(IEnumerable<string> domains)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[PhishingDomain_DeleteAll]",
                commandType: CommandType.StoredProcedure);

            foreach (var domain in domains)
            {
                await connection.ExecuteAsync(
                    "[dbo].[PhishingDomain_Create]",
                    new
                    {
                        Id = Guid.NewGuid(),
                        Domain = domain,
                        CreationDate = DateTime.UtcNow,
                        RevisionDate = DateTime.UtcNow
                    },
                    commandType: CommandType.StoredProcedure);
            }
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
