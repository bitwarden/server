
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.KeyManagement.Repositories;

namespace Bit.Core.KeyManagement.Queries;


public class UserAccountKeysQuery(IUserSignatureKeyPairRepository signatureKeyPairRepository) : IUserAccountKeysQuery
{
    public async Task<UserAccountKeysData> Run(User user)
    {
        if (user.GetSecurityVersion() < 2)
        {
            return new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = user.GetPublicKeyEncryptionKeyPair(),
            };
        }
        else
        {
            return new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = user.GetPublicKeyEncryptionKeyPair(),
                SignatureKeyPairData = await signatureKeyPairRepository.GetByUserIdAsync(user.Id),
                SecurityStateData = new SecurityStateData
                {
                    SecurityState = user.SecurityState!,
                    SecurityVersion = user.GetSecurityVersion(),
                }
            };
        }
    }
}
