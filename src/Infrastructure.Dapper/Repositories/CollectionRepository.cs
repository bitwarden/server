using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Bit.Core.AdminConsole.Collections;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.AdminConsole.Helpers;
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

    public async Task<ICollection<Collection>> GetManySharedCollectionsByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Collection>(
                $"[{Schema}].[{Table}_ReadSharedCollectionsByOrganizationId]",
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
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            if (groups == null && users == null)
            {
                await connection.ExecuteAsync(
                    $"[{Schema}].[Collection_Update]",
                    obj,
                    commandType: CommandType.StoredProcedure,
                    transaction: transaction);
            }
            else if (groups != null && users == null)
            {
                await connection.ExecuteAsync(
                    $"[{Schema}].[Collection_UpdateWithGroups]",
                    new CollectionWithGroups(obj, groups),
                    commandType: CommandType.StoredProcedure,
                    transaction: transaction);
            }
            else if (groups == null && users != null)
            {
                await connection.ExecuteAsync(
                    $"[{Schema}].[Collection_UpdateWithUsers]",
                    new CollectionWithUsers(obj, users),
                    commandType: CommandType.StoredProcedure,
                    transaction: transaction);
            }
            else if (groups != null && users != null)
            {
                await connection.ExecuteAsync(
                    $"[{Schema}].[Collection_UpdateWithGroupsAndUsers]",
                    new CollectionWithGroupsAndUsers(obj, groups, users),
                    commandType: CommandType.StoredProcedure,
                    transaction: transaction);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
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

    public async Task CreateDefaultCollectionsAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, string defaultCollectionName)
    {
        organizationUserIds = organizationUserIds.ToList();
        if (!organizationUserIds.Any())
        {
            return;
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        try
        {
            var organizationUserIdsJson = JsonSerializer.Serialize(organizationUserIds);
            await connection.ExecuteAsync(
                "[dbo].[Collection_CreateDefaultCollections]",
                new
                {
                    OrganizationId = organizationId,
                    DefaultCollectionName = defaultCollectionName,
                    OrganizationUserIdsJson = organizationUserIdsJson
                },
                commandType: CommandType.StoredProcedure);
        }
        catch (Exception ex) when (DatabaseExceptionHelpers.IsDuplicateKeyException(ex))
        {
            throw new DuplicateDefaultCollectionException();
        }
    }

    public async Task CreateDefaultCollectionsBulkAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, string defaultCollectionName)
    {
        organizationUserIds = organizationUserIds.ToList();
        if (!organizationUserIds.Any())
        {
            return;
        }

        var (collections, collectionUsers) =
            CollectionUtils.BuildDefaultUserCollections(organizationId, organizationUserIds, defaultCollectionName);

        await using var connection = new SqlConnection(ConnectionString);
        connection.Open();
        await using var transaction = connection.BeginTransaction();

        try
        {

            // CRITICAL: Insert semaphore entries BEFORE collections
            // Database will throw on duplicate primary key (OrganizationUserId)
            var now = DateTime.UtcNow;
            var semaphores = collectionUsers.Select(c => new DefaultCollectionSemaphore
            {
                OrganizationUserId = c.OrganizationUserId,
                CreationDate = now
            }).ToList();

            await BulkInsertDefaultCollectionSemaphoresAsync(connection, transaction, semaphores);
            await BulkResourceCreationService.CreateCollectionsAsync(connection, transaction, collections);
            await BulkResourceCreationService.CreateCollectionsUsersAsync(connection, transaction, collectionUsers);

            transaction.Commit();
        }
        catch (Exception ex) when (DatabaseExceptionHelpers.IsDuplicateKeyException(ex))
        {
            transaction.Rollback();
            throw new DuplicateDefaultCollectionException();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<HashSet<Guid>> GetDefaultCollectionSemaphoresAsync(IEnumerable<Guid> organizationUserIds)
    {
        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.QueryAsync<Guid>(
            "[dbo].[DefaultCollectionSemaphore_ReadByOrganizationUserIds]",
            new { OrganizationUserIds = organizationUserIds.ToGuidIdArrayTVP() },
            commandType: CommandType.StoredProcedure);

        return results.ToHashSet();
    }

    private async Task BulkInsertDefaultCollectionSemaphoresAsync(SqlConnection connection, SqlTransaction transaction, List<DefaultCollectionSemaphore> semaphores)
    {
        if (!semaphores.Any())
        {
            return;
        }

        // Sort by composite key to reduce deadlocks
        var sortedSemaphores = semaphores
            .OrderBy(s => s.OrganizationUserId)
            .ToList();

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity & SqlBulkCopyOptions.CheckConstraints, transaction);
        bulkCopy.DestinationTableName = "[dbo].[DefaultCollectionSemaphore]";
        bulkCopy.BatchSize = 500;
        bulkCopy.BulkCopyTimeout = 120;
        bulkCopy.EnableStreaming = true;

        var dataTable = new DataTable("DefaultCollectionSemaphoreDataTable");

        var organizationUserIdColumn = new DataColumn(nameof(DefaultCollectionSemaphore.OrganizationUserId), typeof(Guid));
        dataTable.Columns.Add(organizationUserIdColumn);
        var creationDateColumn = new DataColumn(nameof(DefaultCollectionSemaphore.CreationDate), typeof(DateTime));
        dataTable.Columns.Add(creationDateColumn);

        foreach (DataColumn col in dataTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = organizationUserIdColumn;
        dataTable.PrimaryKey = keys;

        foreach (var semaphore in sortedSemaphores)
        {
            var row = dataTable.NewRow();
            row[organizationUserIdColumn] = semaphore.OrganizationUserId;
            row[creationDateColumn] = semaphore.CreationDate;
            dataTable.Rows.Add(row);
        }

        await bulkCopy.WriteToServerAsync(dataTable);
    }

    public class CollectionWithGroupsAndUsers : Collection
    {
        public CollectionWithGroupsAndUsers() { }

        public CollectionWithGroupsAndUsers(Collection collection,
            IEnumerable<CollectionAccessSelection> groups,
            IEnumerable<CollectionAccessSelection> users)
        {
            Id = collection.Id;
            Name = collection.Name;
            OrganizationId = collection.OrganizationId;
            CreationDate = collection.CreationDate;
            RevisionDate = collection.RevisionDate;
            Type = collection.Type;
            ExternalId = collection.ExternalId;
            DefaultUserCollectionEmail = collection.DefaultUserCollectionEmail;
            Groups = groups.ToArrayTVP();
            Users = users.ToArrayTVP();
        }

        [DisallowNull]
        public DataTable? Groups { get; set; }
        [DisallowNull]
        public DataTable? Users { get; set; }
    }

    public class CollectionWithGroups : Collection
    {
        public CollectionWithGroups() { }

        public CollectionWithGroups(Collection collection, IEnumerable<CollectionAccessSelection> groups)
        {
            Id = collection.Id;
            Name = collection.Name;
            OrganizationId = collection.OrganizationId;
            CreationDate = collection.CreationDate;
            RevisionDate = collection.RevisionDate;
            Type = collection.Type;
            ExternalId = collection.ExternalId;
            DefaultUserCollectionEmail = collection.DefaultUserCollectionEmail;
            Groups = groups.ToArrayTVP();
        }

        [DisallowNull]
        public DataTable? Groups { get; set; }
    }

    public class CollectionWithUsers : Collection
    {
        public CollectionWithUsers() { }

        public CollectionWithUsers(Collection collection, IEnumerable<CollectionAccessSelection> users)
        {

            Id = collection.Id;
            Name = collection.Name;
            OrganizationId = collection.OrganizationId;
            CreationDate = collection.CreationDate;
            RevisionDate = collection.RevisionDate;
            Type = collection.Type;
            ExternalId = collection.ExternalId;
            DefaultUserCollectionEmail = collection.DefaultUserCollectionEmail;
            Users = users.ToArrayTVP();
        }

        [DisallowNull]
        public DataTable? Users { get; set; }
    }
}
