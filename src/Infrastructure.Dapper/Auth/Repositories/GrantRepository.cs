using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Infrastructure.Dapper.Auth.Repositories;

public class GrantRepository : BaseRepository<Grant>, IGrantRepository
{
    public GrantRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public GrantRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    public async Task<Grant> GetByKeyAsync(string key)
    {
        return await GetOrCreateThroughCacheAsync("Key", key, async () =>
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Grant>(
                    "[dbo].[Grant_ReadByKey]",
                    new { Key = key },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
    }

    public async Task<ICollection<Grant>> GetManyAsync(string subjectId, string sessionId,
        string clientId, string type)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Grant>(
                "[dbo].[Grant_Read]",
                new { SubjectId = subjectId, SessionId = sessionId, ClientId = clientId, Type = type },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task SaveAsync(Grant obj)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                "[dbo].[Grant_Save]",
                obj,
                commandType: CommandType.StoredProcedure);
        }

        await DeleteCacheAsync(obj);
    }

    public async Task DeleteByKeyAsync(string key)
    {
        var obj = await GetByKeyAsync(key);
        if (obj == null) return;

        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[Grant_DeleteByKey]",
                new { Key = key },
                commandType: CommandType.StoredProcedure);
        }

        await DeleteCacheAsync(obj);
    }

    public async Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[Grant_Delete]",
                new { SubjectId = subjectId, SessionId = sessionId, ClientId = clientId, Type = type },
                commandType: CommandType.StoredProcedure);
        }
    }

    private async Task DeleteCacheAsync(Grant obj)
    {
        await WriteThroughCacheDeleteAsync(new[] { new KeyValuePair<string, string>("Key", obj.Key) });
    }
}
