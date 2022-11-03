using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class ApiKeyRepository : Repository<Core.Entities.ApiKey, ApiKey, Guid>, IApiKeyRepository
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
            .Select(apiKey => new ApiKeyDetails
            {
                Id = apiKey.Id,
                ServiceAccountId = apiKey.ServiceAccountId,
                Name = apiKey.Name,
                ClientSecret = apiKey.ClientSecret,
                Scope = apiKey.Scope,
                EncryptedPayload = apiKey.EncryptedPayload,
                Key = apiKey.Key,
                ExpireAt = apiKey.ExpireAt,
                CreationDate = apiKey.CreationDate,
                RevisionDate = apiKey.RevisionDate,
                ServiceAccountOrganizationId = apiKey.ServiceAccount.OrganizationId
            })
            .FirstOrDefaultAsync();

        return Mapper.Map<ApiKeyDetails>(entity);
    }
}
