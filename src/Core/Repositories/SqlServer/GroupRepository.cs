using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Newtonsoft.Json;
using Bit.Core.Utilities;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.SqlServer
{
    public class GroupRepository : Repository<Group, Guid>, IGroupRepository
    {
        public GroupRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public GroupRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public async Task<Tuple<Group, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryMultipleAsync(
                    $"[{Schema}].[Group_ReadWithCollectionsById]",
                    new { Id = id },
                    commandType: CommandType.StoredProcedure);

                var group = await results.ReadFirstOrDefaultAsync<Group>();
                var colletions = (await results.ReadAsync<SelectionReadOnly>()).ToList();

                return new Tuple<Group, ICollection<SelectionReadOnly>>(group, colletions);
            }
        }

        public async Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Group>(
                    $"[{Schema}].[Group_ReadByOrganizationId]",
                    new { OrganizationId = organizationId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<Guid>> GetManyIdsByUserIdAsync(Guid organizationUserId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Guid>(
                    $"[{Schema}].[GroupUser_ReadGroupIdsByOrganizationUserId]",
                    new { OrganizationUserId = organizationUserId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<Guid>> GetManyUserIdsByIdAsync(Guid id)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Guid>(
                    $"[{Schema}].[GroupUser_ReadOrganizationUserIdsByGroupId]",
                    new { GroupId = id },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<GroupUser>> GetManyGroupUsersByOrganizationIdAsync(Guid organizationId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<GroupUser>(
                    $"[{Schema}].[GroupUser_ReadByOrganizationId]",
                    new { OrganizationId = organizationId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task CreateAsync(Group obj, IEnumerable<SelectionReadOnly> collections)
        {
            obj.SetNewId();
            var objWithCollections = JsonConvert.DeserializeObject<GroupWithCollections>(JsonConvert.SerializeObject(obj));
            objWithCollections.Collections = collections.ToArrayTVP();

            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[Group_CreateWithCollections]",
                    objWithCollections,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task ReplaceAsync(Group obj, IEnumerable<SelectionReadOnly> collections)
        {
            var objWithCollections = JsonConvert.DeserializeObject<GroupWithCollections>(JsonConvert.SerializeObject(obj));
            objWithCollections.Collections = collections.ToArrayTVP();

            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[Group_UpdateWithCollections]",
                    objWithCollections,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task DeleteUserAsync(Guid groupId, Guid organizationUserId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[GroupUser_Delete]",
                    new { GroupId = groupId, OrganizationUserId = organizationUserId  },
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task UpdateUsersAsync(Guid groupId, IEnumerable<Guid> organizationUserIds)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    "[dbo].[GroupUser_UpdateUsers]",
                    new { GroupId = groupId, OrganizationUserIds = organizationUserIds.ToGuidIdArrayTVP() },
                    commandType: CommandType.StoredProcedure);
            }
        }

        public class GroupWithCollections : Group
        {
            public DataTable Collections { get; set; }
        }
    }
}
