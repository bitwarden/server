#nullable enable
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class KeyRegenerationRequestModel
{
    public required string UserPublicKey { get; set; }

    [EncryptedString]
    public required string UserKeyEncryptedUserPrivateKey { get; set; }

    public UserAsymmetricKeys ToUserAsymmetricKeys(Guid userId)
    {
        return new UserAsymmetricKeys
        {
            UserId = userId,
            PublicKey = UserPublicKey,
            UserKeyEncryptedPrivateKey = UserKeyEncryptedUserPrivateKey,
        };
    }
}
