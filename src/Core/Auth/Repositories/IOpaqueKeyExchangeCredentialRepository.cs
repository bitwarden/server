using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Auth.Repositories;

public interface IOpaqueKeyExchangeCredentialRepository : IRepository<OpaqueKeyExchangeCredential, Guid>
{
    Task<OpaqueKeyExchangeCredential?> GetByIdAsync(Guid id, Guid userId);
    Task<ICollection<OpaqueKeyExchangeCredential>> GetManyByUserIdAsync(Guid userId);
    Task<bool> UpdateAsync(OpaqueKeyExchangeCredential credential);
    //TODO implement rotation
    UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<OpaqueKeyExchangeRotateKeyData> credentials);
}
