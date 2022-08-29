using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class SsoUserRepository : Repository<SsoUser, long>, ISsoUserRepository
{
    public SsoUserRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public SsoUserRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task DeleteAsync(Guid userId, Guid? organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[SsoUser_Delete]",
                new { UserId = userId, OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<SsoUser> GetByUserIdOrganizationIdAsync(Guid organizationId, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<SsoUser>(
                $"[{Schema}].[SsoUser_ReadByUserIdOrganizationId]",
                new { UserId = userId, OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }
}
