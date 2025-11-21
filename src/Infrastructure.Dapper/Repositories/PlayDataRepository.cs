using System.Data;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Repositories;

public class PlayDataRepository : Repository<PlayData, Guid>, IPlayDataRepository
{
    public PlayDataRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public PlayDataRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<PlayData>> GetByPlayIdAsync(string playId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<PlayData>(
                "[dbo].[PlayData_ReadByPlayId]",
                new { PlayId = playId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task DeleteByPlayIdAsync(string playId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[PlayData_DeleteByPlayId]",
                new { PlayId = playId },
                commandType: CommandType.StoredProcedure);
        }
    }
}
