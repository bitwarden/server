using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Auth.Repositories;

public interface IWebAuthnCredentialRepository : IRepository<WebAuthnCredential, Guid>
{
    Task<WebAuthnCredential?> GetByIdAsync(Guid id, Guid userId);
    Task<ICollection<WebAuthnCredential>> GetManyByUserIdAsync(Guid userId);
    Task<bool> UpdateAsync(WebAuthnCredential credential);
    UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(
        Guid userId,
        IEnumerable<WebAuthnLoginRotateKeyData> credentials
    );
}
