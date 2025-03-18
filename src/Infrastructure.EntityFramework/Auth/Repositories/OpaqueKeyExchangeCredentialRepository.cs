using AutoMapper;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OpaqueKeyExchangeCredentialRepository : Repository<OpaqueKeyExchangeCredential, OpaqueKeyExchangeCredential, Guid>, IOpaqueKeyExchangeCredentialRepository
{
    public OpaqueKeyExchangeCredentialRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(serviceScopeFactory, mapper, (DatabaseContext context) => null)
    {
    }

    public Task<OpaqueKeyExchangeCredential> GetByUserIdAsync(Guid userId)
    {
        return null;
    }
    public UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<OpaqueKeyExchangeRotateKeyData> credentials)
    {
        return null;
    }
}
