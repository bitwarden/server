using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class AuthRequestRepository : Repository<AuthRequest, Guid>, IAuthRequestRepository
{
    public AuthRequestRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public AuthRequestRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<int> DeleteExpiredAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            return await connection.ExecuteAsync(
                $"[{Schema}].[AuthRequest_DeleteIfExpired]",
                null,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<ICollection<AuthRequest>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<AuthRequest>(
                $"[{Schema}].[AuthRequest_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
