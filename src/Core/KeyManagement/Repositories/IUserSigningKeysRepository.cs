#nullable enable
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.Repositories;

public interface IUserSigningKeysRepository : IRepository<UserSigningKeys, Guid>
{
    public Task<SigningKeyData?> GetByUserIdAsync(Guid userId);
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid grantorId, SigningKeyData signingKeys);
    public UpdateEncryptedDataForKeyRotation SetUserSigningKeys(Guid userId, SigningKeyData signingKeys);
}
