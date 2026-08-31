using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntity = Bit.Pam.Entities.PamTargetSystem;
using EfModel = Bit.Infrastructure.EntityFramework.Pam.Models.PamTargetSystem;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

public class PamTargetSystemRepository : Repository<CoreEntity, EfModel, Guid>, IPamTargetSystemRepository
{
    public PamTargetSystemRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.PamTargetSystems)
    { }

    public async Task<ICollection<CoreEntity>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        // Unordered, matching PamTargetSystem_ReadByOrganizationId -- callers sort for display.
        var targets = await dbContext.PamTargetSystems
            .Where(t => t.OrganizationId == organizationId)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(targets);
    }
}
