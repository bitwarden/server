using System.Linq.Expressions;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Discriminators;
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

    public async Task<PeopleGrantees> GetPeopleGranteesAsync(Guid organizationId, Guid currentUserId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var userGrantees = await dbContext.OrganizationUsers
            .Where(ou =>
                ou.OrganizationId == organizationId &&
                ou.AccessSecretsManager &&
                ou.Status == OrganizationUserStatusType.Confirmed)
            .Include(ou => ou.User)
            .Select(ou => new
                UserGrantee
            {
                OrganizationUserId = ou.Id,
                Name = ou.User.Name,
                Email = ou.User.Email,
                CurrentUser = ou.UserId == currentUserId
            }).ToListAsync();

        var groupGrantees = await dbContext.Groups
            .Where(g => g.OrganizationId == organizationId)
            .Include(g => g.GroupUsers)
            .Select(g => new GroupGrantee
            {
                GroupId = g.Id,
                Name = g.Name,
                CurrentUserInGroup = g.GroupUsers.Any(gu =>
                    gu.OrganizationUser.User.Id == currentUserId)
            }).ToListAsync();

        return new PeopleGrantees { UserGrantees = userGrantees, GroupGrantees = groupGrantees };
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.BaseAccessPolicy>>
        GetPeoplePoliciesByGrantedProjectIdAsync(Guid id, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var entities = await dbContext.AccessPolicies.Where(ap =>
                ap.Discriminator != AccessPolicyDiscriminator.ServiceAccountProject &&
                (((UserProjectAccessPolicy)ap).GrantedProjectId == id ||
                 ((GroupProjectAccessPolicy)ap).GrantedProjectId == id))
            .Include(ap => ((UserProjectAccessPolicy)ap).OrganizationUser.User)
            .Include(ap => ((GroupProjectAccessPolicy)ap).Group)
            .Select(ap => new
            {
                ap,
                CurrentUserInGroup = ap is GroupProjectAccessPolicy &&
                                     ((GroupProjectAccessPolicy)ap).Group.GroupUsers.Any(g =>
                                         g.OrganizationUser.UserId == userId),
            })
            .ToListAsync();

        return entities.Select(e => MapToCore(e.ap, e.CurrentUserInGroup));
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.BaseAccessPolicy>> ReplaceProjectPeopleAsync(
        ProjectPeopleAccessPolicies peopleAccessPolicies, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var peoplePolicyEntities = await dbContext.AccessPolicies.Where(ap =>
            ap.Discriminator != AccessPolicyDiscriminator.ServiceAccountProject &&
            (((UserProjectAccessPolicy)ap).GrantedProjectId == peopleAccessPolicies.Id ||
             ((GroupProjectAccessPolicy)ap).GrantedProjectId == peopleAccessPolicies.Id)).ToListAsync();

        var userPolicyEntities =
            peoplePolicyEntities.Where(ap => ap.GetType() == typeof(UserProjectAccessPolicy)).ToList();
        var groupPolicyEntities =
            peoplePolicyEntities.Where(ap => ap.GetType() == typeof(GroupProjectAccessPolicy)).ToList();


        if (peopleAccessPolicies.UserAccessPolicies == null || !peopleAccessPolicies.UserAccessPolicies.Any())
        {
            dbContext.RemoveRange(userPolicyEntities);
        }
        else
        {
            foreach (var userPolicyEntity in userPolicyEntities.Where(entity =>
                         peopleAccessPolicies.UserAccessPolicies.All(ap =>
                             ((Core.SecretsManager.Entities.UserProjectAccessPolicy)ap).OrganizationUserId !=
                             ((UserProjectAccessPolicy)entity).OrganizationUserId)))
            {
                dbContext.Remove(userPolicyEntity);
            }
        }

        if (peopleAccessPolicies.GroupAccessPolicies == null || !peopleAccessPolicies.GroupAccessPolicies.Any())
        {
            dbContext.RemoveRange(groupPolicyEntities);
        }
        else
        {
            foreach (var groupPolicyEntity in groupPolicyEntities.Where(entity =>
                         peopleAccessPolicies.GroupAccessPolicies.All(ap =>
                             ((Core.SecretsManager.Entities.GroupProjectAccessPolicy)ap).GroupId !=
                             ((GroupProjectAccessPolicy)entity).GroupId)))
            {
                dbContext.Remove(groupPolicyEntity);
            }
        }

        await UpsertPeoplePoliciesAsync(dbContext,
            peopleAccessPolicies.ToBaseAccessPolicies().Select(MapToEntity).ToList(), userPolicyEntities,
            groupPolicyEntities);

        await dbContext.SaveChangesAsync();
        return await GetPeoplePoliciesByGrantedProjectIdAsync(peopleAccessPolicies.Id, userId);
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.BaseAccessPolicy>>
        GetPeoplePoliciesByGrantedServiceAccountIdAsync(Guid id, Guid userId)
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
                                         g.OrganizationUser.UserId == userId)
            })
            .ToListAsync();

        return entities.Select(e => MapToCore(e.ap, e.CurrentUserInGroup));
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.BaseAccessPolicy>> ReplaceServiceAccountPeopleAsync(
        ServiceAccountPeopleAccessPolicies peopleAccessPolicies, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var peoplePolicyEntities = await dbContext.AccessPolicies.Where(ap =>
            ((UserServiceAccountAccessPolicy)ap).GrantedServiceAccountId == peopleAccessPolicies.Id ||
            ((GroupServiceAccountAccessPolicy)ap).GrantedServiceAccountId == peopleAccessPolicies.Id).ToListAsync();

        var userPolicyEntities =
            peoplePolicyEntities.Where(ap => ap.GetType() == typeof(UserServiceAccountAccessPolicy)).ToList();
        var groupPolicyEntities =
            peoplePolicyEntities.Where(ap => ap.GetType() == typeof(GroupServiceAccountAccessPolicy)).ToList();


        if (peopleAccessPolicies.UserAccessPolicies == null || !peopleAccessPolicies.UserAccessPolicies.Any())
        {
            dbContext.RemoveRange(userPolicyEntities);
        }
        else
        {
            foreach (var userPolicyEntity in userPolicyEntities.Where(entity =>
                         peopleAccessPolicies.UserAccessPolicies.All(ap =>
                             ((Core.SecretsManager.Entities.UserServiceAccountAccessPolicy)ap).OrganizationUserId !=
                             ((UserServiceAccountAccessPolicy)entity).OrganizationUserId)))
            {
                dbContext.Remove(userPolicyEntity);
            }
        }

        if (peopleAccessPolicies.GroupAccessPolicies == null || !peopleAccessPolicies.GroupAccessPolicies.Any())
        {
            dbContext.RemoveRange(groupPolicyEntities);
        }
        else
        {
            foreach (var groupPolicyEntity in groupPolicyEntities.Where(entity =>
                         peopleAccessPolicies.GroupAccessPolicies.All(ap =>
                             ((Core.SecretsManager.Entities.GroupServiceAccountAccessPolicy)ap).GroupId !=
                             ((GroupServiceAccountAccessPolicy)entity).GroupId)))
            {
                dbContext.Remove(groupPolicyEntity);
            }
        }

        await UpsertPeoplePoliciesAsync(dbContext,
            peopleAccessPolicies.ToBaseAccessPolicies().Select(MapToEntity).ToList(), userPolicyEntities,
            groupPolicyEntities);
        await dbContext.SaveChangesAsync();
        return await GetPeoplePoliciesByGrantedServiceAccountIdAsync(peopleAccessPolicies.Id, userId);
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.BaseAccessPolicy>>
       GetServiceAccountPoliciesByGrantedProjectIdAsync(Guid projectId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var entities = await dbContext.AccessPolicies.Where(ap =>
                ap.Discriminator == AccessPolicyDiscriminator.ServiceAccountProject &&
                (((ServiceAccountProjectAccessPolicy)ap).GrantedProjectId == projectId))
            .Include(ap => ((ServiceAccountProjectAccessPolicy)ap).ServiceAccount)
            .ToListAsync();

        return entities.Select(MapToCore);
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.BaseAccessPolicy>> ReplaceProjectServiceAccountsAsync(
       ProjectServiceAccountsAccessPolicies newProjectServiceAccountsAccessPolicies)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var currentProjectServiceAccountsPolicyEntities = await dbContext.AccessPolicies.Where(ap =>
            ap.Discriminator == AccessPolicyDiscriminator.ServiceAccountProject &&
            ((ServiceAccountProjectAccessPolicy)ap).GrantedProjectId == newProjectServiceAccountsAccessPolicies.Id).ToListAsync();

        if (newProjectServiceAccountsAccessPolicies.ServiceAccountProjectsAccessPolicies == null || !newProjectServiceAccountsAccessPolicies.ServiceAccountProjectsAccessPolicies.Any())
        {
            dbContext.RemoveRange(currentProjectServiceAccountsPolicyEntities);
        }
        else
        {
            foreach (var projectServiceAccountsPolicyEntity in currentProjectServiceAccountsPolicyEntities.Where(entity =>
                         newProjectServiceAccountsAccessPolicies.ServiceAccountProjectsAccessPolicies.All(ap =>
                             ((Core.SecretsManager.Entities.ServiceAccountProjectAccessPolicy)ap).ServiceAccountId !=
                             ((ServiceAccountProjectAccessPolicy)entity).ServiceAccountId)))
            {
                dbContext.Remove(projectServiceAccountsPolicyEntity);
            }
        }

        await UpsertProjectServiceAccountsPoliciesAsync(dbContext,
            newProjectServiceAccountsAccessPolicies.ToBaseAccessPolicies().Select(MapToEntity).ToList(), currentProjectServiceAccountsPolicyEntities);

        await dbContext.SaveChangesAsync();
        return await GetServiceAccountPoliciesByGrantedProjectIdAsync(newProjectServiceAccountsAccessPolicies.Id);
    }


    private static async Task UpsertPeoplePoliciesAsync(DatabaseContext dbContext,
        List<BaseAccessPolicy> policies, IReadOnlyCollection<AccessPolicy> userPolicyEntities,
        IReadOnlyCollection<AccessPolicy> groupPolicyEntities)
    {
        var currentDate = DateTime.UtcNow;
        foreach (var updatedEntity in policies)
        {
            var currentEntity = updatedEntity switch
            {
                UserProjectAccessPolicy ap => userPolicyEntities.FirstOrDefault(e =>
                    ((UserProjectAccessPolicy)e).OrganizationUserId == ap.OrganizationUserId),
                GroupProjectAccessPolicy ap => groupPolicyEntities.FirstOrDefault(e =>
                    ((GroupProjectAccessPolicy)e).GroupId == ap.GroupId),
                UserServiceAccountAccessPolicy ap => userPolicyEntities.FirstOrDefault(e =>
                    ((UserServiceAccountAccessPolicy)e).OrganizationUserId == ap.OrganizationUserId),
                GroupServiceAccountAccessPolicy ap => groupPolicyEntities.FirstOrDefault(e =>
                    ((GroupServiceAccountAccessPolicy)e).GroupId == ap.GroupId),
                _ => null
            };

            if (currentEntity != null)
            {
                dbContext.AccessPolicies.Attach(currentEntity);
                currentEntity.Read = updatedEntity.Read;
                currentEntity.Write = updatedEntity.Write;
                currentEntity.RevisionDate = currentDate;
            }
            else
            {
                updatedEntity.SetNewId();
                await dbContext.AddAsync(updatedEntity);
            }
        }
    }

    private static async Task UpsertProjectServiceAccountsPoliciesAsync(DatabaseContext dbContext,
      List<BaseAccessPolicy> newPolicies, IReadOnlyCollection<AccessPolicy> currentProjectServiceAccountAccessPolicies)
    {
        var currentDate = DateTime.UtcNow;
        foreach (var updatedEntity in newPolicies)
        {
            var currentEntity = updatedEntity switch
            {
                ServiceAccountProjectAccessPolicy ap => currentProjectServiceAccountAccessPolicies.FirstOrDefault(e =>
                    ((ServiceAccountProjectAccessPolicy)e).ServiceAccountId == ap.ServiceAccountId),
                _ => null
            };

            if (currentEntity != null)
            {
                dbContext.AccessPolicies.Attach(currentEntity);
                currentEntity.Read = updatedEntity.Read;
                currentEntity.Write = updatedEntity.Write;
                currentEntity.RevisionDate = currentDate;
            }
            else
            {
                updatedEntity.SetNewId();
                await dbContext.AddAsync(updatedEntity);
            }
        }
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
            _ => throw new ArgumentException("Unsupported access policy type")
        };

    private BaseAccessPolicy MapToEntity(Core.SecretsManager.Entities.BaseAccessPolicy baseAccessPolicy)
    {
        return baseAccessPolicy switch
        {
            Core.SecretsManager.Entities.UserProjectAccessPolicy accessPolicy => Mapper.Map<UserProjectAccessPolicy>(
                accessPolicy),
            Core.SecretsManager.Entities.UserServiceAccountAccessPolicy accessPolicy => Mapper
                .Map<UserServiceAccountAccessPolicy>(accessPolicy),
            Core.SecretsManager.Entities.GroupProjectAccessPolicy accessPolicy => Mapper.Map<GroupProjectAccessPolicy>(
                accessPolicy),
            Core.SecretsManager.Entities.GroupServiceAccountAccessPolicy accessPolicy => Mapper
                .Map<GroupServiceAccountAccessPolicy>(accessPolicy),
            Core.SecretsManager.Entities.ServiceAccountProjectAccessPolicy accessPolicy => Mapper
                .Map<ServiceAccountProjectAccessPolicy>(accessPolicy),
            _ => throw new ArgumentException("Unsupported access policy type")
        };
    }

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
