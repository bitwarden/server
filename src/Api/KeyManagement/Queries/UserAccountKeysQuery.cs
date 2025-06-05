#nullable enable

using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;

namespace Bit.Api.KeyManagement.Queries;

public interface IUserAccountKeysQuery
{
    Task<UserAccountKeysData> Run(User user);
}

public class UserAccountKeysQuery(IUserSignatureKeyPairRepository signatureKeyPairRepository) : IUserAccountKeysQuery
{
    public async Task<UserAccountKeysData> Run(User user)
    {
        var userAccountKeysData = new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = user.GetPublicKeyEncryptionKeyPair(),
            SignatureKeyPairData = await signatureKeyPairRepository.GetByUserIdAsync(user.Id)
        };
        return userAccountKeysData;
    }
}
