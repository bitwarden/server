﻿using AutoMapper;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class GroupRepository : Repository<AdminConsoleEntities.Group, Group, Guid>, IGroupRepository
{
    public GroupRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Groups)
    { }

    public async Task CreateAsync(AdminConsoleEntities.Group obj, IEnumerable<CollectionAccessSelection> collections)
    {
        var grp = await base.CreateAsync(obj);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var availableCollections = await (
                from c in dbContext.Collections
                where c.OrganizationId == grp.OrganizationId
                select c).ToListAsync();
            var filteredCollections = collections.Where(c => availableCollections.Any(a => c.Id == a.Id));
            var collectionGroups = filteredCollections.Select(y => new CollectionGroup
            {
                CollectionId = y.Id,
                GroupId = grp.Id,
                ReadOnly = y.ReadOnly,
                HidePasswords = y.HidePasswords,
                Manage = y.Manage,
            });
            await dbContext.CollectionGroups.AddRangeAsync(collectionGroups);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteUserAsync(Guid groupId, Guid organizationUserId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from gu in dbContext.GroupUsers
                        where gu.GroupId == groupId &&
                            gu.OrganizationUserId == organizationUserId
                        select gu;
            dbContext.RemoveRange(await query.ToListAsync());
            await dbContext.UserBumpAccountRevisionDateByOrganizationUserIdAsync(organizationUserId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<Tuple<AdminConsoleEntities.Group, ICollection<CollectionAccessSelection>>> GetByIdWithCollectionsAsync(Guid id)
    {
        var grp = await base.GetByIdAsync(id);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = await (
                from cg in dbContext.CollectionGroups
                where cg.GroupId == id
                select cg).ToListAsync();
            var collections = query.Select(c => new CollectionAccessSelection
            {
                Id = c.CollectionId,
                ReadOnly = c.ReadOnly,
                HidePasswords = c.HidePasswords,
                Manage = c.Manage,
            }).ToList();
            return new Tuple<AdminConsoleEntities.Group, ICollection<CollectionAccessSelection>>(
                grp, collections);
        }
    }

    public async Task<ICollection<AdminConsoleEntities.Group>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var data = await (
                from g in dbContext.Groups
                where g.OrganizationId == organizationId
                select g).ToListAsync();
            return Mapper.Map<List<AdminConsoleEntities.Group>>(data);
        }
    }

    public async Task<ICollection<Tuple<AdminConsoleEntities.Group, ICollection<CollectionAccessSelection>>>>
        GetManyWithCollectionsByOrganizationIdAsync(Guid organizationId)
    {
        var groups = await GetManyByOrganizationIdAsync(organizationId);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = await (
                from cg in dbContext.CollectionGroups
                where cg.Group.OrganizationId == organizationId
                select cg).ToListAsync();

            var collections = query.GroupBy(c => c.GroupId).ToList();

            return groups.Select(group =>
                new Tuple<AdminConsoleEntities.Group, ICollection<CollectionAccessSelection>>(
                    group,
                    collections
                        .FirstOrDefault(c => c.Key == group.Id)?
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

    public async Task<ICollection<AdminConsoleEntities.Group>> GetManyByManyIds(IEnumerable<Guid> groupIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from g in dbContext.Groups
                        where groupIds.Contains(g.Id)
                        select g;
            var groups = await query.ToListAsync();
            return Mapper.Map<List<AdminConsoleEntities.Group>>(groups);
        }
    }

    public async Task<ICollection<AdminConsoleEntities.GroupUser>> GetManyGroupUsersByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query =
                from gu in dbContext.GroupUsers
                join g in dbContext.Groups
                    on gu.GroupId equals g.Id
                where g.OrganizationId == organizationId
                select gu;
            var groupUsers = await query.ToListAsync();
            return Mapper.Map<List<AdminConsoleEntities.GroupUser>>(groupUsers);
        }
    }

    public async Task<ICollection<Guid>> GetManyIdsByUserIdAsync(Guid organizationUserId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query =
                from gu in dbContext.GroupUsers
                where gu.OrganizationUserId == organizationUserId
                select gu;
            var groupIds = await query.Select(x => x.GroupId).ToListAsync();
            return groupIds;
        }
    }

    public async Task<ICollection<Guid>> GetManyUserIdsByIdAsync(Guid id, bool useReadOnlyReplica = false)
    {
        // EF is only used for self-hosted so read-only replica parameter is ignored

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query =
                from gu in dbContext.GroupUsers
                where gu.GroupId == id
                select gu;
            var groupIds = await query.Select(x => x.OrganizationUserId).ToListAsync();
            return groupIds;
        }
    }

    public async Task ReplaceAsync(AdminConsoleEntities.Group group, IEnumerable<CollectionAccessSelection> requestedCollections)
    {
        await base.ReplaceAsync(group);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var availableCollections = await dbContext.Collections
                .Where(c => c.OrganizationId == group.OrganizationId)
                .Select(c => c.Id)
                .ToListAsync();

            var existingCollectionGroups = await dbContext.CollectionGroups
                .Where(cg => cg.GroupId == group.Id)
                .ToListAsync();

            foreach (var requestedCollection in requestedCollections)
            {
                var existingCollectionGroup = existingCollectionGroups
                    .FirstOrDefault(cg => cg.CollectionId == requestedCollection.Id);

                if (existingCollectionGroup == null)
                {
                    // It needs to be added
                    dbContext.CollectionGroups.Add(new CollectionGroup
                    {
                        CollectionId = requestedCollection.Id,
                        GroupId = group.Id,
                        ReadOnly = requestedCollection.ReadOnly,
                        HidePasswords = requestedCollection.HidePasswords,
                        Manage = requestedCollection.Manage
                    });
                    continue;
                }

                existingCollectionGroup.ReadOnly = requestedCollection.ReadOnly;
                existingCollectionGroup.HidePasswords = requestedCollection.HidePasswords;
                existingCollectionGroup.Manage = requestedCollection.Manage;
            }

            var requestedCollectionIds = requestedCollections.Select(c => c.Id);

            dbContext.CollectionGroups.RemoveRange(
                existingCollectionGroups.Where(cg => !requestedCollectionIds.Contains(cg.CollectionId)));

            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(group.OrganizationId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task UpdateUsersAsync(Guid groupId, IEnumerable<Guid> organizationUserIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgId = (await dbContext.Groups.FindAsync(groupId)).OrganizationId;
            var insert = from ou in dbContext.OrganizationUsers
                         where organizationUserIds.Contains(ou.Id) &&
                             ou.OrganizationId == orgId &&
                             !dbContext.GroupUsers.Any(gu => gu.GroupId == groupId && ou.Id == gu.OrganizationUserId)
                         select new GroupUser
                         {
                             GroupId = groupId,
                             OrganizationUserId = ou.Id,
                         };
            await dbContext.AddRangeAsync(insert);

            var delete = from gu in dbContext.GroupUsers
                         where gu.GroupId == groupId &&
                         !organizationUserIds.Contains(gu.OrganizationUserId)
                         select gu;
            dbContext.RemoveRange(delete);
            await dbContext.SaveChangesAsync();
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(orgId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task AddGroupUsersByIdAsync(Guid groupId, IEnumerable<Guid> organizationUserIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgId = (await dbContext.Groups.FindAsync(groupId)).OrganizationId;
            var insert = from ou in dbContext.OrganizationUsers
                         where organizationUserIds.Contains(ou.Id) &&
                             ou.OrganizationId == orgId &&
                             !dbContext.GroupUsers.Any(gu => gu.GroupId == groupId && ou.Id == gu.OrganizationUserId)
                         select new GroupUser
                         {
                             GroupId = groupId,
                             OrganizationUserId = ou.Id,
                         };
            await dbContext.AddRangeAsync(insert);

            await dbContext.SaveChangesAsync();
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(orgId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> groupIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entities = await dbContext.Groups
                .Where(g => groupIds.Contains(g.Id))
                .ToListAsync();

            dbContext.Groups.RemoveRange(entities);
            await dbContext.SaveChangesAsync();

            foreach (var group in entities.GroupBy(g => g.Organization.Id))
            {
                await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(group.Key);
            }
        }
    }
}
