using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Auth.Repositories;

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

    public async Task<ICollection<OrganizationAdminAuthRequest>> GetManyPendingByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationAdminAuthRequest>(
                $"[{Schema}].[AuthRequest_ReadPendingByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<OrganizationAdminAuthRequest>> GetManyAdminApprovalRequestsByManyIdsAsync(Guid organizationId, IEnumerable<Guid> ids)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationAdminAuthRequest>(
                $"[{Schema}].[AuthRequest_ReadAdminApprovalsByIds]",
                new { OrganizationId = organizationId, Ids = ids.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
