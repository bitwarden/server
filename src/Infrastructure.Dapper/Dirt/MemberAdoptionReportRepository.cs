using System.Data;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class MemberAdoptionReportRepository : BaseRepository, IMemberAdoptionReportRepository
{
    /// <summary>
    /// Both reads stay well above the 30 second default. The detail read still runs per-member device and
    /// cipher lookups for every confirmed member, and the access graph read returns one row per member to
    /// collection edge plus one per collection to cipher edge, so a large organization spends real time
    /// streaming rows even though the aggregation itself no longer happens in SQL.
    /// </summary>
    private const int ReportCommandTimeoutSeconds = 120;

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
        var parameters = new { OrganizationId = organizationId };

        var details = (await connection.QueryAsync<MemberAdoptionReportDetail>(
            "[dbo].[MemberAdoptionReport_ReadByOrganizationId]",
            parameters,
            commandType: CommandType.StoredProcedure,
            commandTimeout: ReportCommandTimeoutSeconds)).AsList();

        using var accessGraph = await connection.QueryMultipleAsync(
            "[dbo].[MemberAdoptionReport_ReadAccessGraphByOrganizationId]",
            parameters,
            commandType: CommandType.StoredProcedure,
            commandTimeout: ReportCommandTimeoutSeconds);

        var access = (await accessGraph.ReadAsync<MemberCollectionAccess>()).AsList();
        var content = (await accessGraph.ReadAsync<CollectionCipherLink>()).AsList();

        var sharedItemCounts = SharedItemCountCalculator.Calculate(access, content);

        foreach (var detail in details)
        {
            detail.SharedItemCount = sharedItemCounts.GetValueOrDefault(detail.OrganizationUserId);
        }

        return details;
    }
}
