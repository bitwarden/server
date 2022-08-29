using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class SsoConfigRepository : Repository<SsoConfig, long>, ISsoConfigRepository
{
    public SsoConfigRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public SsoConfigRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<SsoConfig> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<SsoConfig>(
                $"[{Schema}].[{Table}_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<SsoConfig> GetByIdentifierAsync(string identifier)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<SsoConfig>(
                $"[{Schema}].[{Table}_ReadByIdentifier]",
                new { Identifier = identifier },
                commandType: CommandType.StoredProcedure);

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
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
