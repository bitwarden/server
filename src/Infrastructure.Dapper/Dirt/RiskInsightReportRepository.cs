using System.Data;
using Bit.Core.Dirt.Reports.Entities;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class RiskInsightReportRepository : Repository<RiskInsightReport, Guid>, IRiskInsightReportRepository
{
    public RiskInsightReportRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public RiskInsightReportRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    public async Task<ICollection<RiskInsightReport>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<RiskInsightReport>(
                $"[{Schema}].[RiskInsightReport_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
