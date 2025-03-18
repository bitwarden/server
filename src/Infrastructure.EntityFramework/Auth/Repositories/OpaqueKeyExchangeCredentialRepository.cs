using AutoMapper;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.KeyManagement.UserKey;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable


namespace Bit.Infrastructure.Dapper.Auth.Repositories;

public class OpaqueKeyExchangeCredentialRepository : Repository<OpaqueKeyExchangeCredential, OpaqueKeyExchangeCredential, Guid>, IOpaqueKeyExchangeCredentialRepository
{
    public OpaqueKeyExchangeCredentialRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper, Func<DatabaseContext, DbSet<OpaqueKeyExchangeCredential>> getDbSet) : base(serviceScopeFactory, mapper, getDbSet)
    {
    }

    public Task<OpaqueKeyExchangeCredential?> GetByUserIdAsync(Guid userId) => throw new NotImplementedException();
    public UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<OpaqueKeyExchangeRotateKeyData> credentials) => throw new NotImplementedException();
}
