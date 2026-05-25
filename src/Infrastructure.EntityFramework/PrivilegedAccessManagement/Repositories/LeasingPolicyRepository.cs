using AutoMapper;
using Bit.Core.PrivilegedAccessManagement.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntity = Bit.Core.PrivilegedAccessManagement.Entities.LeasingPolicy;
using EfModel = Bit.Infrastructure.EntityFramework.PrivilegedAccessManagement.Models.LeasingPolicy;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.PrivilegedAccessManagement.Repositories;

public class LeasingPolicyRepository : Repository<CoreEntity, EfModel, Guid>, ILeasingPolicyRepository
{
    public LeasingPolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.LeasingPolicies)
    { }

    public async Task<ICollection<CoreEntity>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var policies = await dbContext.LeasingPolicies
            .Where(p => p.OrganizationId == organizationId)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(policies);
    }
}
