using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class ApiKeyRepository : Repository<Core.Entities.ApiKey, ApiKey, Guid>, IApiKeyRepository
{
    public ApiKeyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.ApiKeys)
    {
    }

    public Task<ICollection<Core.Entities.ApiKey>> GetManyByServiceAccountIdAsync(Guid id) => throw new NotImplementedException();
}
