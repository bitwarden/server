using System.Data;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class CipherBrowserHealthExclusionRepository : BaseRepository, ICipherBrowserHealthExclusionRepository
{
    public CipherBrowserHealthExclusionRepository(GlobalSettings globalSettings)
        : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public async Task<int> ExcludeAsync(IEnumerable<Guid> cipherIds, Guid userId)
    {
        var ids = cipherIds.ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "[dbo].[Cipher_UpdateManyBrowserHealthExclusions]",
            new { Ids = ids.ToGuidIdArrayTVP(), UserId = userId },
            commandType: CommandType.StoredProcedure);
    }
}
