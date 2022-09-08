using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class GrantRepository : BaseRepository, IGrantRepository
{
    public GrantRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public GrantRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<Grant> GetByKeyAsync(string key)
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
