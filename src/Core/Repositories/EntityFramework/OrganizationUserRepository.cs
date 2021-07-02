using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework.Queries;
using EfModel = Bit.Core.Models.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
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
                var collectionUsers = filteredCollections.Select(y => new EfModel.CollectionUser
                {
                    CollectionId = y.Id,
                    OrganizationUserId = organizationUser.Id,
                    ReadOnly = y.ReadOnly,
                    HidePasswords = y.HidePasswords
                });
                await dbContext.CollectionUsers.AddRangeAsync(collectionUsers);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task CreateManyAsync(IEnumerable<OrganizationUser> organizationUsers)
        {
            if (!organizationUsers.Any())
            {
                return;
            }

            foreach (var organizationUser in organizationUsers)
            {
                organizationUser.SetNewId();
            }

            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entities = Mapper.Map<List<EfModel.OrganizationUser>>(organizationUsers);
                await dbContext.AddRangeAsync(entities);
            }
        }

        public async Task DeleteManyAsync(IEnumerable<Guid> organizationUserIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entities = dbContext.FindAsync<EfModel.OrganizationUser>(organizationUserIds);
                dbContext.RemoveRange(entities);
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
                    select cu).ToListAsync();
                var collections = query.Select(cu => new SelectionReadOnly
                {
                    Id = cu.CollectionId,
                    ReadOnly = cu.ReadOnly,
                    HidePasswords = cu.HidePasswords
                }); 
                return new Tuple<OrganizationUser, ICollection<SelectionReadOnly>>(
                    organizationUser, collections.ToList());
            }
        }

        public async Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = await GetDbSet(dbContext)
                    .FirstOrDefaultAsync(e => e.OrganizationId == organizationId && e.UserId == userId);
                return entity;
            }
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
            var query = new OrganizationUserReadCountByFreeOrganizationAdminUserQuery(userId);
            return await GetCountFromQuery(query);
        }

        public async Task<int> GetCountByOnlyOwnerAsync(Guid userId)
        {
            var query = new OrganizationUserReadCountByOnlyOwnerQuery(userId);
            return await GetCountFromQuery(query);
        }

        public async Task<int> GetCountByOrganizationAsync(Guid organizationId, string email, bool onlyRegisteredUsers)
        {
            var query = new OrganizationUserReadCountByOrganizationIdEmailQuery(organizationId, email, onlyRegisteredUsers);
            return await GetCountFromQuery(query);
        }

        public async Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
        {
            var query = new OrganizationUserReadCountByOrganizationIdQuery(organizationId);
            return await GetCountFromQuery(query);
        }

        public async Task<OrganizationUserUserDetails> GetDetailsByIdAsync(Guid id)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var view = new OrganizationUserUserDetailsViewQuery();
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
                    select cu;
                var collections = await query.Select(cu => new SelectionReadOnly
                {
                   Id = cu.CollectionId,
                   ReadOnly = cu.ReadOnly,
                   HidePasswords = cu.HidePasswords
                }).ToListAsync();
                return new Tuple<OrganizationUserUserDetails, ICollection<SelectionReadOnly>>(organizationUserUserDetails, collections);
            }
        }

        public async Task<OrganizationUserOrganizationDetails> GetDetailsByUserAsync(Guid userId, Guid organizationId, OrganizationUserStatusType? status = null)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var view = new OrganizationUserOrganizationDetailsViewQuery();
                var t = await (view.Run(dbContext)).ToArrayAsync();
                var entity = await view.Run(dbContext)
                    .FirstOrDefaultAsync(o => o.UserId == userId && 
                        o.OrganizationId == organizationId && 
                        (status == null || o.Status == status));
                return entity;
            }
        }

        public async Task<ICollection<OrganizationUser>> GetManyAsync(IEnumerable<Guid> Ids)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from ou in dbContext.OrganizationUsers
                    where Ids.Contains(ou.Id)
                    select ou;
                var data = await query.ToArrayAsync();
                return data;
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
                var view = new OrganizationUserUserDetailsViewQuery();
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
                var view = new OrganizationUserOrganizationDetailsViewQuery();
                var query = from ou in view.Run(dbContext)
                    where ou.UserId == userId &&
                    (status == null || ou.Status == status)
                    select ou;
                var organizationUsers = await query.ToListAsync();
                return organizationUsers;
            }
        }

        public async Task<IEnumerable<OrganizationUserPublicKey>> GetManyPublicKeysByOrganizationUserAsync(Guid organizationId, IEnumerable<Guid> Ids)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from ou in dbContext.OrganizationUsers
                    where Ids.Contains(ou.Id) && ou.Status == OrganizationUserStatusType.Accepted
                    join u in dbContext.Users
                        on ou.UserId equals u.Id
                    where ou.OrganizationId == organizationId
                    select new { ou, u };
                var data = await query
                    .Select(x => new OrganizationUserPublicKey() 
                    {
                       Id = x.ou.Id,
                       PublicKey = x.u.PublicKey
                    }).ToListAsync();
                return data;
            }
        }

        public async Task ReplaceAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections)
        {
            await base.ReplaceAsync(obj);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);

                var procedure = new OrganizationUserUpdateWithCollectionsQuery(obj, collections);

                var update = procedure.Update.Run(dbContext);
                dbContext.UpdateRange(await update.ToListAsync());

                var insert = procedure.Insert.Run(dbContext);
                await dbContext.AddRangeAsync(await insert.ToListAsync());

                dbContext.RemoveRange(await procedure.Delete.Run(dbContext).ToListAsync()); 
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task ReplaceManyAsync(IEnumerable<OrganizationUser> organizationUsers)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                dbContext.UpdateRange(organizationUsers);
                await dbContext.SaveChangesAsync();
                await UserBumpManyAccountRevisionDates(organizationUsers
                    .Where(ou => ou.UserId.HasValue)
                    .Select(ou => ou.UserId.Value).ToArray());
            }
        }

        public async Task<IEnumerable<string>> SelectKnownEmailsAsync(Guid organizationId, IEnumerable<string> emails, bool onlyRegisteredUsers)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var usersQuery = from ou in dbContext.OrganizationUsers
                    join u in dbContext.Users
                        on ou.UserId equals u.Id into u_g
                    from u in u_g
                    where ou.OrganizationId == organizationId
                    select new { ou, u };
                var ouu = await usersQuery.ToListAsync();
                var ouEmails = ouu.Select(x => x.ou.Email);
                var uEmails = ouu.Select(x => x.u.Email);
                var knownEmails = from e in emails
                    where (ouEmails.Contains(e) || uEmails.Contains(e)) &&
                    (!onlyRegisteredUsers && (uEmails.Contains(e) || ouEmails.Contains(e))) ||
                    (onlyRegisteredUsers && uEmails.Contains(e))
                    select e;
                return knownEmails;
            }
        }

        public async Task UpdateGroupsAsync(Guid orgUserId, IEnumerable<Guid> groupIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);

                var procedure = new GroupUserUpdateGroupsQuery(orgUserId, groupIds);

                var insert = procedure.Insert.Run(dbContext);
                var data = await insert.ToListAsync();
                await dbContext.AddRangeAsync(data);

                var delete = procedure.Delete.Run(dbContext);
                var deleteData = await delete.ToListAsync();
                dbContext.RemoveRange(deleteData); 
                await UserBumpAccountRevisionDateByOrganizationUserId(orgUserId);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task UpsertManyAsync(IEnumerable<OrganizationUser> organizationUsers)
        {
            var createUsers = new List<OrganizationUser>();
            var replaceUsers = new List<OrganizationUser>();
            foreach (var organizationUser in organizationUsers)
            {
                if (organizationUser.Id.Equals(default))
                {
                    createUsers.Add(organizationUser);
                }
                else
                {
                    replaceUsers.Add(organizationUser);
                }
            }

            await CreateManyAsync(createUsers);
            await ReplaceManyAsync(replaceUsers);
        }

        Task<ICollection<string>> IOrganizationUserRepository.SelectKnownEmailsAsync(Guid organizationId, IEnumerable<string> emails, bool onlyRegisteredUsers) => throw new NotImplementedException();
    }
}
