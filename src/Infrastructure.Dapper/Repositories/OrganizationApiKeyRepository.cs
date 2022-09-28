using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationApiKeyRepository : Repository<OrganizationApiKey, Guid>, IOrganizationApiKeyRepository
{
    public OrganizationApiKeyRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {

    }

    public OrganizationApiKeyRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<IEnumerable<OrganizationApiKey>> GetManyByOrganizationIdTypeAsync(Guid organizationId, OrganizationApiKeyType? type = null)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            return await connection.QueryAsync<OrganizationApiKey>(
                "[dbo].[OrganizationApikey_ReadManyByOrganizationIdType]",
                new
                {
                    OrganizationId = organizationId,
                    Type = type,
                },
                commandType: CommandType.StoredProcedure);
        }
    }
}
