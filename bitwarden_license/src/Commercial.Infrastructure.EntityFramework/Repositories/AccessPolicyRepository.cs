using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.Repositories;

public class AccessPolicyRepository : BaseEntityFrameworkRepository, IAccessPolicyRepository
{
    public AccessPolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(serviceScopeFactory,
        mapper)
    {
    }

    public async Task<List<Core.Entities.BaseAccessPolicy>> CreateManyAsync(List<Core.Entities.BaseAccessPolicy> baseAccessPolicies)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            foreach (var baseAccessPolicy in baseAccessPolicies)
            {
                baseAccessPolicy.SetNewId();
                switch (baseAccessPolicy)
                {
                    case Core.Entities.UserProjectAccessPolicy accessPolicy:
                        {
                            var entity =
                                Mapper.Map<UserProjectAccessPolicy>(accessPolicy);
                            await dbContext.AddAsync(entity);
                            break;
                        }
                    case Core.Entities.GroupProjectAccessPolicy accessPolicy:
                        {
                            var entity = Mapper.Map<GroupProjectAccessPolicy>(accessPolicy);
                            await dbContext.AddAsync(entity);
                            break;
                        }
                    case Core.Entities.ServiceAccountProjectAccessPolicy accessPolicy:
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

    public async Task<bool> AccessPolicyExists(Core.Entities.BaseAccessPolicy baseAccessPolicy)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            switch (baseAccessPolicy)
            {
                case Core.Entities.UserProjectAccessPolicy accessPolicy:
                    {
                        var policy = await dbContext.UserProjectAccessPolicy
                            .Where(c => c.OrganizationUserId == accessPolicy.OrganizationUserId &&
                                        c.GrantedProjectId == accessPolicy.GrantedProjectId)
                            .FirstOrDefaultAsync();
                        return policy != null;
                    }
                case Core.Entities.GroupProjectAccessPolicy accessPolicy:
                    {
                        var policy = await dbContext.GroupProjectAccessPolicy
                            .Where(c => c.GroupId == accessPolicy.GroupId &&
                                      c.GrantedProjectId == accessPolicy.GrantedProjectId)
                            .FirstOrDefaultAsync();
                        return policy != null;
                    }
                case Core.Entities.ServiceAccountProjectAccessPolicy accessPolicy:
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

    public async Task<Core.Entities.BaseAccessPolicy?> GetByIdAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await dbContext.AccessPolicies.Where(ap => ap.Id == id)
                .Include(ap => ((UserProjectAccessPolicy)ap).OrganizationUser.User)
                .Include(ap => ((GroupProjectAccessPolicy)ap).Group)
                .Include(ap => ((ServiceAccountProjectAccessPolicy)ap).ServiceAccount)
                .FirstOrDefaultAsync();

            if (entity == null)
            {
                return null;
            }

            return entity switch
            {
                UserProjectAccessPolicy ap => Mapper.Map<Core.Entities.UserProjectAccessPolicy>(ap),
                GroupProjectAccessPolicy ap => Mapper.Map<Core.Entities.GroupProjectAccessPolicy>(ap),
                ServiceAccountProjectAccessPolicy ap => Mapper.Map<Core.Entities.ServiceAccountProjectAccessPolicy>(ap),
                _ => throw new ArgumentException("Unsupported access policy type")
            };
        }
    }

    public async Task ReplaceAsync(Core.Entities.BaseAccessPolicy baseAccessPolicy)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await dbContext.AccessPolicies.Where(c => c.Id == baseAccessPolicy.Id).FirstOrDefaultAsync();
            if (entity != null)
            {
                dbContext.AccessPolicies.Attach(entity);
                entity.Write = baseAccessPolicy.Write;
                entity.Read = baseAccessPolicy.Read;
                await dbContext.SaveChangesAsync();
            }
        }
    }

    public async Task<List<Core.Entities.BaseAccessPolicy>?> GetManyByProjectId(Guid id)
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

            if (!entities.Any())
            {
                return null;
            }

            var policies = new List<Core.Entities.BaseAccessPolicy>();

            foreach (var entity in entities)
            {
                switch (entity)
                {
                    case UserProjectAccessPolicy accessPolicy:
                        policies.Add(Mapper.Map<Core.Entities.UserProjectAccessPolicy>(accessPolicy));
                        break;
                    case GroupProjectAccessPolicy accessPolicy:
                        policies.Add(Mapper.Map<Core.Entities.GroupProjectAccessPolicy>(accessPolicy));
                        break;
                    case ServiceAccountProjectAccessPolicy accessPolicy:
                        policies.Add(Mapper.Map<Core.Entities.ServiceAccountProjectAccessPolicy>(accessPolicy));
                        break;
                    default:
                        throw new ArgumentException("Unsupported access policy type");
                }
            }
            return policies;
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
}
