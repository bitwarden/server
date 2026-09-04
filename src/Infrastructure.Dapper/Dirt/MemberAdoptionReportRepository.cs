using System.Data;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class MemberAdoptionReportRepository : BaseRepository, IMemberAdoptionReportRepository
{
    public MemberAdoptionReportRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public MemberAdoptionReportRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    public async Task<IReadOnlyList<MemberAdoptionReportDetail>> GetMemberAdoptionDetailsByOrganizationIdAsync(
        Guid organizationId)
    {
        await using var connection = new SqlConnection(ReadOnlyConnectionString);

        var results = await connection.QueryAsync<MemberAdoptionReportDetail>(
            "[dbo].[MemberAdoptionReport_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure,
            commandTimeout: 120);

        return results.AsList();
    }
}
