using System.Data;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Billing.Repositories;

public class OrganizationInstallationRepository(
    GlobalSettings globalSettings) : Repository<OrganizationInstallation, Guid>(
        globalSettings.SqlServer.ConnectionString,
        globalSettings.SqlServer.ReadOnlyConnectionString), IOrganizationInstallationRepository
{
    public async Task<OrganizationInstallation> GetByInstallationIdAsync(Guid installationId)
    {
        var sqlConnection = new SqlConnection(ConnectionString);

        var results = await sqlConnection.QueryAsync<OrganizationInstallation>(
            "[dbo].[OrganizationInstallation_ReadByInstallationId]",
            new { InstallationId = installationId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }

    public async Task<ICollection<OrganizationInstallation>> GetByOrganizationIdAsync(Guid organizationId)
    {
        var sqlConnection = new SqlConnection(ConnectionString);

        var results = await sqlConnection.QueryAsync<OrganizationInstallation>(
            "[dbo].[OrganizationInstallation_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.ToArray();
    }
}
