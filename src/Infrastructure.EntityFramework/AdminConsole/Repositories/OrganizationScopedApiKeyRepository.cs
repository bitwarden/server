using AutoMapper;
using Bit.Core.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class OrganizationScopedApiKeyRepository
    : Repository<AdminConsoleEntities.OrganizationScopedApiKey, OrganizationScopedApiKey, Guid>,
      IOrganizationScopedApiKeyRepository
{
    public OrganizationScopedApiKeyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationScopedApiKeys)
    { }

    public async Task<ICollection<AdminConsoleEntities.OrganizationScopedApiKey>> GetManyByOrganizationIdAsync(
        Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var apiKeys = await dbContext.OrganizationScopedApiKeys
            .Where(k => k.OrganizationId == organizationId)
            .ToListAsync();
        return Mapper.Map<List<AdminConsoleEntities.OrganizationScopedApiKey>>(apiKeys);
    }
}
