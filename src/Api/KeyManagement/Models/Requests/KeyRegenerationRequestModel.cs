#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class KeyRegenerationRequestModel
{
    [Required]
    public required string UserPublicKey { get; set; }

    [Required]
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
