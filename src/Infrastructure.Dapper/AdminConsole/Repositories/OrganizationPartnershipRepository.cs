using System.Data;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.AdminConsole.Repositories;

public class OrganizationPartnershipRepository
    : Repository<OrganizationPartnership, Guid>, IOrganizationPartnershipRepository
{
    public OrganizationPartnershipRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationPartnershipRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<OrganizationPartnership?> GetByOrganizationIdAsync(Guid organizationId)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationPartnership>(
            $"[{Schema}].[{Table}_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);
        return results.SingleOrDefault();
    }
}
