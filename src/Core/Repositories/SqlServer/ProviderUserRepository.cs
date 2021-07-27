using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Core.Repositories.SqlServer
{
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
        
<<<<<<< HEAD
=======
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

>>>>>>> 545d5f942b1a2d210c9488c669d700d01d2c1aeb
        public async Task DeleteManyAsync(IEnumerable<Guid> providerUserIds)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync("[dbo].[ProviderUser_DeleteByIds]",
                    new { Ids = providerUserIds.ToGuidIdArrayTVP() }, commandType: CommandType.StoredProcedure);
            }
        }
    }
}
