﻿using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Auth.Repositories;

public class SsoConfigRepository : Repository<SsoConfig, long>, ISsoConfigRepository
{
    public SsoConfigRepository(GlobalSettings globalSettings)
        : this(
            globalSettings.SqlServer.ConnectionString,
            globalSettings.SqlServer.ReadOnlyConnectionString
        ) { }

    public SsoConfigRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString) { }

    public async Task<SsoConfig?> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<SsoConfig>(
                $"[{Schema}].[{Table}_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure
            );

            return results.SingleOrDefault();
        }
    }

    public async Task<SsoConfig?> GetByIdentifierAsync(string identifier)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<SsoConfig>(
                $"[{Schema}].[{Table}_ReadByIdentifier]",
                new { Identifier = identifier },
                commandType: CommandType.StoredProcedure
            );

            return results.SingleOrDefault();
        }
    }

    public async Task<ICollection<SsoConfig>> GetManyByRevisionNotBeforeDate(DateTime? notBefore)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<SsoConfig>(
                $"[{Schema}].[{Table}_ReadManyByNotBeforeRevisionDate]",
                new { NotBefore = notBefore },
                commandType: CommandType.StoredProcedure
            );

            return results.ToList();
        }
    }
}
