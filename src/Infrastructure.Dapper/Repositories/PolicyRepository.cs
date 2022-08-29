using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class PolicyRepository : Repository<Policy, Guid>, IPolicyRepository
{
    public PolicyRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public PolicyRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<Policy> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType type)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Policy>(
                $"[{Schema}].[{Table}_ReadByOrganizationIdType]",
                new { OrganizationId = organizationId, Type = (byte)type },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<ICollection<Policy>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Policy>(
                $"[{Schema}].[{Table}_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Policy>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Policy>(
                $"[{Schema}].[{Table}_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Policy>> GetManyByTypeApplicableToUserIdAsync(Guid userId, PolicyType policyType,
        OrganizationUserStatusType minStatus)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Policy>(
                $"[{Schema}].[{Table}_ReadByTypeApplicableToUser]",
                new { UserId = userId, PolicyType = policyType, MinimumStatus = minStatus },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<int> GetCountByTypeApplicableToUserIdAsync(Guid userId, PolicyType policyType,
        OrganizationUserStatusType minStatus)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.ExecuteScalarAsync<int>(
                $"[{Schema}].[{Table}_CountByTypeApplicableToUser]",
                new { UserId = userId, PolicyType = policyType, MinimumStatus = minStatus },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }
}
