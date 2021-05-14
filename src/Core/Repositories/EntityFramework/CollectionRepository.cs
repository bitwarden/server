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
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                // TODO: User_BumpAccountRevisionDateByCollectionId
            }
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
                    .Select(g => new CollectionGroup(){
                        CollectionId = obj.Id,
                        GroupId = g.Id,
                        ReadOnly = g.ReadOnly,
                        HidePasswords = g.HidePasswords
                    });
                await dbContext.AddRangeAsync(collectionGroups);
                // TODO: User_BumpAccountRevisionDateByOrganizationId
                await dbContext.SaveChangesAsync();
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
                // TODO: User_BumpAccountRevisionDateByOrganizationUserId
                await dbContext.SaveChangesAsync();
            }
        }

        public Task<CollectionDetails> GetByIdAsync(Guid id, Guid userId)
        {
            // TODO: UserCollectionDetails function
            throw new NotImplementedException();
        }

        public async Task<Tuple<Collection, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id)
        {
            var collection =  await base.GetByIdAsync(id);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var collectionGroups = await (
                    from cg in dbContext.CollectionGroups
                    where cg.CollectionId == id
                    select cg).ToListAsync();
                var selectionReadOnlys = collectionGroups.Select(cg => new SelectionReadOnly() {
                    Id = cg.GroupId,
                    ReadOnly = cg.ReadOnly,
                    HidePasswords = cg.HidePasswords
                }).ToList();
                return new Tuple<Collection, ICollection<SelectionReadOnly>>(collection, selectionReadOnlys);
            }
        }

        public Task<Tuple<CollectionDetails, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id, Guid userId)
        {
            // TODO: UserCollectionDetails function
            throw new NotImplementedException();
        }

        public async Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
        {
            var query = new CollectionReadCountByOrganizationId(organizationId);
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

        public Task<ICollection<CollectionDetails>> GetManyByUserIdAsync(Guid userId)
        {
            // TODO: UserCollectionDetails function
            throw new NotImplementedException();
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
                return collectionUsers.Select(cu => new SelectionReadOnly() {
                    Id = cu.OrganizationUserId,
                    ReadOnly = cu.ReadOnly,
                    HidePasswords = cu.HidePasswords
                }).ToArray();
            }
        }

        public async Task ReplaceAsync(Collection obj, IEnumerable<SelectionReadOnly> groups)
        {
            await base.ReplaceAsync(obj);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                // TODO: Collection_UpdateWithGroups
                // TODO: User_BumpAccountRevisionDateByCollectionId
            }
        }

        public async Task UpdateUsersAsync(Guid id, IEnumerable<SelectionReadOnly> users)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);

                var procedure = new CollectionUserUpdateUsers(id, users);

                var update = procedure.Update.Run(dbContext);
                dbContext.UpdateRange(await update.ToListAsync());

                var insert = procedure.Insert.Run(dbContext);
                await dbContext.AddRangeAsync(await insert.ToListAsync());

                dbContext.RemoveRange(await procedure.Delete.Run(dbContext).ToListAsync()); 
            }
        }
    }
}
