using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class ApiKeyRepository : Repository<ApiKey, Guid>, IApiKeyRepository
{
    public ApiKeyRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public ApiKeyRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<ApiKey>> GetManyByServiceAccountIdAsync(Guid serviceAccountId)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<ApiKey>(
            $"[{Schema}].[ApiKey_ReadByServiceAccountId]",
            new { ServiceAccountId = serviceAccountId },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }
}
