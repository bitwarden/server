using System.Data;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt.Repositories;

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

    public async Task<bool> DisableAsync(
        Guid organizationId,
        IntegrationType integrationType,
        DateTime disabledDate,
        IntegrationFailureCategory disabledReason)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var rowsAffected = await connection.ExecuteAsync(
                "[dbo].[OrganizationIntegration_Disable]",
                new
                {
                    OrganizationId = organizationId,
                    Type = integrationType,
                    DisabledDate = disabledDate,
                    DisabledReason = disabledReason,
                    RevisionDate = disabledDate
                },
                commandType: CommandType.StoredProcedure);

            return rowsAffected > 0;
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
