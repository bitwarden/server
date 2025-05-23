using System.Data;
using Bit.Core.Dirt.Reports.Entities;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class RiskInsightCriticalApplicationRepository : Repository<RiskInsightCriticalApplication, Guid>, IRiskInsightCriticalApplicationRepository
{
    public RiskInsightCriticalApplicationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public RiskInsightCriticalApplicationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    public async Task<ICollection<RiskInsightCriticalApplication>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<RiskInsightCriticalApplication>(
                $"[{Schema}].[RiskInsightCriticalApplication_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
