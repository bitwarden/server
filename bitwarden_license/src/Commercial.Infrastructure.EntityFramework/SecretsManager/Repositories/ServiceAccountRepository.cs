using System.Linq.Expressions;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Models.Data;
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
        var targetIds = ids.ToList();
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // Policies can't have a cascade delete, so we need to delete them manually.
        await dbContext.AccessPolicies.Where(ap =>
                targetIds.Contains(((ServiceAccountProjectAccessPolicy)ap).ServiceAccountId!.Value) ||
                targetIds.Contains(((ServiceAccountSecretAccessPolicy)ap).ServiceAccountId!.Value) ||
                targetIds.Contains(((GroupServiceAccountAccessPolicy)ap).GrantedServiceAccountId!.Value) ||
                targetIds.Contains(((UserServiceAccountAccessPolicy)ap).GrantedServiceAccountId!.Value))
            .ExecuteDeleteAsync();

        await dbContext.ApiKeys
            .Where(a => targetIds.Contains(a.ServiceAccountId!.Value))
            .ExecuteDeleteAsync();

        await dbContext.ServiceAccount
            .Where(c => targetIds.Contains(c.Id))
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();
    }

    public async Task<(bool Read, bool Write)> AccessToServiceAccountAsync(Guid id, Guid userId,
        AccessClientType accessType)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var serviceAccountQuery = dbContext.ServiceAccount.Where(sa => sa.Id == id);

        var accessQuery = BuildServiceAccountAccessQuery(serviceAccountQuery, userId, accessType);
        var access = await accessQuery.FirstOrDefaultAsync();

        return access == null ? (false, false) : (access.Read, access.Write);
    }

    public async Task<Dictionary<Guid, (bool Read, bool Write)>> AccessToServiceAccountsAsync(
        IEnumerable<Guid> ids,
        Guid userId,
        AccessClientType accessType)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var serviceAccountsQuery = dbContext.ServiceAccount.Where(p => ids.Contains(p.Id));
        var accessQuery = BuildServiceAccountAccessQuery(serviceAccountsQuery, userId, accessType);

        return await accessQuery.ToDictionaryAsync(access => access.Id, access => (access.Read, access.Write));
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

    public async Task<bool> ServiceAccountsAreInOrganizationAsync(List<Guid> serviceAccountIds, Guid organizationId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var result = await dbContext.ServiceAccount.CountAsync(sa =>
            sa.OrganizationId == organizationId && serviceAccountIds.Contains(sa.Id));
        return serviceAccountIds.Count == result;
    }

    public async Task<IEnumerable<ServiceAccountSecretsDetails>> GetManyByOrganizationIdWithSecretsDetailsAsync(
    Guid organizationId, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = from sa in dbContext.ServiceAccount
                    join ap in dbContext.ServiceAccountProjectAccessPolicy
                        on sa.Id equals ap.ServiceAccountId into grouping
                    from ap in grouping.DefaultIfEmpty()
                    where sa.OrganizationId == organizationId
                    select new
                    {
                        ServiceAccount = sa,
                        AccessToSecrets = ap.GrantedProject.Secrets.Count(s => s.DeletedDate == null)
                    };

        query = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(c =>
                c.ServiceAccount.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read) ||
                c.ServiceAccount.GroupAccessPolicies.Any(ap =>
                    ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read))),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        var results = (await query.ToListAsync())
            .GroupBy(g => g.ServiceAccount)
            .Select(g =>
                new ServiceAccountSecretsDetails
                {
                    ServiceAccount = Mapper.Map<Core.SecretsManager.Entities.ServiceAccount>(g.Key),
                    AccessToSecrets = g.Sum(x => x.AccessToSecrets),
                }).OrderBy(c => c.ServiceAccount.RevisionDate).ToList();

        return results;
    }

    private record ServiceAccountAccess(Guid Id, bool Read, bool Write);

    private static IQueryable<ServiceAccountAccess> BuildServiceAccountAccessQuery(IQueryable<ServiceAccount> serviceAccountQuery, Guid userId,
        AccessClientType accessType) =>
        accessType switch
        {
            AccessClientType.NoAccessCheck => serviceAccountQuery.Select(sa => new ServiceAccountAccess(sa.Id, true, true)),
            AccessClientType.User => serviceAccountQuery.Select(sa => new ServiceAccountAccess
            (
                sa.Id,
                sa.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read) ||
                sa.GroupAccessPolicies.Any(ap =>
                    ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read)),
                sa.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
                sa.GroupAccessPolicies.Any(ap =>
                    ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write))
            )),
            AccessClientType.ServiceAccount => serviceAccountQuery.Select(sa => new ServiceAccountAccess(sa.Id, false, false)),
            _ => serviceAccountQuery.Select(sa => new ServiceAccountAccess(sa.Id, false, false))
        };

    private static Expression<Func<ServiceAccount, bool>> UserHasReadAccessToServiceAccount(Guid userId) => sa =>
        sa.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read) ||
        sa.GroupAccessPolicies.Any(ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read));

    private static Expression<Func<ServiceAccount, bool>> UserHasWriteAccessToServiceAccount(Guid userId) => sa =>
        sa.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
        sa.GroupAccessPolicies.Any(ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write));
}
