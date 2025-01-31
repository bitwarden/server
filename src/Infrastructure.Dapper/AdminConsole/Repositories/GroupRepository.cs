﻿using System.Data;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.AdminConsole.Repositories;

public class GroupRepository : Repository<Group, Guid>, IGroupRepository
{
    public GroupRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public GroupRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<Tuple<Group?, ICollection<CollectionAccessSelection>>> GetByIdWithCollectionsAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                $"[{Schema}].[Group_ReadWithCollectionsById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            var group = await results.ReadFirstOrDefaultAsync<Group>();
            var colletions = (await results.ReadAsync<CollectionAccessSelection>()).ToList();

            return new Tuple<Group?, ICollection<CollectionAccessSelection>>(group, colletions);
        }
    }

    public async Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Group>(
                $"[{Schema}].[Group_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Tuple<Group, ICollection<CollectionAccessSelection>>>> GetManyWithCollectionsByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryMultipleAsync(
                $"[{Schema}].[Group_ReadWithCollectionsByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            var groups = (await results.ReadAsync<Group>()).ToList();
            var collections = (await results.ReadAsync<CollectionGroup>())
                .GroupBy(c => c.GroupId)
                .ToList();

            return groups.Select(group =>
                    new Tuple<Group, ICollection<CollectionAccessSelection>>(
                        group,
                        collections.FirstOrDefault(c => c.Key == group.Id)?
                            .Select(c => new CollectionAccessSelection
                            {
                                Id = c.CollectionId,
                                HidePasswords = c.HidePasswords,
                                ReadOnly = c.ReadOnly,
                                Manage = c.Manage
                            }
                            ).ToList() ?? new List<CollectionAccessSelection>())
                ).ToList();
        }
    }

    public async Task<ICollection<Group>> GetManyByManyIds(IEnumerable<Guid> groupIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Group>(
                $"[{Schema}].[Group_ReadByIds]",
                new { Ids = groupIds.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Guid>> GetManyIdsByUserIdAsync(Guid organizationUserId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Guid>(
                $"[{Schema}].[GroupUser_ReadGroupIdsByOrganizationUserId]",
                new { OrganizationUserId = organizationUserId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Guid>> GetManyUserIdsByIdAsync(Guid id, bool useReadOnlyReplica = false)
    {
        var connectionString = useReadOnlyReplica
            ? ReadOnlyConnectionString
            : ConnectionString;

        using (var connection = new SqlConnection(connectionString))
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
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<GroupUser>(
                $"[{Schema}].[GroupUser_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task CreateAsync(Group obj, IEnumerable<CollectionAccessSelection> collections)
    {
        obj.SetNewId();
        var objWithCollections = JsonSerializer.Deserialize<GroupWithCollections>(JsonSerializer.Serialize(obj))!;
        objWithCollections.Collections = collections.ToArrayTVP();

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Group_CreateWithCollections]",
                objWithCollections,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task ReplaceAsync(Group obj, IEnumerable<CollectionAccessSelection> collections)
    {
        var objWithCollections = JsonSerializer.Deserialize<GroupWithCollections>(JsonSerializer.Serialize(obj))!;
        objWithCollections.Collections = collections.ToArrayTVP();

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Group_UpdateWithCollections]",
                objWithCollections,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task DeleteUserAsync(Guid groupId, Guid organizationUserId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[GroupUser_Delete]",
                new { GroupId = groupId, OrganizationUserId = organizationUserId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task UpdateUsersAsync(Guid groupId, IEnumerable<Guid> organizationUserIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                "[dbo].[GroupUser_UpdateUsers]",
                new { GroupId = groupId, OrganizationUserIds = organizationUserIds.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task AddGroupUsersByIdAsync(Guid groupId, IEnumerable<Guid> organizationUserIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                "[dbo].[GroupUser_AddUsers]",
                new { GroupId = groupId, OrganizationUserIds = organizationUserIds.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> groupIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync("[dbo].[Group_DeleteByIds]",
                new { Ids = groupIds.ToGuidIdArrayTVP() }, commandType: CommandType.StoredProcedure);
        }
    }
}
