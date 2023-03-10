using System.Linq.Expressions;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;


namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;

public class AccessPolicyRepository : BaseEntityFrameworkRepository, IAccessPolicyRepository
{
    public AccessPolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(serviceScopeFactory,
        mapper)
    {
    }

    private static Expression<Func<ServiceAccountProjectAccessPolicy, bool>> UserHasWriteAccessToProject(Guid userId) =>
        policy =>
            policy.GrantedProject.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
            policy.GrantedProject.GroupAccessPolicies.Any(ap =>
                ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write));

    public async Task<List<Core.SecretsManager.Entities.BaseAccessPolicy>> CreateManyAsync(List<Core.SecretsManager.Entities.BaseAccessPolicy> baseAccessPolicies)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        foreach (var baseAccessPolicy in baseAccessPolicies)
        {
            baseAccessPolicy.SetNewId();
            switch (baseAccessPolicy)
            {
                case Core.SecretsManager.Entities.UserProjectAccessPolicy accessPolicy:
                    {
                        var entity =
                            Mapper.Map<UserProjectAccessPolicy>(accessPolicy);
                        await dbContext.AddAsync(entity);
                        break;
                    }
                case Core.SecretsManager.Entities.UserServiceAccountAccessPolicy accessPolicy:
                    {
                        var entity =
                            Mapper.Map<UserServiceAccountAccessPolicy>(accessPolicy);
                        await dbContext.AddAsync(entity);
                        break;
                    }
                case Core.SecretsManager.Entities.GroupProjectAccessPolicy accessPolicy:
                    {
                        var entity = Mapper.Map<GroupProjectAccessPolicy>(accessPolicy);
                        await dbContext.AddAsync(entity);
                        break;
                    }
                case Core.SecretsManager.Entities.GroupServiceAccountAccessPolicy accessPolicy:
                    {
                        var entity = Mapper.Map<GroupServiceAccountAccessPolicy>(accessPolicy);
                        await dbContext.AddAsync(entity);
                        break;
                    }
                case Core.SecretsManager.Entities.ServiceAccountProjectAccessPolicy accessPolicy:
                    {
                        var entity = Mapper.Map<ServiceAccountProjectAccessPolicy>(accessPolicy);
                        await dbContext.AddAsync(entity);
                        break;
                    }
            }
        }

        await dbContext.SaveChangesAsync();
        return baseAccessPolicies;
    }

    public async Task<bool> AccessPolicyExists(Core.SecretsManager.Entities.BaseAccessPolicy baseAccessPolicy)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        switch (baseAccessPolicy)
        {
            case Core.SecretsManager.Entities.UserProjectAccessPolicy accessPolicy:
                {
                    var policy = await dbContext.UserProjectAccessPolicy
                        .Where(c => c.OrganizationUserId == accessPolicy.OrganizationUserId &&
                                    c.GrantedProjectId == accessPolicy.GrantedProjectId)
                        .FirstOrDefaultAsync();
                    return policy != null;
                }
            case Core.SecretsManager.Entities.GroupProjectAccessPolicy accessPolicy:
                {
                    var policy = await dbContext.GroupProjectAccessPolicy
                        .Where(c => c.GroupId == accessPolicy.GroupId &&
                                    c.GrantedProjectId == accessPolicy.GrantedProjectId)
                        .FirstOrDefaultAsync();
                    return policy != null;
                }
            case Core.SecretsManager.Entities.ServiceAccountProjectAccessPolicy accessPolicy:
                {
                    var policy = await dbContext.ServiceAccountProjectAccessPolicy
                        .Where(c => c.ServiceAccountId == accessPolicy.ServiceAccountId &&
                                    c.GrantedProjectId == accessPolicy.GrantedProjectId)
                        .FirstOrDefaultAsync();
                    return policy != null;
                }
            case Core.SecretsManager.Entities.UserServiceAccountAccessPolicy accessPolicy:
                {
                    var policy = await dbContext.UserServiceAccountAccessPolicy
                        .Where(c => c.OrganizationUserId == accessPolicy.OrganizationUserId &&
                                    c.GrantedServiceAccountId == accessPolicy.GrantedServiceAccountId)
                        .FirstOrDefaultAsync();
                    return policy != null;
                }
            case Core.SecretsManager.Entities.GroupServiceAccountAccessPolicy accessPolicy:
                {
                    var policy = await dbContext.GroupServiceAccountAccessPolicy
                        .Where(c => c.GroupId == accessPolicy.GroupId &&
                                    c.GrantedServiceAccountId == accessPolicy.GrantedServiceAccountId)
                        .FirstOrDefaultAsync();
                    return policy != null;
                }
            default:
                throw new ArgumentException("Unsupported access policy type provided.", nameof(baseAccessPolicy));
        }
    }

    public async Task<Core.SecretsManager.Entities.BaseAccessPolicy?> GetByIdAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entity = await dbContext.AccessPolicies.Where(ap => ap.Id == id)
            .Include(ap => ((UserProjectAccessPolicy)ap).OrganizationUser.User)
            .Include(ap => ((UserProjectAccessPolicy)ap).GrantedProject)
            .Include(ap => ((GroupProjectAccessPolicy)ap).Group)
            .Include(ap => ((GroupProjectAccessPolicy)ap).GrantedProject)
            .Include(ap => ((ServiceAccountProjectAccessPolicy)ap).ServiceAccount)
            .Include(ap => ((ServiceAccountProjectAccessPolicy)ap).GrantedProject)
            .Include(ap => ((UserServiceAccountAccessPolicy)ap).OrganizationUser.User)
            .Include(ap => ((UserServiceAccountAccessPolicy)ap).GrantedServiceAccount)
            .Include(ap => ((GroupServiceAccountAccessPolicy)ap).Group)
            .Include(ap => ((GroupServiceAccountAccessPolicy)ap).GrantedServiceAccount)
            .FirstOrDefaultAsync();

        return entity == null ? null : MapToCore(entity);
    }

    public async Task ReplaceAsync(Core.SecretsManager.Entities.BaseAccessPolicy baseAccessPolicy)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entity = await dbContext.AccessPolicies.FindAsync(baseAccessPolicy.Id);
        if (entity != null)
        {
            dbContext.AccessPolicies.Attach(entity);
            entity.Write = baseAccessPolicy.Write;
            entity.Read = baseAccessPolicy.Read;
            entity.RevisionDate = baseAccessPolicy.RevisionDate;
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.BaseAccessPolicy>> GetManyByGrantedProjectIdAsync(Guid id, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var entities = await dbContext.AccessPolicies.Where(ap =>
                ((UserProjectAccessPolicy)ap).GrantedProjectId == id ||
                ((GroupProjectAccessPolicy)ap).GrantedProjectId == id ||
                ((ServiceAccountProjectAccessPolicy)ap).GrantedProjectId == id)
            .Include(ap => ((UserProjectAccessPolicy)ap).OrganizationUser.User)
            .Include(ap => ((GroupProjectAccessPolicy)ap).Group)
            .Include(ap => ((ServiceAccountProjectAccessPolicy)ap).ServiceAccount)
            .Select(ap => new
            {
                ap,
                CurrentUserInGroup = ap is GroupProjectAccessPolicy &&
                                     ((GroupProjectAccessPolicy)ap).Group.GroupUsers.Any(g =>
                                         g.OrganizationUser.User.Id == userId),
            })
            .ToListAsync();

        return entities.Select(e => MapToCore(e.ap, e.CurrentUserInGroup));
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.BaseAccessPolicy>> GetManyByGrantedServiceAccountIdAsync(Guid id, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var entities = await dbContext.AccessPolicies.Where(ap =>
                ((UserServiceAccountAccessPolicy)ap).GrantedServiceAccountId == id ||
                ((GroupServiceAccountAccessPolicy)ap).GrantedServiceAccountId == id)
            .Include(ap => ((UserServiceAccountAccessPolicy)ap).OrganizationUser.User)
            .Include(ap => ((GroupServiceAccountAccessPolicy)ap).Group)
            .Select(ap => new
            {
                ap,
                CurrentUserInGroup = ap is GroupServiceAccountAccessPolicy &&
                                     ((GroupServiceAccountAccessPolicy)ap).Group.GroupUsers.Any(g =>
                                         g.OrganizationUser.User.Id == userId),
            })
            .ToListAsync();

        return entities.Select(e => MapToCore(e.ap, e.CurrentUserInGroup));
    }

    public async Task DeleteAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entity = await dbContext.AccessPolicies.FindAsync(id);
        if (entity != null)
        {
            dbContext.Remove(entity);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.BaseAccessPolicy>> GetManyByServiceAccountIdAsync(Guid id, Guid userId,
        AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.ServiceAccountProjectAccessPolicy.Where(ap =>
            ap.ServiceAccountId == id);

        query = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasWriteAccessToProject(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        var entities = await query
            .Include(ap => ap.ServiceAccount)
            .Include(ap => ap.GrantedProject)
            .ToListAsync();

        return entities.Select(MapToCore);
    }

    private Core.SecretsManager.Entities.BaseAccessPolicy MapToCore(
        BaseAccessPolicy baseAccessPolicyEntity) =>
        baseAccessPolicyEntity switch
        {
            UserProjectAccessPolicy ap => Mapper.Map<Core.SecretsManager.Entities.UserProjectAccessPolicy>(ap),
            GroupProjectAccessPolicy ap => Mapper.Map<Core.SecretsManager.Entities.GroupProjectAccessPolicy>(ap),
            ServiceAccountProjectAccessPolicy ap => Mapper
                .Map<Core.SecretsManager.Entities.ServiceAccountProjectAccessPolicy>(ap),
            UserServiceAccountAccessPolicy ap =>
                Mapper.Map<Core.SecretsManager.Entities.UserServiceAccountAccessPolicy>(ap),
            GroupServiceAccountAccessPolicy ap => Mapper
                .Map<Core.SecretsManager.Entities.GroupServiceAccountAccessPolicy>(ap),
            _ => throw new ArgumentException("Unsupported access policy type"),
        };

    private Core.SecretsManager.Entities.BaseAccessPolicy MapToCore(
      BaseAccessPolicy baseAccessPolicyEntity, bool currentUserInGroup)
    {
        switch (baseAccessPolicyEntity)
        {
            case GroupProjectAccessPolicy ap:
                {
                    var mapped = Mapper.Map<Core.SecretsManager.Entities.GroupProjectAccessPolicy>(ap);
                    mapped.CurrentUserInGroup = currentUserInGroup;
                    return mapped;
                }
            case GroupServiceAccountAccessPolicy ap:
                {
                    var mapped = Mapper.Map<Core.SecretsManager.Entities.GroupServiceAccountAccessPolicy>(ap);
                    mapped.CurrentUserInGroup = currentUserInGroup;
                    return mapped;
                }
            default:
                return MapToCore(baseAccessPolicyEntity);
        }
    }
}
