using System.Data;
using Bit.Core.Dirt.Reports.Entities;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class OrganizationApplicationRepository : Repository<OrganizationApplication, Guid>, IOrganizationApplicationRepository
{
    public OrganizationApplicationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public OrganizationApplicationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    public async Task<ICollection<OrganizationApplication>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationApplication>(
                $"[{Schema}].[RiskInsightCriticalApplication_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
