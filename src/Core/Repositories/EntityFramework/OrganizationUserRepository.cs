using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework.Queries;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class OrganizationUserRepository : Repository<TableModel.OrganizationUser, EfModel.OrganizationUser, Guid>, IOrganizationUserRepository
    {
        public OrganizationUserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationUsers)
        { }

        public async Task CreateAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections)
        {
            var organizationUser = await base.CreateAsync(obj);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var availibleCollections = await (
                    from c in dbContext.Collections
                    where c.OrganizationId == organizationUser.OrganizationId
                    select c).ToListAsync();
                var filteredCollections = collections.Where(c => availibleCollections.Any(a => c.Id == a.Id));
                var collectionUsers = filteredCollections.Select(y => new EfModel.CollectionUser(){
                    CollectionId = y.Id,
                    OrganizationUserId = organizationUser.Id,
                    ReadOnly = y.ReadOnly,
                    HidePasswords = y.HidePasswords
                });
                await dbContext.CollectionUsers.AddRangeAsync(collectionUsers);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<Tuple<OrganizationUser, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id)
        {
            var organizationUser = await base.GetByIdAsync(id);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = await (
                    from ou in dbContext.OrganizationUsers
                    join cu in dbContext.CollectionUsers
                        on ou.Id equals cu.OrganizationUserId
                    where !ou.AccessAll && 
                        ou.Id == id
                    select new { ou, cu }).ToListAsync();
                var collections = query.Select(c => new SelectionReadOnly(){
                    Id = c.cu.CollectionId,
                    ReadOnly = c.cu.ReadOnly,
                    HidePasswords = c.cu.HidePasswords
                }); 
                return new Tuple<OrganizationUser, ICollection<SelectionReadOnly>>(
                    organizationUser, (ICollection<SelectionReadOnly>)collections);
            }
        }

        public Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, Guid userId)
        {
            throw new NotImplementedException();
        }

        public async Task<OrganizationUser> GetByOrganizationEmailAsync(Guid organizationId, string email)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = await GetDbSet(dbContext)
                    .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId &&
                        !string.IsNullOrWhiteSpace(ou.Email) &&
                        ou.Email == email);
                return entity;
            }
        }

        public async Task<int> GetCountByFreeOrganizationAdminUserAsync(Guid userId)
        {
            var query = new OrganizationUserReadCountByFreeOrganizationAdminUser(userId);
            return await GetCountFromQuery(query);
        }

        public async Task<int> GetCountByOnlyOwnerAsync(Guid userId)
        {
            var query = new OrganizationUserReadCountByOnlyOwner(userId);
            return await GetCountFromQuery(query);
        }

        public async Task<int> GetCountByOrganizationAsync(Guid organizationId, string email, bool onlyRegisteredUsers)
        {
            var query = new OrganizationUserReadCountByOrganizationIdEmail(organizationId, email, onlyRegisteredUsers);
            return await GetCountFromQuery(query);
        }

        public async Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
        {
            var query = new OrganizationUserReadCountByOrganizationId(organizationId);
            return await GetCountFromQuery(query);
        }

        public async Task<OrganizationUserUserDetails> GetDetailsByIdAsync(Guid id)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var view = new OrganizationUserUserDetailsView();
                var entity = await view.Run(dbContext).FirstOrDefaultAsync(ou => ou.Id == id);
                return entity;
            }
        }

        public async Task<Tuple<OrganizationUserUserDetails, ICollection<SelectionReadOnly>>> GetDetailsByIdWithCollectionsAsync(Guid id)
        {
            var organizationUserUserDetails = await GetDetailsByIdAsync(id);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from ou in dbContext.OrganizationUsers
                            join cu in dbContext.CollectionUsers on ou.Id equals cu.OrganizationUserId
                            where !ou.AccessAll && ou.Id == id
                            select new {ou, cu};
                var collections = await query.Select(x => new SelectionReadOnly(){
                   Id = x.cu.CollectionId,
                   ReadOnly = x.cu.ReadOnly,
                   HidePasswords = x.cu.HidePasswords
                }).ToListAsync();
                return new Tuple<OrganizationUserUserDetails, ICollection<SelectionReadOnly>>(organizationUserUserDetails, collections);
            }
        }

        public async Task<OrganizationUserOrganizationDetails> GetDetailsByUserAsync(Guid userId, Guid organizationId, OrganizationUserStatusType? status = null)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var view = new OrganizationUserOrganizationDetailsView();
                var entity = await view.Run(dbContext)
                    .FirstOrDefaultAsync(o => o.UserId == userId && 
                        o.OrganizationId == organizationId && 
                        o.Status == status);
                return entity;
            }
        }

        public async Task<ICollection<OrganizationUser>> GetManyByManyUsersAsync(IEnumerable<Guid> userIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from ou in dbContext.OrganizationUsers
                            where userIds.Contains(ou.Id)
                            select ou;
                return Mapper.Map<List<TableModel.OrganizationUser>>(await query.ToListAsync());
            }
        }

        public async Task<ICollection<OrganizationUser>> GetManyByOrganizationAsync(Guid organizationId, OrganizationUserType? type)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from ou in dbContext.OrganizationUsers
                            where ou.OrganizationId == organizationId &&
                                (type == null || ou.Type == type)
                            select ou;
                return Mapper.Map<List<TableModel.OrganizationUser>>(await query.ToListAsync());
            }
        }

        public async Task<ICollection<OrganizationUser>> GetManyByUserAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from ou in dbContext.OrganizationUsers
                            where ou.UserId == userId
                            select ou;
                return Mapper.Map<List<TableModel.OrganizationUser>>(await query.ToListAsync());
            }
        }

        public async Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var view = new OrganizationUserUserDetailsView();
                var query = from ou in view.Run(dbContext)
                            where ou.OrganizationId == organizationId
                            select ou;
                return await query.ToListAsync();
            }
        }

        public async Task<ICollection<OrganizationUserOrganizationDetails>> GetManyDetailsByUserAsync(Guid userId,
                OrganizationUserStatusType? status = null)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var view = new OrganizationUserOrganizationDetailsView();
                var query = from ou in view.Run(dbContext)
                            where ou.UserId == userId &&
                            (status == null || ou.Status == status)
                            select ou;
                var organizationUsers = await query.ToListAsync();
                return organizationUsers;
            }
        }

        public async Task ReplaceAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections)
        {
            await base.ReplaceAsync(obj);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);

                var procedure = new OrganizationUserUpdateWithCollections(obj, collections);

                var update = procedure.Update.Run(dbContext);
                dbContext.UpdateRange(await update.ToListAsync());

                var insert = procedure.Insert.Run(dbContext);
                await dbContext.AddRangeAsync(await insert.ToListAsync());

                dbContext.RemoveRange(await procedure.Delete.Run(dbContext).ToListAsync()); 
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task UpdateGroupsAsync(Guid orgUserId, IEnumerable<Guid> groupIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);

                var procedure = new GroupUserUpdateGroups(orgUserId, groupIds);

                var insert = procedure.Insert.Run(dbContext);
                await dbContext.AddRangeAsync(await insert.ToListAsync());

                dbContext.RemoveRange(await procedure.Delete.Run(dbContext).ToListAsync()); 
                // bumpaccountrevisiondatebyorganizationuserid
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
