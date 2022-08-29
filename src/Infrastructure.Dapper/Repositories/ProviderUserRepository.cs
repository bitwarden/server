using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class ProviderUserRepository : Repository<ProviderUser, Guid>, IProviderUserRepository
{
    public ProviderUserRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public ProviderUserRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<int> GetCountByProviderAsync(Guid providerId, string email, bool onlyRegisteredUsers)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.ExecuteScalarAsync<int>(
                "[dbo].[ProviderUser_ReadCountByProviderIdEmail]",
                new { ProviderId = providerId, Email = email, OnlyUsers = onlyRegisteredUsers },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }

    public async Task<ICollection<ProviderUser>> GetManyAsync(IEnumerable<Guid> ids)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderUser>(
                "[dbo].[ProviderUser_ReadByIds]",
                new { Ids = ids.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<ProviderUser>> GetManyByUserAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderUser>(
                "[dbo].[ProviderUser_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ProviderUser> GetByProviderUserAsync(Guid providerId, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderUser>(
                "[dbo].[ProviderUser_ReadByProviderIdUserId]",
                new { ProviderId = providerId, UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<ICollection<ProviderUser>> GetManyByProviderAsync(Guid providerId, ProviderUserType? type)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderUser>(
                "[dbo].[ProviderUser_ReadByProviderId]",
                new { ProviderId = providerId, Type = type },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<ProviderUserUserDetails>> GetManyDetailsByProviderAsync(Guid providerId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderUserUserDetails>(
                "[dbo].[ProviderUserUserDetails_ReadByProviderId]",
                new { ProviderId = providerId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<ProviderUserProviderDetails>> GetManyDetailsByUserAsync(Guid userId,
        ProviderUserStatusType? status = null)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderUserProviderDetails>(
                "[dbo].[ProviderUserProviderDetails_ReadByUserIdStatus]",
                new { UserId = userId, Status = status },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<IEnumerable<ProviderUserOrganizationDetails>> GetManyOrganizationDetailsByUserAsync(Guid userId,
        ProviderUserStatusType? status = null)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderUserOrganizationDetails>(
                "[dbo].[ProviderUserProviderOrganizationDetails_ReadByUserIdStatus]",
                new { UserId = userId, Status = status },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> providerUserIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync("[dbo].[ProviderUser_DeleteByIds]",
                new { Ids = providerUserIds.ToGuidIdArrayTVP() }, commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<IEnumerable<ProviderUserPublicKey>> GetManyPublicKeysByProviderUserAsync(
        Guid providerId, IEnumerable<Guid> Ids)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderUserPublicKey>(
                "[dbo].[User_ReadPublicKeysByProviderUserIds]",
                new { ProviderId = providerId, ProviderUserIds = Ids.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<int> GetCountByOnlyOwnerAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteScalarAsync<int>(
                "[dbo].[ProviderUser_ReadCountByOnlyOwner]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results;
        }
    }
}
