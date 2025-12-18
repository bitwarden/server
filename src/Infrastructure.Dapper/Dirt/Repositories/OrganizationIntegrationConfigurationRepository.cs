using System.Data;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt.Repositories;

public class OrganizationIntegrationConfigurationRepository : Repository<OrganizationIntegrationConfiguration, Guid>, IOrganizationIntegrationConfigurationRepository
{
    public OrganizationIntegrationConfigurationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationIntegrationConfigurationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<List<OrganizationIntegrationConfigurationDetails>>
        GetManyByEventTypeOrganizationIdIntegrationType(EventType eventType, Guid organizationId,
            IntegrationType integrationType)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationIntegrationConfigurationDetails>(
                "[dbo].[OrganizationIntegrationConfigurationDetails_ReadManyByEventTypeOrganizationIdIntegrationType]",
                new
                {
                    EventType = eventType,
                    OrganizationId = organizationId,
                    IntegrationType = integrationType
                },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<List<OrganizationIntegrationConfigurationDetails>> GetAllConfigurationDetailsAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationIntegrationConfigurationDetails>(
                "[dbo].[OrganizationIntegrationConfigurationDetails_ReadMany]",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<List<OrganizationIntegrationConfiguration>> GetManyByIntegrationAsync(Guid organizationIntegrationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationIntegrationConfiguration>(
                "[dbo].[OrganizationIntegrationConfiguration_ReadManyByOrganizationIntegrationId]",
                new
                {
                    OrganizationIntegrationId = organizationIntegrationId
                },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
