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

    private static Expression<Func<ServiceAccount, bool>> UserHasReadAccessToServiceAccount(Guid userId) => sa =>
        sa.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read) ||
        sa.GroupAccessPolicies.Any(ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read));

    private static Expression<Func<ServiceAccount, bool>> UserHasWriteAccessToServiceAccount(Guid userId) => sa =>
        sa.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
        sa.GroupAccessPolicies.Any(ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write));
}
