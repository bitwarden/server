using AutoMapper;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class PolicyRepository : Repository<AdminConsoleEntities.Policy, Policy, Guid>, IPolicyRepository
{
    public PolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Policies)
    { }

    public async Task<AdminConsoleEntities.Policy> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType type)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Policies
                .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Type == type);
            return Mapper.Map<AdminConsoleEntities.Policy>(results);
        }
    }

    public async Task<ICollection<AdminConsoleEntities.Policy>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Policies
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync();
            return Mapper.Map<List<AdminConsoleEntities.Policy>>(results);
        }
    }

    public async Task<ICollection<AdminConsoleEntities.Policy>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var query = new PolicyReadByUserIdQuery(userId);
            var results = await query.Run(dbContext).ToListAsync();
            return Mapper.Map<List<AdminConsoleEntities.Policy>>(results);
        }
    }
}
