using AutoMapper;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OpaqueKeyExchangeCredentialRepository : Repository<OpaqueKeyExchangeCredential, OpaqueKeyExchangeCredential, Guid>, IOpaqueKeyExchangeCredentialRepository
{
    public OpaqueKeyExchangeCredentialRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OpaqueKeyExchangeCredentials)
    {
    }

    public async Task<OpaqueKeyExchangeCredential> GetByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var opaqueConfig = await GetDbSet(dbContext).SingleOrDefaultAsync(sc => sc.UserId == userId);
            return Mapper.Map<OpaqueKeyExchangeCredential>(opaqueConfig);
        }
    }
    public UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<OpaqueKeyExchangeRotateKeyData> credentials)
    {
        return null;
    }
}
