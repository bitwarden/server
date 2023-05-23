using AutoMapper;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class ProviderUserRepository :
    Repository<ProviderUser, Models.ProviderUser, Guid>, IProviderUserRepository
{
    public ProviderUserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.ProviderUsers)
    { }

    public override async Task DeleteAsync(ProviderUser providerUser)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateByProviderUserIdAsync(providerUser.Id);
            await dbContext.SaveChangesAsync();
        }
        await base.DeleteAsync(providerUser);
    }

    public async Task<int> GetCountByProviderAsync(Guid providerId, string email, bool onlyRegisteredUsers)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from pu in dbContext.ProviderUsers
                        join u in dbContext.Users
                            on pu.UserId equals u.Id into u_g
                        from u in u_g.DefaultIfEmpty()
                        where pu.ProviderId == providerId &&
                            ((!onlyRegisteredUsers && (pu.Email == email || u.Email == email)) ||
                            (onlyRegisteredUsers && u.Email == email))
                        select new { pu, u };
            return await query.CountAsync();
        }
    }

    public async Task<ICollection<ProviderUser>> GetManyAsync(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.ProviderUsers.Where(item => ids.Contains(item.Id));
            return await query.ToArrayAsync();
        }
    }

    public async Task<ICollection<ProviderUser>> GetManyByProviderAsync(Guid providerId, ProviderUserType? type = null)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.ProviderUsers.Where(pu => pu.ProviderId.Equals(providerId) &&
                (type != null && pu.Type.Equals(type)));
            return await query.ToArrayAsync();
        }
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> providerUserIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            foreach (var providerUserId in providerUserIds)
            {
                await dbContext.UserBumpAccountRevisionDateByProviderUserIdAsync(providerUserId);
            }
            var entities = dbContext.ProviderUsers.Where(pu => providerUserIds.Contains(pu.Id));
            dbContext.ProviderUsers.RemoveRange(entities);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<ICollection<ProviderUser>> GetManyByUserAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from pu in dbContext.ProviderUsers
                        where pu.UserId == userId
                        select pu;
            return await query.ToArrayAsync();
        }
    }
    public async Task<ProviderUser> GetByProviderUserAsync(Guid providerId, Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from pu in dbContext.ProviderUsers
                        where pu.UserId == userId &&
                            pu.ProviderId == providerId
                        select pu;
            return await query.FirstOrDefaultAsync();
        }
    }

    public async Task<ProviderUserUserDetails> GetDetailsByIdAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var view = from pu in dbContext.ProviderUsers
                       join u in dbContext.Users
                           on pu.UserId equals u.Id into u_g
                       from u in u_g.DefaultIfEmpty()
                       select new { pu, u };
            var data = await view
                .Where(e => e.pu.Id == id)
                .Select(e => new ProviderUserUserDetails
                {
                    Id = e.pu.Id,
                    UserId = e.pu.UserId,
                    ProviderId = e.pu.ProviderId,
                    Name = e.u.Name,
                    Email = e.u.Email ?? e.pu.Email,
                    Status = e.pu.Status,
                    Type = e.pu.Type,
                    Permissions = e.pu.Permissions,
                    HasMasterPassword = e.u != null && !string.IsNullOrWhiteSpace(e.u.MasterPassword)
                }).FirstOrDefaultAsync();
            return data;
        }
    }

    public async Task<ICollection<ProviderUserUserDetails>> GetManyDetailsByProviderAsync(Guid providerId, ProviderUserStatusType? status)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var view = from pu in dbContext.ProviderUsers
                       join u in dbContext.Users
                           on pu.UserId equals u.Id into u_g
                       from u in u_g.DefaultIfEmpty()
                       select new { pu, u };
            var data = await view
                .Where(e => e.pu.ProviderId == providerId && (status == null || e.pu.Status == status))
                .Select(e => new ProviderUserUserDetails
                {
                    Id = e.pu.Id,
                    UserId = e.pu.UserId,
                    ProviderId = e.pu.ProviderId,
                    Name = e.u.Name,
                    Email = e.u.Email ?? e.pu.Email,
                    Status = e.pu.Status,
                    Type = e.pu.Type,
                    Permissions = e.pu.Permissions,
                    HasMasterPassword = e.u != null && !string.IsNullOrWhiteSpace(e.u.MasterPassword)
                }).ToArrayAsync();
            return data;
        }
    }

    public async Task<ICollection<ProviderUserProviderDetails>> GetManyDetailsByUserAsync(Guid userId, ProviderUserStatusType? status = null)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new ProviderUserProviderDetailsReadByUserIdStatusQuery(userId, status);
            var data = await query.Run(dbContext).ToArrayAsync();
            return data;
        }
    }

    public async Task<IEnumerable<ProviderUserPublicKey>> GetManyPublicKeysByProviderUserAsync(Guid providerId, IEnumerable<Guid> Ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new UserReadPublicKeysByProviderUserIdsQuery(providerId, Ids);
            var data = await query.Run(dbContext).ToListAsync();
            return data;
        }
    }

    public async Task<IEnumerable<ProviderUserOrganizationDetails>> GetManyOrganizationDetailsByUserAsync(Guid userId, ProviderUserStatusType? status = null)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var view = new ProviderUserOrganizationDetailsViewQuery();
            var query = from ou in view.Run(dbContext)
                        where ou.UserId == userId &&
                              (status == null || ou.Status == status)
                        select ou;
            var organizationUsers = await query.ToListAsync();
            return organizationUsers;
        }
    }

    public async Task<int> GetCountByOnlyOwnerAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await dbContext.ProviderUsers
                .Where(pu => pu.Type == ProviderUserType.ProviderAdmin && pu.Status == ProviderUserStatusType.Confirmed)
                .GroupBy(pu => pu.UserId)
                .Select(g => new { UserId = g.Key, ConfirmedOwnerCount = g.Count() })
                .Where(oc => oc.UserId == userId && oc.ConfirmedOwnerCount == 1)
                .CountAsync();
        }
    }

    public async Task<ICollection<ProviderUser>> GetManyByOrganizationAsync(Guid organizationId, ProviderUserStatusType? status = null)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from pu in dbContext.ProviderUsers
                        join po in dbContext.ProviderOrganizations
                            on pu.ProviderId equals po.ProviderId
                        where po.OrganizationId == organizationId &&
                              (status == null || pu.Status == status)
                        select pu;
            return await query.ToArrayAsync();
        }
    }
}
