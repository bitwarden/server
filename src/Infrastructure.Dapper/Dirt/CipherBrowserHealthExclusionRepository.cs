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
        return await UpdateExclusionsAsync(cipherIds, userId, true);
    }

    public async Task<int> UnexcludeAsync(IEnumerable<Guid> cipherIds, Guid userId)
    {
        return await UpdateExclusionsAsync(cipherIds, userId, false);
    }

    private async Task<int> UpdateExclusionsAsync(IEnumerable<Guid> cipherIds, Guid userId, bool exclude)
    {
        var ids = cipherIds.ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "[dbo].[Cipher_UpdateManyBrowserHealthExclusions]",
            new { Ids = ids.ToGuidIdArrayTVP(), UserId = userId, Exclude = exclude },
            commandType: CommandType.StoredProcedure);
    }
}
