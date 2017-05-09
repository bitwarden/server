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
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public GroupRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<Tuple<Group, ICollection<Guid>>> GetByIdWithCollectionsAsync(Guid id)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryMultipleAsync(
                    $"[{Schema}].[Group_ReadWithCollectionsById]",
                    new { Id = id },
                    commandType: CommandType.StoredProcedure);

                var group = await results.ReadFirstOrDefaultAsync<Group>();
                var colletionIds = (await results.ReadAsync<Guid>()).ToList();

                return new Tuple<Group, ICollection<Guid>>(group, colletionIds);
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

        public async Task<ICollection<GroupUserUserDetails>> GetManyUserDetailsByIdAsync(Guid id)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<GroupUserUserDetails>(
                    $"[{Schema}].[GroupUserUserDetails_ReadByGroupId]",
                    new { GroupId = id },
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

        public async Task CreateAsync(Group obj, IEnumerable<Guid> collectionIds)
        {
            obj.SetNewId();
            var objWithCollections = JsonConvert.DeserializeObject<GroupWithCollections>(JsonConvert.SerializeObject(obj));
            objWithCollections.CollectionIds = collectionIds.ToGuidIdArrayTVP();

            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[Group_CreateWithCollections]",
                    objWithCollections,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task ReplaceAsync(Group obj, IEnumerable<Guid> collectionIds)
        {
            var objWithCollections = JsonConvert.DeserializeObject<GroupWithCollections>(JsonConvert.SerializeObject(obj));
            objWithCollections.CollectionIds = collectionIds.ToGuidIdArrayTVP();

            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[Group_UpdateWithCollections]",
                    objWithCollections,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public class GroupWithCollections : Group
        {
            public DataTable CollectionIds { get; set; }
        }
    }
}
