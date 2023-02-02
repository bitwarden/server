using AutoMapper;
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

    public async Task<List<Core.SecretsManager.Entities.BaseAccessPolicy>> CreateManyAsync(List<Core.SecretsManager.Entities.BaseAccessPolicy> baseAccessPolicies)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
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
    }

    public async Task<bool> AccessPolicyExists(Core.SecretsManager.Entities.BaseAccessPolicy baseAccessPolicy)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
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
                default:
                    throw new ArgumentException("Unsupported access policy type provided.", nameof(baseAccessPolicy));
            }
        }
    }

    public async Task<Core.SecretsManager.Entities.BaseAccessPolicy?> GetByIdAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
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

            if (entity == null)
            {
                return null;
            }

            return MapToCore(entity);
        }
    }

    public async Task ReplaceAsync(Core.SecretsManager.Entities.BaseAccessPolicy baseAccessPolicy)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
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
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.BaseAccessPolicy>?> GetManyByProjectId(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var entities = await dbContext.AccessPolicies.Where(ap =>
                    ((UserProjectAccessPolicy)ap).GrantedProjectId == id ||
                    ((GroupProjectAccessPolicy)ap).GrantedProjectId == id ||
                    ((ServiceAccountProjectAccessPolicy)ap).GrantedProjectId == id)
                .Include(ap => ((UserProjectAccessPolicy)ap).OrganizationUser.User)
                .Include(ap => ((GroupProjectAccessPolicy)ap).Group)
                .Include(ap => ((ServiceAccountProjectAccessPolicy)ap).ServiceAccount)
                .ToListAsync();

            return !entities.Any() ? null : entities.Select(MapToCore);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await dbContext.AccessPolicies.FindAsync(id);
            if (entity != null)
            {
                dbContext.Remove(entity);
                await dbContext.SaveChangesAsync();
            }
        }
    }

    private Core.SecretsManager.Entities.BaseAccessPolicy MapToCore(BaseAccessPolicy baseAccessPolicyEntity)
    {
        return baseAccessPolicyEntity switch
        {
            UserProjectAccessPolicy ap => Mapper.Map<Core.SecretsManager.Entities.UserProjectAccessPolicy>(ap),
            GroupProjectAccessPolicy ap => Mapper.Map<Core.SecretsManager.Entities.GroupProjectAccessPolicy>(ap),
            ServiceAccountProjectAccessPolicy ap => Mapper.Map<Core.SecretsManager.Entities.ServiceAccountProjectAccessPolicy>(ap),
            _ => throw new ArgumentException("Unsupported access policy type")
        };
    }
}
