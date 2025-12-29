using System.Data;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationIntegrationRepository : Repository<OrganizationIntegration, Guid>, IOrganizationIntegrationRepository
{
    public OrganizationIntegrationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationIntegrationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<List<OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationIntegration>(
                "[dbo].[OrganizationIntegration_ReadManyByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<OrganizationIntegration?> GetByTeamsConfigurationTenantIdTeamId(string tenantId, string teamId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.QuerySingleOrDefaultAsync<OrganizationIntegration>(
                "[dbo].[OrganizationIntegration_ReadByTeamsConfigurationTenantIdTeamId]",
                new { TenantId = tenantId, TeamId = teamId },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }
}
