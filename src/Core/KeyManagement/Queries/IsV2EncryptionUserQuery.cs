using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.KeyManagement.Utilities;

namespace Bit.Core.KeyManagement.Queries;

public class IsV2EncryptionUserQuery(IUserSignatureKeyPairRepository userSignatureKeyPairRepository)
    : IIsV2EncryptionUserQuery
{
    public async Task<bool> Run(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var hasSignatureKeyPair = await userSignatureKeyPairRepository.GetByUserIdAsync(user.Id) != null;
        var isPrivateKeyEncryptionV2 =
            !string.IsNullOrWhiteSpace(user.PrivateKey) &&
            EncryptionParsing.GetEncryptionType(user.PrivateKey) == EncryptionType.XChaCha20Poly1305_B64;

        return hasSignatureKeyPair switch
        {
            // Valid v2 user
            true when isPrivateKeyEncryptionV2 => true,
            // Valid v1 user
            false when !isPrivateKeyEncryptionV2 => false,
            _ => throw new InvalidOperationException(
                "User is in an invalid state for key rotation. User has a signature key pair, but the private key is not in v2 format, or vice versa.")
        };
    }
}


