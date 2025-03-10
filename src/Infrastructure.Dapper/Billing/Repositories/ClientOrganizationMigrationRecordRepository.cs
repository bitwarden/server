using System.Data;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Billing.Repositories;

public class ClientOrganizationMigrationRecordRepository(
    GlobalSettings globalSettings) : Repository<ClientOrganizationMigrationRecord, Guid>(
        globalSettings.SqlServer.ConnectionString,
        globalSettings.SqlServer.ReadOnlyConnectionString), IClientOrganizationMigrationRecordRepository
{
    public async Task<ClientOrganizationMigrationRecord> GetByOrganizationId(Guid organizationId)
    {
        var sqlConnection = new SqlConnection(ConnectionString);

        var results = await sqlConnection.QueryAsync<ClientOrganizationMigrationRecord>(
            "[dbo].[ClientOrganizationMigrationRecord_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }

    public async Task<ICollection<ClientOrganizationMigrationRecord>> GetByProviderId(Guid providerId)
    {
        var sqlConnection = new SqlConnection(ConnectionString);

        var results = await sqlConnection.QueryAsync<ClientOrganizationMigrationRecord>(
            "[dbo].[ClientOrganizationMigrationRecord_ReadByProviderId]",
            new { ProviderId = providerId },
            commandType: CommandType.StoredProcedure);

        return results.ToArray();
    }
}
