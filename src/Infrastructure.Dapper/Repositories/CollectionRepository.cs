using System.Data;
using System.Data.SqlClient;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class CollectionRepository : Repository<Collection, Guid>, ICollectionRepository
{
    public CollectionRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public CollectionRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteScalarAsync<int>(
                "[dbo].[Collection_ReadCountByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results;
        }
    }

    public async Task<Tuple<Collection, CollectionAccessDetails>> GetByIdWithAccessAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                $"[{Schema}].[Collection_ReadWithGroupsAndUsersById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            var collection = await results.ReadFirstOrDefaultAsync<Collection>();
            var groups = (await results.ReadAsync<CollectionAccessSelection>()).ToList();
            var users = (await results.ReadAsync<CollectionAccessSelection>()).ToList();
            var access = new CollectionAccessDetails { Groups = groups, Users = users };

            return new Tuple<Collection, CollectionAccessDetails>(collection, access);
        }
    }

    public async Task<Tuple<CollectionDetails, CollectionAccessDetails>> GetByIdWithAccessAsync(
        Guid id, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                $"[{Schema}].[Collection_ReadWithGroupsAndUsersByIdUserId]",
                new { Id = id, UserId = userId },
                commandType: CommandType.StoredProcedure);

            var collection = await results.ReadFirstOrDefaultAsync<CollectionDetails>();
            var groups = (await results.ReadAsync<CollectionAccessSelection>()).ToList();
            var users = (await results.ReadAsync<CollectionAccessSelection>()).ToList();
            var access = new CollectionAccessDetails { Groups = groups, Users = users };

            return new Tuple<CollectionDetails, CollectionAccessDetails>(collection, access);
        }
    }

    public async Task<ICollection<Collection>> GetManyByManyIdsAsync(IEnumerable<Guid> collectionIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Collection>(
                $"[{Schema}].[Collection_ReadByIds]",
                new { Ids = collectionIds.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Collection>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Collection>(
                $"[{Schema}].[{Table}_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Tuple<Collection, CollectionAccessDetails>>> GetManyByOrganizationIdWithAccessAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                $"[{Schema}].[Collection_ReadWithGroupsAndUsersByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            var collections = (await results.ReadAsync<Collection>());
            var groups = (await results.ReadAsync<CollectionGroup>())
                .GroupBy(g => g.CollectionId);
            var users = (await results.ReadAsync<CollectionUser>())
                .GroupBy(u => u.CollectionId);

            return collections.Select(collection =>
                new Tuple<Collection, CollectionAccessDetails>(
                    collection,
                    new CollectionAccessDetails
                    {
                        Groups = groups
                            .FirstOrDefault(g => g.Key == collection.Id)
                            .Select(g => new CollectionAccessSelection
                            {
                                Id = g.GroupId,
                                HidePasswords = g.HidePasswords,
                                ReadOnly = g.ReadOnly
                            }).ToList(),
                        Users = users
                            .FirstOrDefault(u => u.Key == collection.Id)
                            .Select(c => new CollectionAccessSelection
                            {
                                Id = c.OrganizationUserId,
                                HidePasswords = c.HidePasswords,
                                ReadOnly = c.ReadOnly
                            }).ToList()
                    }
                )
            ).ToList();
        }
    }

    public async Task<ICollection<Tuple<Collection, CollectionAccessDetails>>> GetManyByUserIdWithAccessAsync(Guid userId, Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                $"[{Schema}].[Collection_ReadWithGroupsAndUsersByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            var collections = (await results.ReadAsync<Collection>()).Where(c => c.OrganizationId == organizationId);
            var groups = (await results.ReadAsync<CollectionGroup>())
                .GroupBy(g => g.CollectionId);
            var users = (await results.ReadAsync<CollectionUser>())
                .GroupBy(u => u.CollectionId);

            return collections.Select(collection =>
                new Tuple<Collection, CollectionAccessDetails>(
                    collection,
                    new CollectionAccessDetails
                    {
                        Groups = groups
                            .FirstOrDefault(g => g.Key == collection.Id)
                            .Select(g => new CollectionAccessSelection
                            {
                                Id = g.GroupId,
                                HidePasswords = g.HidePasswords,
                                ReadOnly = g.ReadOnly
                            }).ToList(),
                        Users = users
                            .FirstOrDefault(u => u.Key == collection.Id)
                            .Select(c => new CollectionAccessSelection
                            {
                                Id = c.OrganizationUserId,
                                HidePasswords = c.HidePasswords,
                                ReadOnly = c.ReadOnly
                            }).ToList()
                    }
                )
            ).ToList();
        }

    }

    public async Task<CollectionDetails> GetByIdAsync(Guid id, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CollectionDetails>(
                $"[{Schema}].[Collection_ReadByIdUserId]",
                new { Id = id, UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.FirstOrDefault();
        }
    }

    public async Task<ICollection<CollectionDetails>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CollectionDetails>(
                $"[{Schema}].[Collection_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task CreateAsync(Collection obj, IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users)
    {
        obj.SetNewId();
        var objWithGroupsAndUsers = JsonSerializer.Deserialize<CollectionWithGroupsAndUsers>(JsonSerializer.Serialize(obj));

        objWithGroupsAndUsers.Groups = groups != null ? groups.ToArrayTVP() : Enumerable.Empty<CollectionAccessSelection>().ToArrayTVP();
        objWithGroupsAndUsers.Users = users != null ? users.ToArrayTVP() : Enumerable.Empty<CollectionAccessSelection>().ToArrayTVP();

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Collection_CreateWithGroupsAndUsers]",
                objWithGroupsAndUsers,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task ReplaceAsync(Collection obj, IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users)
    {
        var objWithGroupsAndUsers = JsonSerializer.Deserialize<CollectionWithGroupsAndUsers>(JsonSerializer.Serialize(obj));

        objWithGroupsAndUsers.Groups = groups != null ? groups.ToArrayTVP() : Enumerable.Empty<CollectionAccessSelection>().ToArrayTVP();
        objWithGroupsAndUsers.Users = users != null ? users.ToArrayTVP() : Enumerable.Empty<CollectionAccessSelection>().ToArrayTVP();

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Collection_UpdateWithGroupsAndUsers]",
                objWithGroupsAndUsers,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> collectionIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync("[dbo].[Collection_DeleteByIds]",
                new { Ids = collectionIds.ToGuidIdArrayTVP() }, commandType: CommandType.StoredProcedure);
        }
    }

    public async Task CreateUserAsync(Guid collectionId, Guid organizationUserId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[CollectionUser_Create]",
                new { CollectionId = collectionId, OrganizationUserId = organizationUserId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task DeleteUserAsync(Guid collectionId, Guid organizationUserId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[CollectionUser_Delete]",
                new { CollectionId = collectionId, OrganizationUserId = organizationUserId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task UpdateUsersAsync(Guid id, IEnumerable<CollectionAccessSelection> users)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[CollectionUser_UpdateUsers]",
                new { CollectionId = id, Users = users.ToArrayTVP() },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<ICollection<CollectionAccessSelection>> GetManyUsersByIdAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CollectionAccessSelection>(
                $"[{Schema}].[CollectionUser_ReadByCollectionId]",
                new { CollectionId = id },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public class CollectionWithGroupsAndUsers : Collection
    {
        public DataTable Groups { get; set; }
        public DataTable Users { get; set; }
    }
}
