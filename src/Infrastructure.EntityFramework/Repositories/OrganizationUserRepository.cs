using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OrganizationUserRepository : Repository<Core.Entities.OrganizationUser, OrganizationUser, Guid>, IOrganizationUserRepository
{
    public OrganizationUserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationUsers)
    { }

    public async Task<Guid> CreateAsync(Core.Entities.OrganizationUser obj, IEnumerable<SelectionReadOnly> collections)
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
            var collectionUsers = filteredCollections.Select(y => new CollectionUser
            {
                CollectionId = y.Id,
                OrganizationUserId = organizationUser.Id,
                ReadOnly = y.ReadOnly,
                HidePasswords = y.HidePasswords,
            });
            await dbContext.CollectionUsers.AddRangeAsync(collectionUsers);
            await dbContext.SaveChangesAsync();
        }

        return organizationUser.Id;
    }

    public async Task<ICollection<Guid>> CreateManyAsync(IEnumerable<Core.Entities.OrganizationUser> organizationUsers)
    {
        if (!organizationUsers.Any())
        {
            return new List<Guid>();
        }

        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.SetNewId();
        }

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entities = Mapper.Map<List<OrganizationUser>>(organizationUsers);
            await dbContext.AddRangeAsync(entities);
            await dbContext.SaveChangesAsync();
        }

        return organizationUsers.Select(u => u.Id).ToList();
    }

    public override async Task DeleteAsync(Core.Entities.OrganizationUser organizationUser) => await DeleteAsync(organizationUser.Id);
    public async Task DeleteAsync(Guid organizationUserId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgUser = await dbContext.FindAsync<OrganizationUser>(organizationUserId);

            dbContext.Remove(orgUser);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> organizationUserIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entities = await dbContext.OrganizationUsers
                .Where(ou => organizationUserIds.Contains(ou.Id))
                .ToListAsync();

            dbContext.OrganizationUsers.RemoveRange(entities);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<Tuple<Core.Entities.OrganizationUser, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id)
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
                HidePasswords = cu.HidePasswords,
            });
            return new Tuple<Core.Entities.OrganizationUser, ICollection<SelectionReadOnly>>(
                organizationUser, collections.ToList());
        }
    }

    public async Task<Core.Entities.OrganizationUser> GetByOrganizationAsync(Guid organizationId, Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await GetDbSet(dbContext)
                .FirstOrDefaultAsync(e => e.OrganizationId == organizationId && e.UserId == userId);
            return entity;
        }
    }

    public async Task<Core.Entities.OrganizationUser> GetByOrganizationEmailAsync(Guid organizationId, string email)
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
                HidePasswords = cu.HidePasswords,
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

    public async Task<ICollection<Core.Entities.OrganizationUser>> GetManyAsync(IEnumerable<Guid> Ids)
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

    public async Task<ICollection<Core.Entities.OrganizationUser>> GetManyByManyUsersAsync(IEnumerable<Guid> userIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from ou in dbContext.OrganizationUsers
                        where userIds.Contains(ou.Id)
                        select ou;
            return Mapper.Map<List<Core.Entities.OrganizationUser>>(await query.ToListAsync());
        }
    }

    public async Task<ICollection<Core.Entities.OrganizationUser>> GetManyByOrganizationAsync(Guid organizationId, OrganizationUserType? type)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from ou in dbContext.OrganizationUsers
                        where ou.OrganizationId == organizationId &&
                            (type == null || ou.Type == type)
                        select ou;
            return Mapper.Map<List<Core.Entities.OrganizationUser>>(await query.ToListAsync());
        }
    }

    public async Task<ICollection<Core.Entities.OrganizationUser>> GetManyByUserAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from ou in dbContext.OrganizationUsers
                        where ou.UserId == userId
                        select ou;
            return Mapper.Map<List<Core.Entities.OrganizationUser>>(await query.ToListAsync());
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
                    PublicKey = x.u.PublicKey,
                }).ToListAsync();
            return data;
        }
    }

    public async Task ReplaceAsync(Core.Entities.OrganizationUser obj, IEnumerable<SelectionReadOnly> requestedCollections)
    {
        await base.ReplaceAsync(obj);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var existingCollectionUsers = await dbContext.CollectionUsers
                .Where(cu => cu.OrganizationUserId == obj.Id)
                .ToListAsync();

            foreach (var requestedCollection in requestedCollections)
            {
                var existingCollectionUser = existingCollectionUsers.FirstOrDefault(cu => cu.CollectionId == requestedCollection.Id);
                if (existingCollectionUser == null)
                {
                    // This is a brand new entry
                    dbContext.CollectionUsers.Add(new CollectionUser
                    {
                        CollectionId = requestedCollection.Id,
                        OrganizationUserId = obj.Id,
                        HidePasswords = requestedCollection.HidePasswords,
                        ReadOnly = requestedCollection.ReadOnly,
                    });
                    continue;
                }

                // It already exists, update it
                existingCollectionUser.HidePasswords = requestedCollection.HidePasswords;
                existingCollectionUser.ReadOnly = requestedCollection.ReadOnly;
                dbContext.CollectionUsers.Update(existingCollectionUser);
            }

            // Remove all existing ones that are no longer requested
            var requestedCollectionIds = requestedCollections.Select(c => c.Id).ToList();
            dbContext.CollectionUsers.RemoveRange(existingCollectionUsers.Where(cu => !requestedCollectionIds.Contains(cu.CollectionId)));
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task ReplaceManyAsync(IEnumerable<Core.Entities.OrganizationUser> organizationUsers)
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

    public async Task<ICollection<string>> SelectKnownEmailsAsync(Guid organizationId, IEnumerable<string> emails, bool onlyRegisteredUsers)
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
            return knownEmails.ToList();
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

    public async Task UpsertManyAsync(IEnumerable<Core.Entities.OrganizationUser> organizationUsers)
    {
        var createUsers = new List<Core.Entities.OrganizationUser>();
        var replaceUsers = new List<Core.Entities.OrganizationUser>();
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

    public async Task<IEnumerable<OrganizationUserUserDetails>> GetManyByMinimumRoleAsync(Guid organizationId, OrganizationUserType minRole)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.OrganizationUsers
                .Include(e => e.User)
                .Where(e => e.OrganizationId.Equals(organizationId) &&
                    e.Type <= minRole &&
                    e.Status == OrganizationUserStatusType.Confirmed)
                .Select(e => new OrganizationUserUserDetails()
                {
                    Id = e.Id,
                    Email = e.Email ?? e.User.Email
                });
            return await query.ToListAsync();
        }
    }

    public async Task RevokeAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgUser = await GetDbSet(dbContext).FindAsync(id);
            if (orgUser != null)
            {
                dbContext.Update(orgUser);
                orgUser.Status = OrganizationUserStatusType.Revoked;
                await dbContext.SaveChangesAsync();
                if (orgUser.UserId.HasValue)
                {
                    await UserBumpAccountRevisionDate(orgUser.UserId.Value);
                }
            }
        }
    }

    public async Task RestoreAsync(Guid id, OrganizationUserStatusType status)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgUser = await GetDbSet(dbContext).FindAsync(id);
            if (orgUser != null)
            {
                dbContext.Update(orgUser);
                orgUser.Status = status;
                await dbContext.SaveChangesAsync();
                if (orgUser.UserId.HasValue)
                {
                    await UserBumpAccountRevisionDate(orgUser.UserId.Value);
                }
            }
        }
    }
}
