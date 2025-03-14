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
        var domainsList = domains.ToList();
        _logger.LogInformation("Beginning bulk update of {Count} phishing domains", domainsList.Count);

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(
                "[dbo].[PhishingDomain_DeleteAll]",
                transaction: transaction,
                commandType: CommandType.StoredProcedure);

            var dataTable = new DataTable();
            dataTable.Columns.Add("Id", typeof(Guid));
            dataTable.Columns.Add("Domain", typeof(string));
            dataTable.Columns.Add("CreationDate", typeof(DateTime));
            dataTable.Columns.Add("RevisionDate", typeof(DateTime));

            dataTable.PrimaryKey = [dataTable.Columns["Id"]];

            var now = DateTime.UtcNow;
            foreach (var domain in domainsList)
            {
                dataTable.Rows.Add(Guid.NewGuid(), domain, now, now);
            }

            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
            {
                DestinationTableName = "[dbo].[PhishingDomain]",
                BatchSize = 10000
            };

            bulkCopy.ColumnMappings.Add("Id", "Id");
            bulkCopy.ColumnMappings.Add("Domain", "Domain");
            bulkCopy.ColumnMappings.Add("CreationDate", "CreationDate");
            bulkCopy.ColumnMappings.Add("RevisionDate", "RevisionDate");

            await bulkCopy.WriteToServerAsync(dataTable);
            await transaction.CommitAsync();

            _logger.LogInformation("Successfully bulk updated {Count} phishing domains", domainsList.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to bulk update phishing domains");
            throw;
        }

        try
        {
            await _cache.SetStringAsync(
                _cacheKey,
                JsonSerializer.Serialize(domainsList),
                _cacheOptions);
            _logger.LogDebug("Updated phishing domains cache after update operation");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update phishing domains in cache");
        }
    }
}
