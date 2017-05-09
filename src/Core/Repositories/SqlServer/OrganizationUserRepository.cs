using System;
using Bit.Core.Models.Table;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;
using System.Linq;
using Bit.Core.Models.Data;
using System.Collections.Generic;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Repositories.SqlServer
{
    public class OrganizationUserRepository : Repository<OrganizationUser, Guid>, IOrganizationUserRepository
    {
        public OrganizationUserRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public OrganizationUserRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteScalarAsync<int>(
                    "[dbo].[OrganizationUser_ReadCountByOrganizationId]",
                    new { OrganizationId = organizationId },
                    commandType: CommandType.StoredProcedure);

                return results;
            }
        }

        public async Task<int> GetCountByFreeOrganizationAdminUserAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteScalarAsync<int>(
                    "[dbo].[OrganizationUser_ReadCountByFreeOrganizationAdminUser]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results;
            }
        }

        public async Task<int> GetCountByOrganizationOwnerUserAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteScalarAsync<int>(
                    "[dbo].[OrganizationUser_ReadCountByOrganizationOwnerUser]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results;
            }
        }

        public async Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, string email)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationUser>(
                    "[dbo].[OrganizationUser_ReadByOrganizationIdEmail]",
                    new { OrganizationId = organizationId, Email = email },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }

        public async Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationUser>(
                    "[dbo].[OrganizationUser_ReadByOrganizationIdUserId]",
                    new { OrganizationId = organizationId, UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }

        public async Task<ICollection<OrganizationUser>> GetManyByUserAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationUser>(
                    "[dbo].[OrganizationUser_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<OrganizationUser>> GetManyByOrganizationAsync(Guid organizationId,
            OrganizationUserType? type)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationUser>(
                    "[dbo].[OrganizationUser_ReadByOrganizationId]",
                    new { OrganizationId = organizationId, Type = type },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<Tuple<OrganizationUserUserDetails, ICollection<CollectionUserCollectionDetails>>>
            GetDetailsByIdAsync(Guid id)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryMultipleAsync(
                    "[dbo].[OrganizationUserUserDetails_ReadById]",
                    new { Id = id },
                    commandType: CommandType.StoredProcedure);

                var user = (await results.ReadAsync<OrganizationUserUserDetails>()).SingleOrDefault();
                var collections = (await results.ReadAsync<CollectionUserCollectionDetails>()).ToList();
                return new Tuple<OrganizationUserUserDetails, ICollection<CollectionUserCollectionDetails>>(user, collections);
            }
        }

        public async Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync(Guid organizationId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationUserUserDetails>(
                    "[dbo].[OrganizationUserUserDetails_ReadByOrganizationId]",
                    new { OrganizationId = organizationId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<OrganizationUserOrganizationDetails>> GetManyDetailsByUserAsync(Guid userId,
            OrganizationUserStatusType? status = null)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationUserOrganizationDetails>(
                    "[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]",
                    new { UserId = userId, Status = status },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task UpdateGroupsAsync(Guid orgUserId, IEnumerable<Guid> groupIds)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    "[dbo].[GroupUser_UpdateGroups]",
                    new { OrganizationUserId = orgUserId, GroupIds = groupIds.ToGuidIdArrayTVP() },
                    commandType: CommandType.StoredProcedure);
            }
        }
    }
}
