using System.Data;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class CipherReportExclusionRepository : BaseRepository, ICipherReportExclusionRepository
{
    public CipherReportExclusionRepository(GlobalSettings globalSettings)
        : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public async Task<int> ExcludeAsync(IEnumerable<Guid> cipherIds, Guid userId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "[dbo].[Cipher_ExcludeFromReports]",
            new { Ids = cipherIds.ToGuidIdArrayTVP(), UserId = userId },
            commandType: CommandType.StoredProcedure);
    }
}
