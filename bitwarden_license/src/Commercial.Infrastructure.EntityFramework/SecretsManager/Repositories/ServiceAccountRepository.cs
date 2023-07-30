using System.Linq.Expressions;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;

public class ServiceAccountRepository : Repository<Core.SecretsManager.Entities.ServiceAccount, ServiceAccount, Guid>, IServiceAccountRepository
{
    public ServiceAccountRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.ServiceAccount)
    { }

    public async Task<IEnumerable<Core.SecretsManager.Entities.ServiceAccount>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.ServiceAccount.Where(c => c.OrganizationId == organizationId);

        query = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasReadAccessToServiceAccount(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        var serviceAccounts = await query.OrderBy(c => c.RevisionDate).ToListAsync();
        return Mapper.Map<List<Core.SecretsManager.Entities.ServiceAccount>>(serviceAccounts);
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.ServiceAccount>> GetManyByIds(IEnumerable<Guid> ids)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var serviceAccounts = await dbContext.ServiceAccount
            .Where(c => ids.Contains(c.Id))
            .ToListAsync();
        return Mapper.Map<List<Core.SecretsManager.Entities.ServiceAccount>>(serviceAccounts);
    }

    public async Task<bool> UserHasReadAccessToServiceAccount(Guid id, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.ServiceAccount
            .Where(sa => sa.Id == id)
            .Where(UserHasReadAccessToServiceAccount(userId));

        return await query.AnyAsync();
    }

    public async Task<bool> UserHasWriteAccessToServiceAccount(Guid id, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.ServiceAccount
            .Where(sa => sa.Id == id)
            .Where(UserHasWriteAccessToServiceAccount(userId));

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.ServiceAccount>> GetManyByOrganizationIdWriteAccessAsync(Guid organizationId, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.ServiceAccount.Where(c => c.OrganizationId == organizationId);

        query = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasWriteAccessToServiceAccount(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        var serviceAccounts = await query.OrderBy(c => c.RevisionDate).ToListAsync();
        return Mapper.Map<List<Core.SecretsManager.Entities.ServiceAccount>>(serviceAccounts);
    }

    public async Task DeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Policies can't have a cascade delete, so we need to delete them manually.
        var policies = dbContext.AccessPolicies.Where(ap =>
            ((ServiceAccountProjectAccessPolicy)ap).ServiceAccountId.HasValue && ids.Contains(((ServiceAccountProjectAccessPolicy)ap).ServiceAccountId!.Value) ||
            ((GroupServiceAccountAccessPolicy)ap).GrantedServiceAccountId.HasValue && ids.Contains(((GroupServiceAccountAccessPolicy)ap).GrantedServiceAccountId!.Value) ||
            ((UserServiceAccountAccessPolicy)ap).GrantedServiceAccountId.HasValue && ids.Contains(((UserServiceAccountAccessPolicy)ap).GrantedServiceAccountId!.Value));
        dbContext.RemoveRange(policies);

        var apiKeys = dbContext.ApiKeys.Where(a => a.ServiceAccountId.HasValue && ids.Contains(a.ServiceAccountId!.Value));
        dbContext.RemoveRange(apiKeys);

        var serviceAccounts = dbContext.ServiceAccount.Where(c => ids.Contains(c.Id));
        dbContext.RemoveRange(serviceAccounts);
        await dbContext.SaveChangesAsync();
    }

    public async Task<(bool Read, bool Write)> AccessToServiceAccountAsync(Guid id, Guid userId,
        AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var serviceAccount = dbContext.ServiceAccount.Where(sa => sa.Id == id);

        var query = accessType switch
        {
            AccessClientType.NoAccessCheck => serviceAccount.Select(_ => new { Read = true, Write = true }),
            AccessClientType.User => serviceAccount.Select(sa => new
            {
                Read = sa.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read) ||
                       sa.GroupAccessPolicies.Any(ap =>
                           ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read)),
                Write = sa.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
                        sa.GroupAccessPolicies.Any(ap =>
                            ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write)),
            }),
            AccessClientType.ServiceAccount => serviceAccount.Select(_ => new { Read = false, Write = false }),
            _ => serviceAccount.Select(_ => new { Read = false, Write = false }),
        };

        var policy = await query.FirstOrDefaultAsync();

        return policy == null ? (false, false) : (policy.Read, policy.Write);
    }

    public async Task<int> GetServiceAccountCountByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await dbContext.ServiceAccount
                .CountAsync(ou => ou.OrganizationId == organizationId);
        }
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.ServiceAccount>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.ServiceAccount.Where(x => x.OrganizationId == organizationId);
            var serviceAccounts = await query.ToListAsync();
            return Mapper.Map<List<Core.SecretsManager.Entities.ServiceAccount>>(serviceAccounts);
        }
    }

    private static Expression<Func<ServiceAccount, bool>> UserHasReadAccessToServiceAccount(Guid userId) => sa =>
        sa.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read) ||
        sa.GroupAccessPolicies.Any(ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read));

    private static Expression<Func<ServiceAccount, bool>> UserHasWriteAccessToServiceAccount(Guid userId) => sa =>
        sa.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
        sa.GroupAccessPolicies.Any(ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write));
}
