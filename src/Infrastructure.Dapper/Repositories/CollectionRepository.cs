using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

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

    public async Task<Tuple<Collection?, CollectionAccessDetails>> GetByIdWithAccessAsync(Guid id)
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

            return new Tuple<Collection?, CollectionAccessDetails>(collection, access);
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
                            .FirstOrDefault(g => g.Key == collection.Id)?
                            .Select(g => new CollectionAccessSelection
                            {
                                Id = g.GroupId,
                                HidePasswords = g.HidePasswords,
                                ReadOnly = g.ReadOnly,
                                Manage = g.Manage
                            }).ToList() ?? new List<CollectionAccessSelection>(),
                        Users = users
                            .FirstOrDefault(u => u.Key == collection.Id)?
                            .Select(c => new CollectionAccessSelection
                            {
                                Id = c.OrganizationUserId,
                                HidePasswords = c.HidePasswords,
                                ReadOnly = c.ReadOnly,
                                Manage = c.Manage
                            }).ToList() ?? new List<CollectionAccessSelection>()
                    }
                )
            ).ToList();
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

    public async Task<ICollection<CollectionAdminDetails>> GetManyByOrganizationIdWithPermissionsAsync(Guid organizationId, Guid userId, bool includeAccessRelationships)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                $"[{Schema}].[Collection_ReadByOrganizationIdWithPermissions]",
                new { OrganizationId = organizationId, UserId = userId, IncludeAccessRelationships = includeAccessRelationships },
                commandType: CommandType.StoredProcedure);

            var collections = (await results.ReadAsync<CollectionAdminDetails>()).ToList();

            if (!includeAccessRelationships)
            {
                return collections;
            }

            var groups = (await results.ReadAsync<CollectionGroup>())
                .GroupBy(g => g.CollectionId)
                .ToList();
            var users = (await results.ReadAsync<CollectionUser>())
                .GroupBy(u => u.CollectionId)
                .ToList();

            foreach (var collection in collections)
            {
                collection.Groups = groups
                    .FirstOrDefault(g => g.Key == collection.Id)?
                    .Select(g => new CollectionAccessSelection
                    {
                        Id = g.GroupId,
                        HidePasswords = g.HidePasswords,
                        ReadOnly = g.ReadOnly,
                        Manage = g.Manage
                    }).ToList() ?? new List<CollectionAccessSelection>();
                collection.Users = users
                    .FirstOrDefault(u => u.Key == collection.Id)?
                    .Select(c => new CollectionAccessSelection
                    {
                        Id = c.OrganizationUserId,
                        HidePasswords = c.HidePasswords,
                        ReadOnly = c.ReadOnly,
                        Manage = c.Manage
                    }).ToList() ?? new List<CollectionAccessSelection>();
            }

            return collections;
        }
    }

    public async Task<CollectionAdminDetails?> GetByIdWithPermissionsAsync(Guid collectionId, Guid? userId, bool includeAccessRelationships)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                $"[{Schema}].[Collection_ReadByIdWithPermissions]",
                new { CollectionId = collectionId, UserId = userId, IncludeAccessRelationships = includeAccessRelationships },
                commandType: CommandType.StoredProcedure);

            var collectionDetails = await results.ReadFirstOrDefaultAsync<CollectionAdminDetails>();

            if (!includeAccessRelationships || collectionDetails == null) return collectionDetails;

            // TODO-NRE: collectionDetails should be checked for null and probably return early
            collectionDetails!.Groups = (await results.ReadAsync<CollectionAccessSelection>()).ToList();
            collectionDetails.Users = (await results.ReadAsync<CollectionAccessSelection>()).ToList();

            return collectionDetails;
        }
    }

    public async Task CreateAsync(Collection obj, IEnumerable<CollectionAccessSelection>? groups, IEnumerable<CollectionAccessSelection>? users)
    {
        obj.SetNewId();
        var objWithGroupsAndUsers = JsonSerializer.Deserialize<CollectionWithGroupsAndUsers>(JsonSerializer.Serialize(obj))!;

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

    public async Task ReplaceAsync(Collection obj, IEnumerable<CollectionAccessSelection>? groups, IEnumerable<CollectionAccessSelection>? users)
    {
        var objWithGroupsAndUsers = JsonSerializer.Deserialize<CollectionWithGroupsAndUsers>(JsonSerializer.Serialize(obj))!;

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

    public async Task CreateOrUpdateAccessForManyAsync(Guid organizationId, IEnumerable<Guid> collectionIds,
        IEnumerable<CollectionAccessSelection> users, IEnumerable<CollectionAccessSelection> groups)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var usersArray = users != null ? users.ToArrayTVP() : Enumerable.Empty<CollectionAccessSelection>().ToArrayTVP();
            var groupsArray = groups != null ? groups.ToArrayTVP() : Enumerable.Empty<CollectionAccessSelection>().ToArrayTVP();

            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Collection_CreateOrUpdateAccessForMany]",
                new { OrganizationId = organizationId, CollectionIds = collectionIds.ToGuidIdArrayTVP(), Users = usersArray, Groups = groupsArray },
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

    // Jimmy TODO: update the params
    public async Task CreateDefaultCollectionsAsync()
    {

        using (var connection = new SqlConnection(ConnectionString))
        {
            connection.Open();

            var orgId = Guid.Parse("C8D71195-CA4F-473F-80E6-B2AB010F35EF");


            var collections = new[]
            {
            new Collection
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    Name = "default collection 1",
                    ExternalId = "ENG-001",
                    CreationDate = DateTime.UtcNow.AddDays(-30),
                    RevisionDate = DateTime.UtcNow.AddDays(-1),
                    Type = CollectionType.DefaultUserCollection,
                    DefaultUserCollectionEmail = "eng@example.com"
                },
                new Collection
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    Name = "default collection 2",
                    ExternalId = "MKT-002",
                    CreationDate = DateTime.UtcNow.AddDays(-60),
                    RevisionDate = DateTime.UtcNow.AddDays(-2),
                    Type = CollectionType.DefaultUserCollection,
                    DefaultUserCollectionEmail = "marketing@example.com"
                },
                new Collection
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    Name = "default collection 3",
                    ExternalId = null,
                    CreationDate = DateTime.UtcNow.AddDays(-15),
                    RevisionDate = DateTime.UtcNow,
                    Type = CollectionType.DefaultUserCollection,
                    DefaultUserCollectionEmail = "marketing1@example.com"
                }
            };

            using (var transaction = connection.BeginTransaction())
            {
                try
                {

                    // Jimmy TODO: Filter existing CollectionUser

                    // Jimmy TODO: create the CollectionUser



                    await CreateCollections(connection, transaction, collections);

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    // Jimmy TODO: rename
    private async Task CreateTempTable(SqlConnection connection, SqlTransaction transaction, Collection[] collections)
    {
        var sqlCreateTemp = @"
            CREATE TABLE #TempGuid (
                Id UNIQUEIDENTIFIER NOT NULL
            );";

        using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
        {
            await cmd.ExecuteNonQueryAsync();
        }


        var dataTable = new DataTable();
        dataTable.Columns.Add("Id", typeof(Guid));
        foreach (var id in yourGuidList)
        {
            dataTable.Rows.Add(id);
        }

        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
        {
            bulkCopy.DestinationTableName = "#TempGuid";
            bulkCopy.WriteToServer(dataTable);
        }

    }

    private async Task CreateCollections(SqlConnection connection, SqlTransaction transaction, Collection[] collections)
    {
        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
        {
            bulkCopy.DestinationTableName = "[dbo].[Collection]";
            var dataTable = BuildCollectionsTable(bulkCopy, collections);
            await bulkCopy.WriteToServerAsync(dataTable);
        }
    }

    // Jimmy TODO: Assess the trade-offs between IEnumerable and arrays.
    private DataTable BuildCollectionsTable(SqlBulkCopy bulkCopy, Collection[] collections)
    {
        var c = collections.FirstOrDefault();
        if (c == null)
        {
            throw new ApplicationException("Must have some collections to bulk import.");
        }

        var collectionsTable = new DataTable("CollectionDataTable");

        var idColumn = new DataColumn(nameof(c.Id), c.Id.GetType());
        collectionsTable.Columns.Add(idColumn);
        var organizationIdColumn = new DataColumn(nameof(c.OrganizationId), c.OrganizationId.GetType());
        collectionsTable.Columns.Add(organizationIdColumn);
        var nameColumn = new DataColumn(nameof(c.Name), typeof(string));
        collectionsTable.Columns.Add(nameColumn);
        var creationDateColumn = new DataColumn(nameof(c.CreationDate), c.CreationDate.GetType());
        collectionsTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(c.RevisionDate), c.RevisionDate.GetType());
        collectionsTable.Columns.Add(revisionDateColumn);
        var externalIdColumn = new DataColumn(nameof(c.ExternalId), typeof(string));
        collectionsTable.Columns.Add(externalIdColumn);
        var typeColumn = new DataColumn(nameof(c.Type), typeof(CollectionType));
        collectionsTable.Columns.Add(typeColumn);
        var defaultUserCollectionEmailColumn = new DataColumn(nameof(c.DefaultUserCollectionEmail), typeof(string));
        collectionsTable.Columns.Add(defaultUserCollectionEmailColumn);

        foreach (DataColumn col in collectionsTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        collectionsTable.PrimaryKey = keys;

        foreach (var collection in collections)
        {
            var row = collectionsTable.NewRow();

            row[idColumn] = collection.Id;
            row[organizationIdColumn] = collection.OrganizationId;
            row[nameColumn] = collection.Name;
            row[creationDateColumn] = collection.CreationDate;
            row[revisionDateColumn] = collection.RevisionDate;
            row[externalIdColumn] = collection.ExternalId;
            row[typeColumn] = collection.Type;
            row[defaultUserCollectionEmailColumn] = collection.DefaultUserCollectionEmail;

            collectionsTable.Rows.Add(row);
        }

        return collectionsTable;
    }

    public class CollectionWithGroupsAndUsers : Collection
    {
        [DisallowNull]
        public DataTable? Groups { get; set; }
        [DisallowNull]
        public DataTable? Users { get; set; }
    }
}
