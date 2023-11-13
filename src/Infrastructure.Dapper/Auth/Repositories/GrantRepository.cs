using System.Data;
using Bit.Core.Auth.Entities;
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
        // This method can be left empty if it's not used in this class
    }
    public async Task SaveAsync(Grant obj, bool grantSaveOptimizationIsEnabled)
    {
        bool grantSaveOptimization = grantSaveOptimizationIsEnabled;
        using (var connection = new SqlConnection(ConnectionString))
        {
            if (!grantSaveOptimization)
            {
                var results = await connection.ExecuteAsync(
                "[dbo].[Grant_Save]",
                obj,
                commandType: CommandType.StoredProcedure);

            }
            else
            {
                var results = await connection.ExecuteAsync(
                "[dbo].[Grant_Save_withInsertUpdate]",
                obj,
                commandType: CommandType.StoredProcedure);
            }
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
