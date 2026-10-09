using System.Data;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.AdminConsole.Repositories;

public class OrganizationScopedApiKeyRepository
    : Repository<OrganizationScopedApiKey, Guid>, IOrganizationScopedApiKeyRepository
{
    public OrganizationScopedApiKeyRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationScopedApiKeyRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<OrganizationScopedApiKey>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationScopedApiKey>(
            $"[{Schema}].[{Table}_ReadManyByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);
        return results.ToList();
    }
}
