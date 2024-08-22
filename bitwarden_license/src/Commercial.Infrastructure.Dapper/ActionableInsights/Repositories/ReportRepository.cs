using System.Data;
using Bit.Core.ActionableInsights.Entities;
using Bit.Core.ActionableInsights.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Commercial.Infrastructure.Dapper.ActionableInsights.Repositories;

public class ReportRepository(string connectionString, string readOnlyConnectionString)
    : Repository<Report, Guid>(connectionString, readOnlyConnectionString), IReportRepository
{
    public ReportRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }


    public async Task<Report?> GetByOrganizationIdAsync(Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<Report>(
            $"[{Schema}].[Report_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }
}
