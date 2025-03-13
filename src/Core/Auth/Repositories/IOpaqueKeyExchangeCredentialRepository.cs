using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Auth.Repositories;

public interface IOpaqueKeyExchangeCredentialRepository : IRepository<OpaqueKeyExchangeCredential, Guid>
{
    Task<OpaqueKeyExchangeCredential?> GetByUserIdAsync(Guid userId);
    //TODO implement rotation
    UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<OpaqueKeyExchangeRotateKeyData> credentials);
}
