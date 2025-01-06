using AutoMapper;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.UserKey;
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

    public async Task<Guid> CreateAsync(Core.Entities.OrganizationUser obj, IEnumerable<CollectionAccessSelection> collections)
    {
        var organizationUser = await base.CreateAsync(obj);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var availableCollections = await (
                from c in dbContext.Collections
                where c.OrganizationId == organizationUser.OrganizationId
                select c).ToListAsync();
            var filteredCollections = collections.Where(c => availableCollections.Any(a => c.Id == a.Id));
            var collectionUsers = filteredCollections.Select(y => new CollectionUser
            {
                CollectionId = y.Id,
                OrganizationUserId = organizationUser.Id,
                ReadOnly = y.ReadOnly,
                HidePasswords = y.HidePasswords,
                Manage = y.Manage
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
            await dbContext.UserBumpAccountRevisionDateByOrganizationUserIdAsync(organizationUserId);
            var orgUser = await dbContext.OrganizationUsers
                .Where(ou => ou.Id == organizationUserId)
                .FirstAsync();

            var organizationId = orgUser?.OrganizationId;
            var userId = orgUser?.UserId;

            if (orgUser?.OrganizationId != null && orgUser?.UserId != null)
            {
                var ssoUsers = dbContext.SsoUsers
                    .Where(su => su.UserId == userId && su.OrganizationId == organizationId);
                dbContext.SsoUsers.RemoveRange(ssoUsers);
            }

            var collectionUsers = dbContext.CollectionUsers
                .Where(cu => cu.OrganizationUserId == organizationUserId);
            dbContext.CollectionUsers.RemoveRange(collectionUsers);

            var groupUsers = dbContext.GroupUsers
                .Where(gu => gu.OrganizationUserId == organizationUserId);
            dbContext.GroupUsers.RemoveRange(groupUsers);

            dbContext.UserProjectAccessPolicy.RemoveRange(
                dbContext.UserProjectAccessPolicy.Where(ap => ap.OrganizationUserId == organizationUserId));
            dbContext.UserServiceAccountAccessPolicy.RemoveRange(
                dbContext.UserServiceAccountAccessPolicy.Where(ap => ap.OrganizationUserId == organizationUserId));
            dbContext.UserSecretAccessPolicy.RemoveRange(
                dbContext.UserSecretAccessPolicy.Where(ap => ap.OrganizationUserId == organizationUserId));

            var orgSponsorships = await dbContext.OrganizationSponsorships
                .Where(os => os.SponsoringOrganizationUserId == organizationUserId)
                .ToListAsync();

            foreach (var orgSponsorship in orgSponsorships)
            {
                orgSponsorship.ToDelete = true;
            }

            dbContext.OrganizationUsers.Remove(orgUser);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> organizationUserIds)
    {
        var targetOrganizationUserIds = organizationUserIds.ToList();
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var transaction = await dbContext.Database.BeginTransactionAsync();
        await dbContext.UserBumpAccountRevisionDateByOrganizationUserIdsAsync(targetOrganizationUserIds);

        await dbContext.CollectionUsers
            .Where(cu => targetOrganizationUserIds.Contains(cu.OrganizationUserId))
            .ExecuteDeleteAsync();

        await dbContext.GroupUsers
            .Where(gu => targetOrganizationUserIds.Contains(gu.OrganizationUserId))
            .ExecuteDeleteAsync();

        await dbContext.UserProjectAccessPolicy
            .Where(ap => targetOrganizationUserIds.Contains(ap.OrganizationUserId!.Value))
            .ExecuteDeleteAsync();
        await dbContext.UserServiceAccountAccessPolicy
            .Where(ap => targetOrganizationUserIds.Contains(ap.OrganizationUserId!.Value))
            .ExecuteDeleteAsync();
        await dbContext.UserSecretAccessPolicy
            .Where(ap => targetOrganizationUserIds.Contains(ap.OrganizationUserId!.Value))
            .ExecuteDeleteAsync();

        await dbContext.OrganizationUsers
            .Where(ou => targetOrganizationUserIds.Contains(ou.Id)).ExecuteDeleteAsync();

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<Tuple<Core.Entities.OrganizationUser, ICollection<CollectionAccessSelection>>> GetByIdWithCollectionsAsync(Guid id)
    {
        var organizationUser = await base.GetByIdAsync(id);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = await (
                from ou in dbContext.OrganizationUsers
                join cu in dbContext.CollectionUsers
                    on ou.Id equals cu.OrganizationUserId
                where ou.Id == id
                select cu).ToListAsync();
            var collections = query.Select(cu => new CollectionAccessSelection
            {
                Id = cu.CollectionId,
                ReadOnly = cu.ReadOnly,
                HidePasswords = cu.HidePasswords,
                Manage = cu.Manage,
            });
            return new Tuple<Core.Entities.OrganizationUser, ICollection<CollectionAccessSelection>>(
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
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await dbContext.OrganizationUsers
                .Where(ou => ou.Type == OrganizationUserType.Owner && ou.Status == OrganizationUserStatusType.Confirmed)
                .GroupBy(ou => ou.UserId)
                .Select(g => new { UserId = g.Key, ConfirmedOwnerCount = g.Count() })
                .Where(oc => oc.UserId == userId && oc.ConfirmedOwnerCount == 1)
                .CountAsync();
        }
    }

    public async Task<int> GetCountByOrganizationAsync(Guid organizationId, string email, bool onlyRegisteredUsers)
    {
        var query = new OrganizationUserReadCountByOrganizationIdEmailQuery(organizationId, email, onlyRegisteredUsers);
        return await GetCountFromQuery(query);
    }

    public async Task<int> GetOccupiedSeatCountByOrganizationIdAsync(Guid organizationId)
    {
        var query = new OrganizationUserReadOccupiedSeatCountByOrganizationIdQuery(organizationId);
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

    public async Task<(OrganizationUserUserDetails? OrganizationUser, ICollection<CollectionAccessSelection> Collections)> GetDetailsByIdWithCollectionsAsync(Guid id)
    {
        var organizationUserUserDetails = await GetDetailsByIdAsync(id);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from ou in dbContext.OrganizationUsers
                        join cu in dbContext.CollectionUsers on ou.Id equals cu.OrganizationUserId
                        where ou.Id == id
                        select cu;
            var collections = await query.Select(cu => new CollectionAccessSelection
            {
                Id = cu.CollectionId,
                ReadOnly = cu.ReadOnly,
                HidePasswords = cu.HidePasswords,
                Manage = cu.Manage
            }).ToListAsync();
            return (organizationUserUserDetails, collections);
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

    public async Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync(Guid organizationId, bool includeGroups, bool includeCollections)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var view = new OrganizationUserUserDetailsViewQuery();
            var users = await (from ou in view.Run(dbContext)
                               where ou.OrganizationId == organizationId
                               select ou).ToListAsync();

            if (!includeCollections && !includeGroups)
            {
                return users;
            }

            List<IGrouping<Guid, GroupUser>> groups = null;
            List<IGrouping<Guid, CollectionUser>> collections = null;
            var userIds = users.Select(u => u.Id);
            var userIdEntities = dbContext.OrganizationUsers.Where(x => userIds.Contains(x.Id));

            // Query groups/collections separately to avoid cartesian explosion
            if (includeGroups)
            {
                groups = (await (from gu in dbContext.GroupUsers
                                 join ou in userIdEntities on gu.OrganizationUserId equals ou.Id
                                 select gu).ToListAsync())
                    .GroupBy(g => g.OrganizationUserId).ToList();
            }

            if (includeCollections)
            {
                collections = (await (from cu in dbContext.CollectionUsers
                                      join ou in userIdEntities on cu.OrganizationUserId equals ou.Id
                                      select cu).ToListAsync())
                    .GroupBy(c => c.OrganizationUserId).ToList();
            }

            // Map any queried collections and groups to their respective users
            foreach (var user in users)
            {
                if (groups != null)
                {
                    user.Groups = groups
                        .FirstOrDefault(g => g.Key == user.Id)?
                        .Select(g => g.GroupId).ToList() ?? new List<Guid>();
                }

                if (collections != null)
                {
                    user.Collections = collections
                        .FirstOrDefault(c => c.Key == user.Id)?
                        .Select(cu => new CollectionAccessSelection
                        {
                            Id = cu.CollectionId,
                            ReadOnly = cu.ReadOnly,
                            HidePasswords = cu.HidePasswords,
                            Manage = cu.Manage,
                        }).ToList() ?? new List<CollectionAccessSelection>();
                }
            }

            return users;
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

    public async override Task ReplaceAsync(Core.Entities.OrganizationUser organizationUser)
    {
        await base.ReplaceAsync(organizationUser);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateAsync(organizationUser.UserId.GetValueOrDefault());
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task ReplaceAsync(Core.Entities.OrganizationUser obj, IEnumerable<CollectionAccessSelection> requestedCollections)
    {
        await ReplaceAsync(obj);
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
                        Manage = requestedCollection.Manage
                    });
                    continue;
                }

                // It already exists, update it
                existingCollectionUser.HidePasswords = requestedCollection.HidePasswords;
                existingCollectionUser.ReadOnly = requestedCollection.ReadOnly;
                existingCollectionUser.Manage = requestedCollection.Manage;
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
            await dbContext.UserBumpManyAccountRevisionDatesAsync(organizationUsers
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
            await dbContext.UserBumpAccountRevisionDateByOrganizationUserIdAsync(orgUserId);
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
            var orgUser = await dbContext.OrganizationUsers.FindAsync(id);
            if (orgUser == null)
            {
                return;
            }

            orgUser.Status = OrganizationUserStatusType.Revoked;
            await dbContext.UserBumpAccountRevisionDateByOrganizationUserIdAsync(id);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task RestoreAsync(Guid id, OrganizationUserStatusType status)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgUser = await dbContext.OrganizationUsers
                .FirstOrDefaultAsync(ou => ou.Id == id && ou.Status == OrganizationUserStatusType.Revoked);

            if (orgUser == null)
            {
                return;
            }

            orgUser.Status = status;
            await dbContext.UserBumpAccountRevisionDateByOrganizationUserIdAsync(id);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<OrganizationUserPolicyDetails>> GetByUserIdWithPolicyDetailsAsync(Guid userId, PolicyType policyType)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var providerOrganizations = from pu in dbContext.ProviderUsers
                                        where pu.UserId == userId
                                        join po in dbContext.ProviderOrganizations
                                            on pu.ProviderId equals po.ProviderId
                                        select po;

            var query = from p in dbContext.Policies
                        join ou in dbContext.OrganizationUsers
                            on p.OrganizationId equals ou.OrganizationId
                        let email = dbContext.Users.Find(userId).Email  // Invited orgUsers do not have a UserId associated with them, so we have to match up their email
                        where p.Type == policyType &&
                            (ou.UserId == userId || ou.Email == email)
                        select new OrganizationUserPolicyDetails
                        {
                            OrganizationUserId = ou.Id,
                            OrganizationId = p.OrganizationId,
                            PolicyType = p.Type,
                            PolicyEnabled = p.Enabled,
                            PolicyData = p.Data,
                            OrganizationUserType = ou.Type,
                            OrganizationUserStatus = ou.Status,
                            OrganizationUserPermissionsData = ou.Permissions,
                            IsProvider = providerOrganizations.Any(po => po.OrganizationId == p.OrganizationId)
                        };
            return await query.ToListAsync();
        }
    }

    public async Task<int> GetOccupiedSmSeatCountByOrganizationIdAsync(Guid organizationId)
    {
        var query = new OrganizationUserReadOccupiedSmSeatCountByOrganizationIdQuery(organizationId);
        return await GetCountFromQuery(query);
    }

    public async Task<IEnumerable<OrganizationUserResetPasswordDetails>>
        GetManyAccountRecoveryDetailsByOrganizationUserAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from ou in dbContext.OrganizationUsers
                        where organizationUserIds.Contains(ou.Id)
                        join u in dbContext.Users
                            on ou.UserId equals u.Id
                        join o in dbContext.Organizations
                            on ou.OrganizationId equals o.Id
                        where ou.OrganizationId == organizationId
                        select new { ou, u, o };
            var data = await query
                .Select(x => new OrganizationUserResetPasswordDetails(x.ou, x.u, x.o)).ToListAsync();
            return data;
        }
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(
        Guid userId, IEnumerable<Core.Entities.OrganizationUser> resetPasswordKeys)
    {
        return async (_, _) =>
        {
            var newOrganizationUsers = resetPasswordKeys.ToList();
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetDatabaseContext(scope);

            // Get user organization users
            var userOrganizationUsers = await GetDbSet(dbContext)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            // Filter to only organization users that are included
            var validOrganizationUsers = userOrganizationUsers
                .Where(organizationUser =>
                    newOrganizationUsers.Any(newOrganizationUser => newOrganizationUser.Id == organizationUser.Id));

            foreach (var organizationUser in validOrganizationUsers)
            {
                var updateOrganizationUser =
                    newOrganizationUsers.First(newOrganizationUser => newOrganizationUser.Id == organizationUser.Id);
                organizationUser.ResetPasswordKey = updateOrganizationUser.ResetPasswordKey;
            }

            await dbContext.SaveChangesAsync();
        };
    }

    public async Task<ICollection<Core.Entities.OrganizationUser>> GetManyByOrganizationWithClaimedDomainsAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new OrganizationUserReadByClaimedOrganizationDomainsQuery(organizationId);
            var data = await query.Run(dbContext).ToListAsync();
            return data;
        }
    }

    public async Task RevokeManyByIdAsync(IEnumerable<Guid> organizationUserIds)
    {
        using var scope = ServiceScopeFactory.CreateScope();

        var dbContext = GetDatabaseContext(scope);

        await dbContext.OrganizationUsers.Where(x => organizationUserIds.Contains(x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, OrganizationUserStatusType.Revoked));

        await dbContext.UserBumpAccountRevisionDateByOrganizationUserIdsAsync(organizationUserIds);
    }
}
