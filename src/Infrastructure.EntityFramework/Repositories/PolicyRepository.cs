using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class PolicyRepository : Repository<Core.Entities.Policy, Policy, Guid>, IPolicyRepository
{
    public PolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Policies)
    { }

    public async Task<Core.Entities.Policy> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType type)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Policies
                .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Type == type);
            return Mapper.Map<Core.Entities.Policy>(results);
        }
    }

    public async Task<ICollection<Core.Entities.Policy>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Policies
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.Policy>>(results);
        }
    }

    public async Task<ICollection<Core.Entities.Policy>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var query = new PolicyReadByUserIdQuery(userId);
            var results = await query.Run(dbContext).ToListAsync();
            return Mapper.Map<List<Core.Entities.Policy>>(results);
        }
    }

    public async Task<ICollection<Core.Entities.Policy>> GetManyByTypeApplicableToUserIdAsync(Guid userId, PolicyType policyType,
                OrganizationUserStatusType minStatus)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var query = new PolicyReadByTypeApplicableToUserQuery(userId, policyType, minStatus);
            var results = await query.Run(dbContext).ToListAsync();
            return Mapper.Map<List<Core.Entities.Policy>>(results);
        }
    }

    public async Task<int> GetCountByTypeApplicableToUserIdAsync(Guid userId, PolicyType policyType,
                OrganizationUserStatusType minStatus)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var query = new PolicyReadByTypeApplicableToUserQuery(userId, policyType, minStatus);
            return await GetCountFromQuery(query);
        }
    }
}
