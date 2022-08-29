using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class GroupRepository : Repository<Core.Entities.Group, Group, Guid>, IGroupRepository
{
    public GroupRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Groups)
    { }

    public async Task CreateAsync(Core.Entities.Group obj, IEnumerable<SelectionReadOnly> collections)
    {
        var grp = await base.CreateAsync(obj);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var availibleCollections = await (
                from c in dbContext.Collections
                where c.OrganizationId == grp.OrganizationId
                select c).ToListAsync();
            var filteredCollections = collections.Where(c => availibleCollections.Any(a => c.Id == a.Id));
            var collectionGroups = filteredCollections.Select(y => new CollectionGroup
            {
                CollectionId = y.Id,
                GroupId = grp.Id,
                ReadOnly = y.ReadOnly,
                HidePasswords = y.HidePasswords,
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
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<Tuple<Core.Entities.Group, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id)
    {
        var grp = await base.GetByIdAsync(id);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = await (
                from cg in dbContext.CollectionGroups
                where cg.GroupId == id
                select cg).ToListAsync();
            var collections = query.Select(c => new SelectionReadOnly
            {
                Id = c.CollectionId,
                ReadOnly = c.ReadOnly,
                HidePasswords = c.HidePasswords,
            }).ToList();
            return new Tuple<Core.Entities.Group, ICollection<SelectionReadOnly>>(
                grp, collections);
        }
    }

    public async Task<ICollection<Core.Entities.Group>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var data = await (
                from g in dbContext.Groups
                where g.OrganizationId == organizationId
                select g).ToListAsync();
            return Mapper.Map<List<Core.Entities.Group>>(data);
        }
    }

    public async Task<ICollection<Core.Entities.GroupUser>> GetManyGroupUsersByOrganizationIdAsync(Guid organizationId)
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
            return Mapper.Map<List<Core.Entities.GroupUser>>(groupUsers);
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

    public async Task<ICollection<Guid>> GetManyUserIdsByIdAsync(Guid id)
    {
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

    public async Task ReplaceAsync(Core.Entities.Group obj, IEnumerable<SelectionReadOnly> collections)
    {
        await base.ReplaceAsync(obj);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await UserBumpAccountRevisionDateByOrganizationId(obj.OrganizationId);
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
            await UserBumpAccountRevisionDateByOrganizationId(orgId);
        }
    }
}
