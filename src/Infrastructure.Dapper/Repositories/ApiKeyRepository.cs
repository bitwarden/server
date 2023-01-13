using System.Data;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
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

    public async Task<ApiKeyDetails> GetDetailsByIdAsync(Guid id)
    {
        using var connection = new SqlConnection(ConnectionString);
        // When adding different key details, we should change the QueryAsync type to match the database data,
        //  but cast it to the appropriate data model.
        var results = await connection.QueryAsync<ServiceAccountApiKeyDetails>(
            $"[{Schema}].[ApiKeyDetails_ReadById]",
            new { Id = id },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }

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
