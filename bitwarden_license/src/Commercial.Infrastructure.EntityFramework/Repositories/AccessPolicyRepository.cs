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
}
