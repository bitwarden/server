using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class SsoConfigRepository : Repository<Core.Entities.SsoConfig, SsoConfig, long>, ISsoConfigRepository
{
    public SsoConfigRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.SsoConfigs)
    { }

    public async Task<Core.Entities.SsoConfig> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var ssoConfig = await GetDbSet(dbContext).SingleOrDefaultAsync(sc => sc.OrganizationId == organizationId);
            return Mapper.Map<Core.Entities.SsoConfig>(ssoConfig);
        }
    }

    public async Task<Core.Entities.SsoConfig> GetByIdentifierAsync(string identifier)
    {

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var ssoConfig = await GetDbSet(dbContext).SingleOrDefaultAsync(sc => sc.Organization.Identifier == identifier);
            return Mapper.Map<Core.Entities.SsoConfig>(ssoConfig);
        }
    }

    public async Task<ICollection<Core.Entities.SsoConfig>> GetManyByRevisionNotBeforeDate(DateTime? notBefore)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var ssoConfigs = await GetDbSet(dbContext).Where(sc => sc.Enabled && sc.RevisionDate >= notBefore).ToListAsync();
            return Mapper.Map<List<Core.Entities.SsoConfig>>(ssoConfigs);
        }
    }
}
