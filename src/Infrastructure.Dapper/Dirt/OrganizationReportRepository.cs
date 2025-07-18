// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Data;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class OrganizationReportRepository : Repository<OrganizationReport, Guid>, IOrganizationReportRepository
{
    public OrganizationReportRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public OrganizationReportRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    public async Task<ICollection<OrganizationReport>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationReport>(
                $"[{Schema}].[OrganizationReport_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<OrganizationReport> GetLatestByOrganizationIdAsync(Guid organizationId)
    {
        return await GetByOrganizationIdAsync(organizationId)
        .ContinueWith(task =>
        {
            var reports = task.Result;
            return reports.OrderByDescending(r => r.CreationDate).FirstOrDefault();
        });
    }
}
