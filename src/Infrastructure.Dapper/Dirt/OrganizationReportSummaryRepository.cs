#nullable disable

using System.Data;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class OrganizationReportSummaryRepository : Repository<OrganizationReportSummary, Guid>, IOrganizationReportSummaryRepository
{
    public OrganizationReportSummaryRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public OrganizationReportSummaryRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    public async Task<ICollection<OrganizationReportSummary>> GetByOrganizationReportIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationReportSummary>(
                $"[{Schema}].[OrganizationReportSummary_ReadByOrganizationReportId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
