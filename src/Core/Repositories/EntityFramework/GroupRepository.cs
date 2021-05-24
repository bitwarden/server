using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class GroupRepository : Repository<TableModel.Group, EfModel.Group, Guid>, IGroupRepository
    {
        public GroupRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Groups)
        { }

        public async Task CreateAsync(Group obj, IEnumerable<SelectionReadOnly> collections)
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
                var collectionGroups = filteredCollections.Select(y => new EfModel.CollectionGroup(){
                    CollectionId = y.Id,
                    GroupId = grp.Id,
                    ReadOnly = y.ReadOnly,
                    HidePasswords = y.HidePasswords
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

        public async Task<Tuple<Group, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id)
        {
            var grp = await base.GetByIdAsync(id);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = await ( 
                    from cg in dbContext.CollectionGroups
                    where cg.GroupId == id
                    select cg).ToListAsync();
                var collections = query.Select(c => new SelectionReadOnly(){
                    Id = c.CollectionId,
                    ReadOnly = c.ReadOnly,
                    HidePasswords = c.HidePasswords
                }).ToList(); 
                return new Tuple<Group, ICollection<SelectionReadOnly>>(
                    grp, collections);
            }
        }

        public async Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var data = await (
                    from g in dbContext.Groups
                    where g.OrganizationId == organizationId
                    select g).ToListAsync();
                return Mapper.Map<List<TableModel.Group>>(data);
            }
        }

        public async Task<ICollection<GroupUser>> GetManyGroupUsersByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query =
                    from gu in dbContext.GroupUsers
                    join g in dbContext.Groups
                        on gu.GroupId equals g.Id
                    where g.OrganizationId == organizationId
                    select new { gu, g };
                var groupUsers = await query.Select(x => x.gu).ToListAsync();
                return Mapper.Map<List<TableModel.GroupUser>>(groupUsers);
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

        public async Task ReplaceAsync(Group obj, IEnumerable<SelectionReadOnly> collections)
        {
            await base.ReplaceAsync(obj);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                // my brain is broken looking at this proc
                /* EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId */
            }
        }

        public Task UpdateUsersAsync(Guid groupId, IEnumerable<Guid> organizationUserIds)
        {
            throw new NotImplementedException();
        }
    }
}
