﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Data;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.SecretsManager.Repositories;

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

    public async Task DeleteManyAsync(IEnumerable<ApiKey> objs)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.QueryAsync<ApiKey>(
            $"[{Schema}].[ApiKey_DeleteByIds]",
            new { Ids = objs.Select(obj => obj.Id).ToGuidIdArrayTVP() },
            commandType: CommandType.StoredProcedure);
    }
}
