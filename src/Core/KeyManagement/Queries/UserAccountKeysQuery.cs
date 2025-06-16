#nullable enable

using Bit.Api.KeyManagement.Queries.Interfaces;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data.Models;
using Bit.Core.KeyManagement.Repositories;

namespace Bit.Core.KeyManagement.Queries;


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
