using AutoMapper;
using Bit.Core.PrivilegedAccessManagement.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntity = Bit.Core.PrivilegedAccessManagement.Entities.AccessRule;
using EfModel = Bit.Infrastructure.EntityFramework.PrivilegedAccessManagement.Models.AccessRule;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.PrivilegedAccessManagement.Repositories;

public class AccessRuleRepository : Repository<CoreEntity, EfModel, Guid>, IAccessRuleRepository
{
    public AccessRuleRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.AccessRules)
    { }

    public async Task<ICollection<CoreEntity>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var rules = await dbContext.AccessRules
            .Where(p => p.OrganizationId == organizationId)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(rules);
    }
}
