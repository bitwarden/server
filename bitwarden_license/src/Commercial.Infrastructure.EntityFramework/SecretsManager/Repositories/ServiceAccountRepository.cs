using AutoMapper;
using Bit.Core.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;

public class ServiceAccountRepository : Repository<Core.SecretsManager.Entities.ServiceAccount, ServiceAccount, Guid>, IServiceAccountRepository
{
    public ServiceAccountRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.ServiceAccount)
    { }

    public async Task<IEnumerable<Core.SecretsManager.Entities.ServiceAccount>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var serviceAccounts = await dbContext.ServiceAccount
                .Where(c => c.OrganizationId == organizationId)
                .OrderBy(c => c.RevisionDate)
                .ToListAsync();
            return Mapper.Map<List<Core.SecretsManager.Entities.ServiceAccount>>(serviceAccounts);
        }
    }
}
