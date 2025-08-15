
using Bit.Core.KeyManagement.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.Repositories;

public interface IUserSignatureKeyPairRepository : IRepository<UserSignatureKeyPair, Guid>
{
    public Task<SignatureKeyPairData?> GetByUserIdAsync(Guid userId);
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid grantorId, SignatureKeyPairData signatureKeyPair);
    public UpdateEncryptedDataForKeyRotation SetUserSignatureKeyPair(Guid userId, SignatureKeyPairData signatureKeyPair);
}
