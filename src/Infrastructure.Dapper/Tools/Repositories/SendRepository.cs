using System.Data;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Tools.Repositories;

public class SendRepository : Repository<Send, Guid>, ISendRepository
{
    public SendRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public SendRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<Send>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Send>(
                $"[{Schema}].[Send_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Send>(
                $"[{Schema}].[Send_ReadByDeletionDateBefore]",
                new { DeletionDate = deletionDateBefore },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
