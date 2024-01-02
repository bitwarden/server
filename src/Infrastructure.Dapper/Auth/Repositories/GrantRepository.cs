using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Auth.Repositories;

public class GrantRepository : BaseRepository, IGrantRepository
{
    public GrantRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public GrantRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<IGrant> GetByKeyAsync(string key)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<IGrant>(
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
            var results = await connection.QueryAsync<IGrant>(
                "[dbo].[Grant_Read]",
                new { SubjectId = subjectId, SessionId = sessionId, ClientId = clientId, Type = type },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task SaveAsync(IGrant obj)
    {
        if (!(obj is Grant gObj))
        {
            throw new ArgumentException(null, nameof(obj));
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                "[dbo].[Grant_Save]",
                new
                {
                    gObj.Key,
                    gObj.Type,
                    gObj.SubjectId,
                    gObj.SessionId,
                    gObj.ClientId,
                    gObj.Description,
                    gObj.CreationDate,
                    gObj.ExpirationDate,
                    gObj.ConsumedDate,
                    gObj.Data
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
