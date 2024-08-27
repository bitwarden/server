using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Auth.Repositories;

public class GrantRepository : BaseRepository, IGrantRepository
{
    public GrantRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public GrantRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<IGrant?> GetByKeyAsync(string key)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Grant>(
                "[dbo].[Grant_ReadByKey]",
                new { Key = key },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<ICollection<IGrant>> GetManyAsync(string subjectId, string sessionId,
        string clientId, string type)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Grant>(
                "[dbo].[Grant_Read]",
                new { SubjectId = subjectId, SessionId = sessionId, ClientId = clientId, Type = type },
                commandType: CommandType.StoredProcedure);

            return results.ToList<IGrant>();
        }
    }

    public async Task SaveAsync(IGrant obj)
    {
        if (obj is not Grant gObj)
        {
            throw new ArgumentException(null, nameof(obj));
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                "[dbo].[Grant_Save]",
                new
                {
                    obj.Key,
                    obj.Type,
                    obj.SubjectId,
                    obj.SessionId,
                    obj.ClientId,
                    obj.Description,
                    obj.CreationDate,
                    obj.ExpirationDate,
                    obj.ConsumedDate,
                    obj.Data
                },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task DeleteByKeyAsync(string key)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[Grant_DeleteByKey]",
                new { Key = key },
                commandType: CommandType.StoredProcedure);
        }
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
}
