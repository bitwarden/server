using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OrganizationApiKeyRepository
    : Repository<OrganizationApiKey, Models.OrganizationApiKey, Guid>,
        IOrganizationApiKeyRepository
{
    public OrganizationApiKeyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.OrganizationApiKeys) { }

    public async Task<IEnumerable<OrganizationApiKey>> GetManyByOrganizationIdTypeAsync(
        Guid organizationId,
        OrganizationApiKeyType? type = null
    )
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var apiKeys = await dbContext
                .OrganizationApiKeys.Where(o =>
                    o.OrganizationId == organizationId && (type == null || o.Type == type)
                )
                .ToListAsync();
            return Mapper.Map<List<OrganizationApiKey>>(apiKeys);
        }
    }
}
