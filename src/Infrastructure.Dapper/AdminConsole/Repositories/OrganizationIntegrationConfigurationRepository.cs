using System.Data;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.AdminConsole.Repositories;

public class OrganizationIntegrationConfigurationRepository : Repository<OrganizationIntegrationConfiguration, Guid>, IOrganizationIntegrationConfigurationRepository
{
    public OrganizationIntegrationConfigurationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationIntegrationConfigurationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<List<OrganizationIntegrationConfigurationDetails>> GetConfigurationDetailsAsync(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType)
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
}
