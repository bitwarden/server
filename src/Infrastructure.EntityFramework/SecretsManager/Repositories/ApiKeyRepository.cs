using AutoMapper;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.SecretsManager.Repositories;

public class ApiKeyRepository : Repository<Core.SecretsManager.Entities.ApiKey, ApiKey, Guid>, IApiKeyRepository
{
    public ApiKeyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.ApiKeys)
    {
    }

    public async Task<ApiKeyDetails> GetDetailsByIdAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entity = await GetDbSet(dbContext)
            .Where(apiKey => apiKey.Id == id)
            .Include(apiKey => apiKey.ServiceAccount)
            .Select(apiKey => new ServiceAccountApiKeyDetails(apiKey, apiKey.ServiceAccount.OrganizationId))
            .FirstOrDefaultAsync();

        return Mapper.Map<ServiceAccountApiKeyDetails>(entity);
    }

    public async Task<ICollection<Core.SecretsManager.Entities.ApiKey>> GetManyByServiceAccountIdAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var apiKeys = await GetDbSet(dbContext).Where(e => e.ServiceAccountId == id).ToListAsync();

        return Mapper.Map<List<Core.SecretsManager.Entities.ApiKey>>(apiKeys);
    }
}
