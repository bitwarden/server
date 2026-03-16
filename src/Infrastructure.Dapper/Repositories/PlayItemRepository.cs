using System.Data;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Repositories;

public class PlayItemRepository : Repository<PlayItem, Guid>, IPlayItemRepository
{
    public PlayItemRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public PlayItemRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<PlayItem>> GetByPlayIdAsync(string playId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<PlayItem>(
                "[dbo].[PlayItem_ReadByPlayId]",
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
                "[dbo].[PlayItem_DeleteByPlayId]",
                new { PlayId = playId },
                commandType: CommandType.StoredProcedure);
        }
    }
}
