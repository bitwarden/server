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

    public async Task<Tuple<Collection, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
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

    public async Task<Tuple<CollectionDetails, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(
        Guid id, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                $"[{Schema}].[Collection_ReadWithGroupsByIdUserId]",
                new { Id = id, UserId = userId },
                commandType: CommandType.StoredProcedure);

            var collection = await results.ReadFirstOrDefaultAsync<CollectionDetails>();
            var groups = (await results.ReadAsync<SelectionReadOnly>()).ToList();

            return new Tuple<CollectionDetails, ICollection<SelectionReadOnly>>(collection, groups);
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

    public async Task CreateAsync(Collection obj, IEnumerable<SelectionReadOnly> groups)
    {
        obj.SetNewId();
        var objWithGroups = JsonSerializer.Deserialize<CollectionWithGroups>(JsonSerializer.Serialize(obj));
        objWithGroups.Groups = groups.ToArrayTVP();

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Collection_CreateWithGroups]",
                objWithGroups,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task ReplaceAsync(Collection obj, IEnumerable<SelectionReadOnly> groups)
    {
        var objWithGroups = JsonSerializer.Deserialize<CollectionWithGroups>(JsonSerializer.Serialize(obj));
        objWithGroups.Groups = groups.ToArrayTVP();

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Collection_UpdateWithGroups]",
                objWithGroups,
                commandType: CommandType.StoredProcedure);
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

    public async Task UpdateUsersAsync(Guid id, IEnumerable<SelectionReadOnly> users)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[CollectionUser_UpdateUsers]",
                new { CollectionId = id, Users = users.ToArrayTVP() },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<ICollection<SelectionReadOnly>> GetManyUsersByIdAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<SelectionReadOnly>(
                $"[{Schema}].[CollectionUser_ReadByCollectionId]",
                new { CollectionId = id },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public class CollectionWithGroups : Collection
    {
        public DataTable Groups { get; set; }
    }
}
