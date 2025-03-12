using System.Data;
using System.Text.Json;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Infrastructure.Dapper.Repositories;

public class PhishingDomainRepository : IPhishingDomainRepository
{
    private readonly string _connectionString;
    private readonly IDistributedCache _cache;
    private const string _cacheKey = "PhishingDomains";
    private static readonly DistributedCacheEntryOptions _cacheOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) // Cache for 24 hours
    };

    public PhishingDomainRepository(GlobalSettings globalSettings, IDistributedCache cache)
        : this(globalSettings.SqlServer.ConnectionString, cache)
    { }

    public PhishingDomainRepository(string connectionString, IDistributedCache cache)
    {
        _connectionString = connectionString;
        _cache = cache;
    }

    public async Task<ICollection<string>> GetActivePhishingDomainsAsync()
    {
        // Try to get from cache first
        var cachedDomains = await _cache.GetStringAsync(_cacheKey);
        if (!string.IsNullOrEmpty(cachedDomains))
        {
            return JsonSerializer.Deserialize<ICollection<string>>(cachedDomains) ?? new List<string>();
        }

        // If not in cache, get from database
        using (var connection = new SqlConnection(_connectionString))
        {
            var results = await connection.QueryAsync<string>(
                "[dbo].[PhishingDomain_ReadAll]",
                commandType: CommandType.StoredProcedure);

            var domains = results.AsList();

            // Store in cache
            await _cache.SetStringAsync(
                _cacheKey,
                JsonSerializer.Serialize(domains),
                _cacheOptions);

            return domains;
        }
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

        // Update cache with new domains
        await _cache.SetStringAsync(
            _cacheKey,
            JsonSerializer.Serialize(domains),
            _cacheOptions);
    }
}
