using System.Data;
using System.Text.Json;
using Bit.Core.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
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
        var domainsList = domains.ToList();
        _logger.LogInformation("Beginning bulk update of {Count} phishing domains", domainsList.Count);

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var connection = dbContext.Database.GetDbConnection();
        var connectionString = connection.ConnectionString;

        using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync();

        using var transaction = sqlConnection.BeginTransaction();
        try
        {
            using var command = sqlConnection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "[dbo].[PhishingDomain_DeleteAll]";
            command.CommandType = CommandType.StoredProcedure;
            await command.ExecuteNonQueryAsync();

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

            using var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, transaction)
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
