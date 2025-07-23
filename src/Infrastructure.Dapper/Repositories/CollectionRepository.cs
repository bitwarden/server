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

    public async Task CreateDefaultCollectionsAsync(Guid organizationId, IEnumerable<Guid> affectedOrgUserIds, string defaultCollectionName)
    {
        if (!affectedOrgUserIds.Any())
        {
            return;
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var orgUserIdWithDefaultCollection = await GetOrgUserIdsWithDefaultCollectionAsync(connection, transaction, organizationId);

                    var missingDefaultCollectionUserIds = affectedOrgUserIds
                        .Except(orgUserIdWithDefaultCollection)
                        .ToList();

                    var (collectionUsers, collections) = BuildDefaultCollectionForUsers(organizationId, missingDefaultCollectionUserIds, defaultCollectionName);

                    if (!collectionUsers.Any() || !collections.Any())
                    {
                        return;
                    }

                    await CreateCollectionsAsync(connection, transaction, collections);
                    await CreateCollectionsUsersAsync(connection, transaction, collectionUsers);

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

    private async Task<HashSet<Guid>> GetOrgUserIdsWithDefaultCollectionAsync(SqlConnection connection, SqlTransaction transaction, Guid organizationId)
    {
        const string sql = @"
                    SELECT
                        ou.Id AS OrganizationUserId
                    FROM
                        OrganizationUser ou
                    INNER JOIN
                        CollectionUser cu ON cu.OrganizationUserId = ou.Id
                    INNER JOIN
                        Collection c ON c.Id = cu.CollectionId
                    WHERE
                        ou.OrganizationId = @OrganizationId
                        AND c.Type = @CollectionType;
                ";

        var organizationUserIds = await connection.QueryAsync<Guid>(
            sql,
            new { OrganizationId = organizationId, CollectionType = CollectionType.DefaultUserCollection },
            transaction: transaction
        );

        return organizationUserIds.ToHashSet();
    }

    private (List<CollectionUser> collectionUser, List<Collection> collection) BuildDefaultCollectionForUsers(Guid organizationId, List<Guid> missingDefaultCollectionUserIds, string defaultCollectionName)
    {
        var collectionUsers = new List<CollectionUser>();
        var collections = new List<Collection>();

        foreach (var orgUserId in missingDefaultCollectionUserIds)
        {
            var collectionId = Guid.NewGuid();

            collections.Add(new Collection
            {
                Id = collectionId,
                OrganizationId = organizationId,
                Name = defaultCollectionName,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow,
                Type = CollectionType.DefaultUserCollection,
                DefaultUserCollectionEmail = null

            });

            collectionUsers.Add(new CollectionUser
            {
                CollectionId = collectionId,
                OrganizationUserId = orgUserId,
                ReadOnly = false,
                HidePasswords = false,
                Manage = true,
            });
        }

        return (collectionUsers, collections);
    }

    private async Task CreateCollectionsUsersAsync(SqlConnection connection, SqlTransaction transaction, List<CollectionUser> collectionUsers)
    {
        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
        {
            bulkCopy.DestinationTableName = "[dbo].[CollectionUser]";
            var dataTable = BuildCollectionsUsersTable(bulkCopy, collectionUsers);
            await bulkCopy.WriteToServerAsync(dataTable);
        }
    }

    private DataTable BuildCollectionsUsersTable(SqlBulkCopy bulkCopy, List<CollectionUser> collectionUsers)
    {
        var collectionUser = collectionUsers.First();

        var table = new DataTable("CollectionUserDataTable");

        var collectionIdColumn = new DataColumn(nameof(collectionUser.CollectionId), typeof(Guid));
        table.Columns.Add(collectionIdColumn);

        var orgUserIdColumn = new DataColumn(nameof(collectionUser.OrganizationUserId), typeof(Guid));
        table.Columns.Add(orgUserIdColumn);

        var readOnlyColumn = new DataColumn(nameof(collectionUser.ReadOnly), typeof(bool));
        table.Columns.Add(readOnlyColumn);

        var hidePasswordsColumn = new DataColumn(nameof(collectionUser.HidePasswords), typeof(bool));
        table.Columns.Add(hidePasswordsColumn);

        var manageColumn = new DataColumn(nameof(collectionUser.Manage), typeof(bool));
        table.Columns.Add(manageColumn);

        foreach (DataColumn col in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        foreach (var collectionUserRecord in collectionUsers)
        {
            var row = table.NewRow();

            row[collectionIdColumn] = collectionUserRecord.CollectionId;
            row[orgUserIdColumn] = collectionUserRecord.OrganizationUserId;
            row[readOnlyColumn] = collectionUserRecord.ReadOnly;
            row[hidePasswordsColumn] = collectionUserRecord.HidePasswords;
            row[manageColumn] = collectionUserRecord.Manage;

            table.Rows.Add(row);
        }

        return table;
    }

    private async Task CreateCollectionsAsync(SqlConnection connection, SqlTransaction transaction, IEnumerable<Collection> collections)
    {
        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
        {
            bulkCopy.DestinationTableName = "[dbo].[Collection]";
            var dataTable = BuildCollectionsTable(bulkCopy, collections);
            await bulkCopy.WriteToServerAsync(dataTable);
        }
    }

    private DataTable BuildCollectionsTable(SqlBulkCopy bulkCopy, IEnumerable<Collection> collections)
    {
        var collection = collections.First();

        var collectionsTable = new DataTable("CollectionDataTable");

        var idColumn = new DataColumn(nameof(collection.Id), collection.Id.GetType());
        collectionsTable.Columns.Add(idColumn);
        var organizationIdColumn = new DataColumn(nameof(collection.OrganizationId), collection.OrganizationId.GetType());
        collectionsTable.Columns.Add(organizationIdColumn);
        var nameColumn = new DataColumn(nameof(collection.Name), typeof(string));
        collectionsTable.Columns.Add(nameColumn);
        var creationDateColumn = new DataColumn(nameof(collection.CreationDate), collection.CreationDate.GetType());
        collectionsTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(collection.RevisionDate), collection.RevisionDate.GetType());
        collectionsTable.Columns.Add(revisionDateColumn);
        var externalIdColumn = new DataColumn(nameof(collection.ExternalId), typeof(string));
        collectionsTable.Columns.Add(externalIdColumn);
        var typeColumn = new DataColumn(nameof(collection.Type), typeof(CollectionType));
        collectionsTable.Columns.Add(typeColumn);
        var defaultUserCollectionEmailColumn = new DataColumn(nameof(collection.DefaultUserCollectionEmail), typeof(string));
        collectionsTable.Columns.Add(defaultUserCollectionEmailColumn);

        foreach (DataColumn col in collectionsTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        collectionsTable.PrimaryKey = keys;

        foreach (var collectionRecord in collections)
        {
            var row = collectionsTable.NewRow();

            row[idColumn] = collectionRecord.Id;
            row[organizationIdColumn] = collectionRecord.OrganizationId;
            row[nameColumn] = collectionRecord.Name;
            row[creationDateColumn] = collectionRecord.CreationDate;
            row[revisionDateColumn] = collectionRecord.RevisionDate;
            row[externalIdColumn] = collectionRecord.ExternalId;
            row[typeColumn] = collectionRecord.Type;
            row[defaultUserCollectionEmailColumn] = collectionRecord.DefaultUserCollectionEmail;

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
