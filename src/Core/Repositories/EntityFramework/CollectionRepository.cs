using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class CollectionRepository : Repository<TableModel.Collection, EfModel.Collection, Guid>, ICollectionRepository
    {
        public CollectionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Collections)
        { }

        public override async Task<TableModel.Collection> CreateAsync(Collection obj)
        {
            await base.CreateAsync(obj);
            await UserBumpAccountRevisionDateByCollectionId(obj.Id, obj.OrganizationId);
            return obj;
        }

        public async Task CreateAsync(Collection obj, IEnumerable<SelectionReadOnly> groups)
        {
            await base.CreateAsync(obj);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var availibleGroups = await (from g in dbContext.Groups
                    where g.OrganizationId == obj.OrganizationId
                    select g.Id).ToListAsync();
                var collectionGroups = groups
                    .Where(g => availibleGroups.Contains(g.Id))
                    .Select(g => new EfModel.CollectionGroup
                    {
                        CollectionId = obj.Id,
                        GroupId = g.Id,
                        ReadOnly = g.ReadOnly,
                        HidePasswords = g.HidePasswords
                    });
                await dbContext.AddRangeAsync(collectionGroups);
                await dbContext.SaveChangesAsync();
                await UserBumpAccountRevisionDateByOrganizationId(obj.OrganizationId);
            }
        }

        public async Task DeleteUserAsync(Guid collectionId, Guid organizationUserId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from cu in dbContext.CollectionUsers
                    where cu.CollectionId == collectionId &&
                        cu.OrganizationUserId == organizationUserId
                    select cu;
                dbContext.RemoveRange(await query.ToListAsync());
                await dbContext.SaveChangesAsync();
                await UserBumpAccountRevisionDateByOrganizationUserId(organizationUserId);
            }
        }

        public async Task<CollectionDetails> GetByIdAsync(Guid id, Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = new UserCollectionDetailsQuery(userId);
                var collection = await query.Run(dbContext).FirstOrDefaultAsync();
                return collection;
            }
        }

        public async Task<Tuple<Collection, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id)
        {
            var collection =  await base.GetByIdAsync(id);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var collectionGroups = await (from cg in dbContext.CollectionGroups
                    where cg.CollectionId == id
                    select cg).ToListAsync();
                var selectionReadOnlys = collectionGroups.Select(cg => new SelectionReadOnly 
                {
                    Id = cg.GroupId,
                    ReadOnly = cg.ReadOnly,
                    HidePasswords = cg.HidePasswords
                }).ToList();
                return new Tuple<Collection, ICollection<SelectionReadOnly>>(collection, selectionReadOnlys);
            }
        }

        public async Task<Tuple<CollectionDetails, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id, Guid userId)
        {
            var collection = await GetByIdAsync(id, userId);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from cg in dbContext.CollectionGroups
                    where cg.CollectionId.Equals(id)
                    select new SelectionReadOnly 
                    { 
                        Id = cg.GroupId,
                        ReadOnly = cg.ReadOnly,
                        HidePasswords = cg.HidePasswords, 
                    };
                var configurations = await query.ToArrayAsync();
                return new Tuple<CollectionDetails, ICollection<SelectionReadOnly>>(collection, configurations);
            }
        }

        public async Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
        {
            var query = new CollectionReadCountByOrganizationIdQuery(organizationId);
            return await GetCountFromQuery(query);
        }

        public async Task<ICollection<Collection>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from c in dbContext.Collections
                    where c.OrganizationId == organizationId
                    select c;
                var collections = await query.ToArrayAsync();
                return collections;
            }
        }

        public async Task<ICollection<CollectionDetails>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = new UserCollectionDetailsQuery(userId).Run(dbContext);
                var data = await query.ToListAsync();
                return data.GroupBy(c => c.Id).Select(c => c.First()).ToList();
            }
        }

        public async Task<ICollection<SelectionReadOnly>> GetManyUsersByIdAsync(Guid id)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from cu in dbContext.CollectionUsers
                    where cu.CollectionId == id
                    select cu;
                var collectionUsers = await query.ToListAsync();
                return collectionUsers.Select(cu => new SelectionReadOnly 
                {
                    Id = cu.OrganizationUserId,
                    ReadOnly = cu.ReadOnly,
                    HidePasswords = cu.HidePasswords
                }).ToArray();
            }
        }

        public async Task ReplaceAsync(Collection collection, IEnumerable<SelectionReadOnly> groups)
        {
            await base.ReplaceAsync(collection);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var groupsInOrg = dbContext.Groups.Where(g => g.OrganizationId == collection.OrganizationId);
                var modifiedGroupEntities = dbContext.Groups.Where(x => groups.Select(x => x.Id).Contains(x.Id));
                var target = (from cg in dbContext.CollectionGroups
                    join g in modifiedGroupEntities 
                        on cg.CollectionId equals collection.Id into s_g
                    from g in s_g.DefaultIfEmpty()
                    where g == null || cg.GroupId == g.Id
                    select new {cg, g}).AsNoTracking();
                var source = (from g in modifiedGroupEntities
                    from cg in dbContext.CollectionGroups
                        .Where(cg => cg.CollectionId == collection.Id && cg.GroupId == g.Id).DefaultIfEmpty()
                    select new {cg, g}).AsNoTracking();
                var union = await target
                    .Union(source)
                    .Where(x => 
                        x.cg == null ||
                        ((x.g == null || x.g.Id == x.cg.GroupId) && 
                        (x.cg.CollectionId == collection.Id)))
                    .AsNoTracking()
                    .ToListAsync();
                var insert = union.Where(x => x.cg == null && groupsInOrg.Any(c => x.g.Id == c.Id))
                    .Select(x => new EfModel.CollectionGroup 
                    {
                        CollectionId = collection.Id,
                        GroupId = x.g.Id,
                        ReadOnly = groups.FirstOrDefault(g => g.Id == x.g.Id).ReadOnly,
                        HidePasswords = groups.FirstOrDefault(g => g.Id == x.g.Id).HidePasswords
                    }).ToList();
                var update = union
                    .Where(
                        x => x.g != null && 
                        x.cg != null && 
                        (x.cg.ReadOnly != groups.FirstOrDefault(g => g.Id == x.g.Id).ReadOnly || 
                        x.cg.HidePasswords != groups.FirstOrDefault(g => g.Id == x.g.Id).HidePasswords)
                    )
                    .Select(x => new EfModel.CollectionGroup 
                    {
                        CollectionId = collection.Id, 
                        GroupId = x.g.Id,
                        ReadOnly = groups.FirstOrDefault(g => g.Id == x.g.Id).ReadOnly,
                        HidePasswords = groups.FirstOrDefault(g => g.Id == x.g.Id).HidePasswords
                    });
                var delete = union
                    .Where(
                        x => x.g == null && 
                        x.cg.CollectionId == collection.Id
                    )
                    .Select(x => new EfModel.CollectionGroup 
                    { 
                        CollectionId = collection.Id, GroupId = x.cg.GroupId
                    })
                    .ToList();
                
                await dbContext.AddRangeAsync(insert);
                dbContext.UpdateRange(update);
                dbContext.RemoveRange(delete);
                await dbContext.SaveChangesAsync();
                await UserBumpAccountRevisionDateByCollectionId(collection.Id, collection.OrganizationId);
            }
        }

        public async Task UpdateUsersAsync(Guid id, IEnumerable<SelectionReadOnly> users)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var procedure = new CollectionUserUpdateUsersQuery(id, users);
                var updateData = await procedure.Update.BuildInMemory(dbContext);
                dbContext.UpdateRange(updateData);
                var insertData = await procedure.Insert.BuildInMemory(dbContext);
                await dbContext.AddRangeAsync(insertData);
                dbContext.RemoveRange(await procedure.Delete.Run(dbContext).ToListAsync()); 
            }
        }
    }
}
