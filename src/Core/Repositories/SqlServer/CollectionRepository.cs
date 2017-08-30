using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using System.Linq;
using Newtonsoft.Json;
using Bit.Core.Utilities;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.SqlServer
{
    public class CollectionRepository : Repository<Collection, Guid>, ICollectionRepository
    {
        public CollectionRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public CollectionRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteScalarAsync<int>(
                    "[dbo].[Collection_ReadCountByOrganizationId]",
                    new { OrganizationId = organizationId },
                    commandType: CommandType.StoredProcedure);

                return results;
            }
        }

        public async Task<Tuple<Collection, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryMultipleAsync(
                    $"[{Schema}].[Collection_ReadWithGroupsById]",
                    new { Id = id },
                    commandType: CommandType.StoredProcedure);

                var collection = await results.ReadFirstOrDefaultAsync<Collection>();
                var groups = (await results.ReadAsync<SelectionReadOnly>()).ToList();

                return new Tuple<Collection, ICollection<SelectionReadOnly>>(collection, groups);
            }
        }

        public async Task<ICollection<Collection>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Collection>(
                    $"[{Schema}].[{Table}_ReadByOrganizationId]",
                    new { OrganizationId = organizationId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<Collection>> GetManyByUserIdAsync(Guid userId, bool writeOnly)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Collection>(
                    $"[{Schema}].[Collection_ReadByUserId]",
                    new { UserId = userId, WriteOnly = writeOnly },
                    commandType: CommandType.StoredProcedure);

                // Return distinct Id results.
                return results
                    .GroupBy(c => c.Id)
                    .Select(c => c.First())
                    .ToList();
            }
        }

        public async Task<ICollection<CollectionUserDetails>> GetManyUserDetailsByIdAsync(Guid organizationId,
            Guid collectionId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<CollectionUserDetails>(
                    $"[{Schema}].[CollectionUserDetails_ReadByCollectionId]",
                    new { OrganizationId = organizationId, CollectionId = collectionId },
                    commandType: CommandType.StoredProcedure);

                // Return distinct Id results. If at least one of the grouped results is not ReadOnly, that we return it.
                return results
                    .GroupBy(c => c.OrganizationUserId)
                    .Select(g => g.OrderBy(og => og.ReadOnly).First())
                    .ToList();
            }
        }

        public async Task CreateAsync(Collection obj, IEnumerable<SelectionReadOnly> groups)
        {
            obj.SetNewId();
            var objWithGroups = JsonConvert.DeserializeObject<CollectionWithGroups>(JsonConvert.SerializeObject(obj));
            objWithGroups.Groups = groups.ToArrayTVP();

            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[Collection_CreateWithGroups]",
                    objWithGroups,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task ReplaceAsync(Collection obj, IEnumerable<SelectionReadOnly> groups)
        {
            var objWithGroups = JsonConvert.DeserializeObject<CollectionWithGroups>(JsonConvert.SerializeObject(obj));
            objWithGroups.Groups = groups.ToArrayTVP();

            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[Collection_UpdateWithGroups]",
                    objWithGroups,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task DeleteUserAsync(Guid collectionId, Guid organizationUserId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[CollectionUser_Delete]",
                    new { CollectionId = collectionId, OrganizationUserId = organizationUserId },
                    commandType: CommandType.StoredProcedure);
            }
        }

        public class CollectionWithGroups : Collection
        {
            public DataTable Groups { get; set; }
        }
    }
}
