using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class EmergencyAccessRepository : Repository<EmergencyAccess, Guid>, IEmergencyAccessRepository
{
    public EmergencyAccessRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public EmergencyAccessRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<int> GetCountByGrantorIdEmailAsync(Guid grantorId, string email, bool onlyRegisteredUsers)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteScalarAsync<int>(
                "[dbo].[EmergencyAccess_ReadCountByGrantorIdEmail]",
                new { GrantorId = grantorId, Email = email, OnlyUsers = onlyRegisteredUsers },
                commandType: CommandType.StoredProcedure);

            return results;
        }
    }

    public async Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGrantorIdAsync(Guid grantorId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<EmergencyAccessDetails>(
                "[dbo].[EmergencyAccessDetails_ReadByGrantorId]",
                new { GrantorId = grantorId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGranteeIdAsync(Guid granteeId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<EmergencyAccessDetails>(
                "[dbo].[EmergencyAccessDetails_ReadByGranteeId]",
                new { GranteeId = granteeId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<EmergencyAccessDetails> GetDetailsByIdGrantorIdAsync(Guid id, Guid grantorId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<EmergencyAccessDetails>(
                "[dbo].[EmergencyAccessDetails_ReadByIdGrantorId]",
                new { Id = id, GrantorId = grantorId },
                commandType: CommandType.StoredProcedure);

            return results.FirstOrDefault();
        }
    }

    public async Task<ICollection<EmergencyAccessNotify>> GetManyToNotifyAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<EmergencyAccessNotify>(
                "[dbo].[EmergencyAccess_ReadToNotify]",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<EmergencyAccessDetails>> GetExpiredRecoveriesAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<EmergencyAccessDetails>(
                "[dbo].[EmergencyAccessDetails_ReadExpiredRecoveries]",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
