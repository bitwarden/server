using Bit.Core.Utilities;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Infrastructure.Dapper.Repositories;

public abstract class BaseRepository<T>
    where T : class
{
    private readonly IDistributedCache _cache;

    static BaseRepository()
    {
        SqlMapper.AddTypeHandler(new DateTimeHandler());
    }

    public BaseRepository(
        string connectionString,
        string readOnlyConnectionString)
        : this(null, connectionString, readOnlyConnectionString)
    {
    }

    public BaseRepository(
        IDistributedCache cache,
        string connectionString,
        string readOnlyConnectionString)
    {
        _cache = cache;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(readOnlyConnectionString))
        {
            throw new ArgumentNullException(nameof(readOnlyConnectionString));
        }

        ConnectionString = connectionString;
        ReadOnlyConnectionString = readOnlyConnectionString;
    }

    protected string ConnectionString { get; private set; }
    protected string ReadOnlyConnectionString { get; private set; }

    /// <summary>
    /// Provides read-through caching of a single data object.
    /// </summary>
    /// <typeparam name="T">The type of object to read-through cache.</typeparam>
    /// <param name="objectKey">The object key. This should typically be the name of a field or fields that uniquely identify the query.</param>
    /// <param name="instanceKey">The instance key. This should typically be the value that uniquely varies each instance by the object key.</param>
    /// <param name="dataAccessFunc">The function to access the data from the data source, if needed.</param>
    /// <param name="entryOptions">The caching options e.g. TTL.</param>
    /// <returns>The data object, or default of T if the data could not be loaded.</returns>
    protected async Task<T> GetOrCreateThroughCacheAsync(
        string objectKey,
        string instanceKey,
        Func<Task<T>> dataAccessFunc,
        DistributedCacheEntryOptions entryOptions)
    {
        // return directly from the data store if cache is unavailable
        if (_cache == null) return await dataAccessFunc();

        var key = $"{typeof(T).Name}:{objectKey}:{instanceKey}";
        var result = await _cache.GetAsync(key, dataAccessFunc, entryOptions);

        return result.Result;
    }

    /// <summary>
    /// Provides write-through caching for deleting an object.
    /// </summary>
    /// <param name="objectAndInstanceKeys">The pairs of object keys and instance keys.</param>
    protected async Task WriteThroughCacheDeleteAsync(IEnumerable<KeyValuePair<string, string>> objectAndInstanceKeys)
    {
        if (_cache == null) return;

        foreach (var key in objectAndInstanceKeys)
        {
            await _cache.RemoveAsync($"{typeof(T).Name}:{key.Key}:{key.Value}");
        }
    }
}
